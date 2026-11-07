using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MiniTui;

/// <summary>
/// Raw terminal mode, bracketed paste, mouse reporting and non-blocking reads on Unix (1.x spec §2.5–2.6, §3.6, §4.6). The termios struct is handled as an
/// opaque buffer, so nothing here depends on its layout or flag values, which differ between Linux and macOS.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static unsafe partial class UnixTerminal
{
    // Larger than termios on Linux (60 bytes) and macOS (72 bytes)
    private const int TermiosSize = 256;
    private const int TcsaNow = 0;
    private const short PollIn = 0x1;
    private const int Eintr = 4;
    private const int Eio = 5;
    private const int SigTstpLinux = 20;
    private const int SigTstpMacOS = 18;

    private static readonly Lock Gate = new();
    private static readonly byte[] Saved = new byte[TermiosSize];
    private static readonly byte[] RawMode = new byte[TermiosSize];
    private static int s_fd = -1;
    private static int s_outputFd = -1;
    private static bool s_exitHooked;
    private static bool s_mouse;
    private static bool s_hover;
    private static KeyboardProtocol s_keyboard;

    // The fd whose saved mode Suspend kept, so entering again doesn't save the mode a child left behind
    private static int s_suspendedFd = -1;

    /// <summary>The platforms whose libc and termios this has been checked against.</summary>
    public static bool IsSupported =>
        (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64 or Architecture.Arm)
        || (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64);

    /// <summary>
    /// True if <paramref name="fd"/> is a terminal that has hung up: the other end closed, so every call but close
    /// fails with EIO (1.x spec §25.3).
    /// </summary>
    public static bool IsHungUp(int fd)
    {
        var termios = stackalloc byte[TermiosSize];
        return tcgetattr(fd, termios) != 0 && Marshal.GetLastPInvokeError() == Eio;
    }

    /// <summary>True while a terminal is in raw mode.</summary>
    public static bool IsRaw
    {
        get
        {
            lock (Gate) return s_fd >= 0;
        }
    }

    /// <summary>
    /// Save the mode of <paramref name="fd"/> and switch it to raw mode (<c>cfmakeraw</c>). Then turn on bracketed
    /// paste by writing to <paramref name="outputFd"/>, if it's a terminal (−1 for none).
    /// </summary>
    /// <returns>False if <paramref name="fd"/> isn't a terminal or a call fails; the mode is then unchanged.</returns>
    /// <remarks>After <see cref="Suspend"/> on the same fd, the mode saved before it is kept (1.x spec §5.3).</remarks>
    public static bool TryEnterRaw(int fd, int outputFd)
    {
        lock (Gate)
        {
            if (s_fd >= 0) return s_fd == fd;
            try
            {
                if (isatty(fd) != 1) return false;
                var keepSaved = s_suspendedFd == fd;
                if (!keepSaved) Array.Clear(Saved);
                Array.Clear(RawMode);
                fixed (byte* saved = Saved)
                fixed (byte* raw = RawMode)
                {
                    if (!keepSaved && tcgetattr(fd, saved) != 0) return false;
                    Saved.CopyTo(RawMode, 0);
                    cfmakeraw(raw);
                    if (tcsetattr(fd, TcsaNow, raw) != 0) return false;

                    // Keep what the terminal actually stored, so Reapply compares like with like
                    Array.Clear(RawMode);
                    if (tcgetattr(fd, raw) != 0) return false;
                }
                s_fd = fd;
                s_suspendedFd = -1;
                s_outputFd = outputFd >= 0 && isatty(outputFd) == 1 ? outputFd : -1;
                if (s_outputFd >= 0) Write(s_outputFd, "\e[?2004h"u8);

                // A backstop for sources used without a console session, which restores on exit itself
                if (!s_exitHooked)
                {
                    AppDomain.CurrentDomain.ProcessExit += (_, _) => Restore();
                    s_exitHooked = true;
                }
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Turn mouse reporting (presses, releases, drags and the wheel, SGR encoded; with <paramref name="hover"/>, every
    /// move too) on or off, while in raw mode.
    /// </summary>
    public static void SetMouse(bool enabled, bool hover = false)
    {
        lock (Gate)
        {
            hover &= enabled;
            if (s_fd < 0 || (s_mouse == enabled && s_hover == hover)) return;
            if (s_outputFd >= 0 && s_mouse) Write(s_outputFd, MouseModes.Off(s_hover));
            s_mouse = enabled;
            s_hover = hover;
            if (s_outputFd >= 0 && enabled) Write(s_outputFd, MouseModes.On(hover));
        }
    }

    /// <summary>Switch the keyboard protocol (1.x spec §12.5), while in raw mode. Turned off again on every restore path.</summary>
    public static void SetKeyboard(KeyboardProtocol protocol)
    {
        lock (Gate)
        {
            if (s_fd < 0 || s_keyboard == protocol || s_outputFd < 0) return;
            Write(s_outputFd, KeyboardModes.Off(s_keyboard));
            Write(s_outputFd, KeyboardModes.On(protocol));
            s_keyboard = protocol;
        }
    }

    /// <summary>Turn mouse reporting and bracketed paste off and put back the mode saved by <see cref="TryEnterRaw"/>. Idempotent, and never throws.</summary>
    public static void Restore() => RestoreCore(keepSaved: false);

    /// <summary>Like <see cref="Restore"/>, but the next <see cref="TryEnterRaw"/> on this fd reuses the saved mode.</summary>
    public static void Suspend() => RestoreCore(keepSaved: true);

    /// <summary>
    /// Stop the process with SIGTSTP, as Ctrl+Z does in a shell, and return once it's continued. The default action
    /// only stops a .NET process that has no SIGTSTP handler registered (1.x spec §5.5).
    /// </summary>
    public static void StopProcess() => kill(Environment.ProcessId, OperatingSystem.IsMacOS() ? SigTstpMacOS : SigTstpLinux);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Runs on exit and signal paths, which must not throw.")]
    private static void RestoreCore(bool keepSaved)
    {
        lock (Gate)
        {
            if (s_fd < 0)
            {
                if (!keepSaved) s_suspendedFd = -1;
                return;
            }
            try
            {
                if (s_outputFd >= 0 && s_mouse) Write(s_outputFd, MouseModes.Off(s_hover));
                if (s_outputFd >= 0) Write(s_outputFd, KeyboardModes.Off(s_keyboard));
                if (s_outputFd >= 0) Write(s_outputFd, "\e[?2004l"u8);
            }
            catch (Exception) { }
            s_mouse = false;
            s_hover = false;
            s_keyboard = KeyboardProtocol.Legacy;
            try
            {
                fixed (byte* saved = Saved) tcsetattr(s_fd, TcsaNow, saved);
            }
            catch (Exception) { }
            s_suspendedFd = keepSaved ? s_fd : -1;
            s_fd = -1;
            s_outputFd = -1;
        }
    }

    /// <summary>
    /// Switch back to raw mode if something else changed the terminal's mode. .NET's console code does that
    /// after a child process exits and on SIGCONT.
    /// </summary>
    public static void Reapply()
    {
        lock (Gate)
        {
            if (s_fd < 0) return;
            var current = stackalloc byte[TermiosSize];
            new Span<byte>(current, TermiosSize).Clear();
            if (tcgetattr(s_fd, current) != 0) return;
            if (new ReadOnlySpan<byte>(current, TermiosSize).SequenceEqual(RawMode)) return;
            fixed (byte* raw = RawMode) tcsetattr(s_fd, TcsaNow, raw);
        }
    }

    /// <summary>Wait up to <paramref name="timeoutMs"/> for input on <paramref name="fd"/>.</summary>
    /// <returns>True if input, end of input or an error is ready to read; false on a timeout or a signal.</returns>
    public static bool Poll(int fd, int timeoutMs)
    {
        var request = new PollFd { Fd = fd, Events = PollIn };
        return poll(&request, 1, timeoutMs) > 0 && request.Revents != 0;
    }

    /// <summary>Read what's available into <paramref name="buffer"/>. Only call after <see cref="Poll"/> returned true.</summary>
    /// <returns>Bytes read; 0 at end of input; −1 on an error.</returns>
    public static int Read(int fd, Span<byte> buffer)
    {
        fixed (byte* p = buffer)
        {
            while (true)
            {
                var n = read(fd, p, (nuint)buffer.Length);
                if (n >= 0) return (int)n;
                if (Marshal.GetLastPInvokeError() != Eintr) return -1;
            }
        }
    }

    /// <summary>Write all of <paramref name="bytes"/>, retrying on EINTR; gives up on any other error.</summary>
    private static void Write(int fd, ReadOnlySpan<byte> bytes)
    {
        fixed (byte* p = bytes)
        {
            var offset = 0;
            while (offset < bytes.Length)
            {
                var n = write(fd, p + offset, (nuint)(bytes.Length - offset));
                if (n > 0) offset += (int)n;
                else if (n < 0 && Marshal.GetLastPInvokeError() == Eintr) continue;
                else return;
            }
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int Fd;
        public short Events;
        public short Revents;
    }

    [LibraryImport("libc", SetLastError = true)]
    private static partial int isatty(int fd);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int tcgetattr(int fd, byte* termios);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int tcsetattr(int fd, int optionalActions, byte* termios);

    [LibraryImport("libc")]
    private static partial void cfmakeraw(byte* termios);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int poll(PollFd* fds, nuint nfds, int timeout);

    [LibraryImport("libc", SetLastError = true)]
    private static partial nint write(int fd, byte* buffer, nuint count);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int kill(int pid, int signal);

    [LibraryImport("libc", SetLastError = true)]
    private static partial nint read(int fd, byte* buffer, nuint count);
}
