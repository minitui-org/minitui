using System.Runtime.InteropServices;

namespace MiniTui;

/// <summary>
/// Process-wide, ref-counted terminal sessions (§5.4). The first lease on a device enters the
/// terminal and the last one leaves it, so nested apps share one session instead of each
/// clearing the screen. While a console session is active, the terminal is also restored on
/// SIGTERM, SIGHUP, SIGQUIT, process exit and unhandled exceptions. A session can be suspended, giving the terminal
/// back for a while (1.x spec §5.4).
/// </summary>
internal static class TerminalSession
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<object, Session> Sessions = [];

    // Running apps that handle job control on the console, innermost last (1.x spec §5.5)
    private static readonly List<JobControl> JobControls = [];

    /// <summary>
    /// Raised on SIGWINCH (Unix), or a window size event read by the input thread (Windows), while a console
    /// session is active. Runs on a signal or input thread.
    /// </summary>
    public static event Action? Resized;

    /// <summary>
    /// Called on the signal thread for SIGHUP and SIGTERM while a console session is active, before the terminal is
    /// restored and the process ends; it may block for a while (1.x spec §25.3).
    /// </summary>
    internal static Action<QuitReason>? Ending { get; set; }

    /// <summary>Raise <see cref="Resized"/>; for input sources that see size changes.</summary>
    internal static void RaiseResized() => Resized?.Invoke();

    /// <summary>
    /// Join the session for <paramref name="backend"/>'s device, entering the terminal if this is the first lease.
    /// </summary>
    public static TerminalLease Acquire(ITerminalBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        var key = backend is IConsoleDevice device ? device.DeviceKey : backend;

        lock (Gate)
        {
            if (!Sessions.TryGetValue(key, out var session))
            {
                backend.Enter();
                session = new Session(backend);
                if (ReferenceEquals(key, ConsoleDevice.Key)) session.RegisterRestoreHandlers();
                Sessions[key] = session;
                session.UpdateStopHandler();
            }
            else if (session.Suspended)
            {
                throw new InvalidOperationException(
                    "The terminal is suspended (App.Suspend); an app can't start until the suspend ends.");
            }
            session.Count++;
            session.Version++;
            return new TerminalLease(session, key);
        }
    }

    /// <summary>True if a session is active for the given backend's device.</summary>
    public static bool IsActive(ITerminalBackend backend)
    {
        var key = backend is IConsoleDevice device ? device.DeviceKey : backend;
        lock (Gate) return Sessions.ContainsKey(key);
    }

    internal static void Release(Session session, object key)
    {
        lock (Gate)
        {
            session.Version++;
            if (--session.Count > 0) return;
            Sessions.Remove(key);
            session.UnregisterRestoreHandlers();
            if (!session.Suspended) session.Backend.Leave();
        }
    }

    /// <summary>
    /// Leave the terminal, whatever the lease count, until the returned scope is disposed; then enter it again, and
    /// every terminal on the session redraws fully. While a console session is suspended, SIGINT and SIGQUIT don't
    /// end the process, and SIGTSTP has its default action. Nested calls do nothing.
    /// </summary>
    internal static IDisposable Suspend(Session session)
    {
        lock (Gate)
        {
            if (session.Suspended || !Sessions.ContainsValue(session)) return DisposeAction.None;
            session.Suspended = true;
            session.UpdateStopHandler();
            session.SetSuspendedSignals(true);
            session.Backend.Leave();
        }
        return new DisposeAction(() =>
        {
            lock (Gate)
            {
                if (!session.Suspended) return;
                session.Suspended = false;
                session.SetSuspendedSignals(false);
                if (Sessions.ContainsValue(session))
                {
                    session.Backend.Enter();
                    session.Version++;
                }
                session.UpdateStopHandler();
            }
        });
    }

    /// <summary>True while the console session is suspended.</summary>
    internal static bool IsConsoleSuspended
    {
        get
        {
            lock (Gate) return Sessions.TryGetValue(ConsoleDevice.Key, out var session) && session.Suspended;
        }
    }

    /// <summary>
    /// Have SIGTSTP from outside call <paramref name="stop"/> instead of stopping the process, and SIGCONT the
    /// process didn't cause call <paramref name="continued"/>, while the returned scope is held. Only the most
    /// recent listener is called. Both run on a signal thread.
    /// </summary>
    internal static IDisposable ListenForJobControl(Action stop, Action continued)
    {
        var listener = new JobControl(stop, continued);
        lock (Gate)
        {
            JobControls.Add(listener);
            ConsoleSession()?.UpdateStopHandler();
        }
        return new DisposeAction(() =>
        {
            lock (Gate)
            {
                JobControls.Remove(listener);
                ConsoleSession()?.UpdateStopHandler();
            }
        });
    }

    /// <summary>SIGTSTP from outside: the listening app stops itself, restoring the terminal first.</summary>
    /// <returns>True to cancel the default action, because an app is handling it.</returns>
    internal static bool HandleStopSignal()
    {
        if (CurrentJobControl() is not { } listener) return false;
        listener.Stop();
        return true;
    }

    /// <summary>SIGCONT. A resume from the SIGTSTP an app sent itself is handled by its Suspend.</summary>
    internal static void HandleContinueSignal(bool suspended)
    {
        if (!suspended) CurrentJobControl()?.Continued();
    }

    private static Session? ConsoleSession() => Sessions.GetValueOrDefault(ConsoleDevice.Key);

    private static JobControl? CurrentJobControl()
    {
        lock (Gate) return JobControls.Count > 0 ? JobControls[^1] : null;
    }

    private sealed record JobControl(Action Stop, Action Continued);

    internal sealed class Session(ITerminalBackend backend)
    {
        private readonly List<IDisposable> _registrations = [];
        private readonly List<IDisposable> _suspendedRegistrations = [];
        private PosixSignalRegistration? _quitRegistration;
        private PosixSignalRegistration? _stopRegistration;
        private bool _console;
        private EventHandler? _onProcessExit;
        private UnhandledExceptionEventHandler? _onUnhandledException;

        public ITerminalBackend Backend { get; } = backend;
        public int Count { get; set; }

        /// <summary>Bumped on every acquire and release, so leases can tell that someone else drew.</summary>
        public int Version { get; set; }

        /// <summary>True while <see cref="TerminalSession.Suspend"/> has the terminal given back.</summary>
        public bool Suspended { get; set; }

        public void RegisterRestoreHandlers()
        {
            _console = true;

            // The handlers let the apps save, restore the terminal and let termination proceed
            TryRegister(_registrations, PosixSignal.SIGTERM, _ => End(QuitReason.Terminated));
            TryRegister(_registrations, PosixSignal.SIGHUP, _ => End(QuitReason.Hangup));
            _quitRegistration = Register(PosixSignal.SIGQUIT, _ => Restore());
            if (!OperatingSystem.IsWindows())
            {
                TryRegister(_registrations, PosixSignal.SIGWINCH, _ => Resized?.Invoke());
                TryRegister(_registrations, PosixSignal.SIGCONT, _ => HandleContinueSignal(Suspended));
            }

            _onProcessExit = (_, _) => Restore();
            _onUnhandledException = (_, _) => Restore();
            AppDomain.CurrentDomain.ProcessExit += _onProcessExit;
            AppDomain.CurrentDomain.UnhandledException += _onUnhandledException;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "The terminal must be restored and the process allowed to end whatever the apps did.")]
        private void End(QuitReason reason)
        {
            try
            {
                Ending?.Invoke(reason);
            }
            catch (Exception)
            {
            }
            Restore();
        }

        private void Restore()
        {
            TerminalInputSource.RestoreTerminalMode();
            Backend.Leave();
        }

        /// <summary>
        /// Register the SIGTSTP handler only while an app listens and the terminal isn't suspended: once .NET has a
        /// handler, an uncancelled SIGTSTP doesn't stop the process (1.x spec §5.5).
        /// </summary>
        public void UpdateStopHandler()
        {
            var wanted = _console && !Suspended && JobControls.Count > 0;
            if (wanted == _stopRegistration is not null) return;
            if (!wanted)
            {
                _stopRegistration!.Dispose();
                _stopRegistration = null;
                return;
            }
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
            _stopRegistration = Register(PosixSignal.SIGTSTP, context => context.Cancel = HandleStopSignal());
        }

        /// <summary>While suspended, Ctrl+C and Ctrl+\ reach the child process, not this one, as in system(3).</summary>
        public void SetSuspendedSignals(bool suspended)
        {
            if (!_console) return;
            if (suspended)
            {
                _quitRegistration?.Dispose();
                _quitRegistration = null;
                foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGQUIT })
                    TryRegister(_suspendedRegistrations, signal, context => context.Cancel = true);
                return;
            }
            foreach (var registration in _suspendedRegistrations) registration.Dispose();
            _suspendedRegistrations.Clear();
            _quitRegistration ??= Register(PosixSignal.SIGQUIT, _ => Restore());
        }

        public void UnregisterRestoreHandlers()
        {
            foreach (var registration in _registrations) registration.Dispose();
            _registrations.Clear();
            foreach (var registration in _suspendedRegistrations) registration.Dispose();
            _suspendedRegistrations.Clear();
            _quitRegistration?.Dispose();
            _quitRegistration = null;
            _stopRegistration?.Dispose();
            _stopRegistration = null;
            _console = false;
            if (_onProcessExit is not null) AppDomain.CurrentDomain.ProcessExit -= _onProcessExit;
            if (_onUnhandledException is not null) AppDomain.CurrentDomain.UnhandledException -= _onUnhandledException;
            _onProcessExit = null;
            _onUnhandledException = null;
        }

        private static void TryRegister(List<IDisposable> registrations, PosixSignal signal, Action<PosixSignalContext> handler)
        {
            if (Register(signal, handler) is { } registration) registrations.Add(registration);
        }

        private static PosixSignalRegistration? Register(PosixSignal signal, Action<PosixSignalContext> handler)
        {
            try
            {
                return PosixSignalRegistration.Create(signal, handler);
            }
            catch (PlatformNotSupportedException)
            {
                return null;
            }
        }
    }
}

/// <summary>
/// A share of the terminal session. Disposing the last lease restores the terminal.
/// </summary>
public sealed class TerminalLease : IDisposable
{
    private readonly TerminalSession.Session _session;
    private readonly object _key;
    private bool _disposed;

    internal TerminalLease(TerminalSession.Session session, object key)
    {
        _session = session;
        _key = key;
    }

    /// <summary>The session's backend: the one that entered the terminal.</summary>
    internal ITerminalBackend Backend => _session.Backend;

    /// <summary>Changes whenever any lease on this session is acquired or released.</summary>
    internal int Version => _session.Version;

    /// <summary>Give the terminal back until disposed (1.x spec §5.4), whatever the lease count.</summary>
    internal IDisposable Suspend() => TerminalSession.Suspend(_session);

    /// <summary>Release this share; the last release leaves the terminal.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        TerminalSession.Release(_session, _key);
    }
}
