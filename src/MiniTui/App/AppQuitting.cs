namespace MiniTui;

/// <summary>Why an app is quitting (1.x spec §25.3).</summary>
public enum QuitReason
{
    /// <summary>Ctrl+C, or <see cref="App.RequestQuit"/>: the app can say no.</summary>
    Requested,

    /// <summary>The terminal went away: SIGHUP on Unix, the console window closing on Windows.</summary>
    Hangup,

    /// <summary>SIGTERM on Unix; logoff or shutdown on Windows.</summary>
    Terminated,
}

// Asking before quitting (1.x spec §25.3).
public partial class App
{
    /// <summary>How long a hangup or SIGTERM waits for the apps' <see cref="OnQuitting"/> handlers.</summary>
    internal static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(2);


    // Apps running on the console, innermost last, for the signals
    private static readonly Lock ConsoleAppsGate = new();
    private static readonly List<App> ConsoleApps = [];

    private Func<QuitReason, bool>? _onQuitting;

    /// <summary>
    /// After its handlers ran for a signal, how long the loop waits for the process to end before it quits anyway, in
    /// case something else cancelled the signal. Shorter in tests.
    /// </summary>
    internal TimeSpan EndedWait { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Overrides whether the app counts as running on the console, for the signals; for tests.</summary>
    internal bool? ConsoleOverride { get; set; }

    /// <summary>Overrides whether the console has hung up; for tests.</summary>
    internal Func<bool>? HungUpOverride { get; set; }

    // Set once the handlers ran for a signal: the loop handles and draws nothing more
    private volatile bool _ended;

    /// <summary>
    /// Run <paramref name="quitting"/> on the UI thread before the app quits (1.x spec §25.3). For
    /// <see cref="QuitReason.Requested"/> (Ctrl+C or <see cref="RequestQuit"/>), return false to keep running; to ask
    /// first, return false and call <see cref="Quit"/> once the user has answered. For <see cref="QuitReason.Hangup"/>
    /// and <see cref="QuitReason.Terminated"/> the process ends whatever it returns, and the handler has 2 seconds.
    /// <see cref="Quit"/> doesn't call it.
    /// </summary>
    public App OnQuitting(Func<QuitReason, bool> quitting)
    {
        ArgumentNullException.ThrowIfNull(quitting);
        _onQuitting = quitting;
        return this;
    }

    /// <summary>
    /// Quit as Ctrl+C does: the <see cref="OnQuitting"/> handler, given <see cref="QuitReason.Requested"/>, decides.
    /// Without one, or when the app isn't running, this is <see cref="Quit"/>. Thread-safe.
    /// </summary>
    public void RequestQuit()
    {
        if (_onQuitting is null || !_running)
        {
            Quit();
            return;
        }
        InvokeOnUiThread(AskToQuit);
    }

    private void AskToQuit()
    {
        if (_onQuitting is not { } quitting || !_running)
        {
            Quit();
            return;
        }
        // A handler that throws goes to OnError and isn't a yes
        var quit = false;
        Guard(() => quit = quitting(QuitReason.Requested));
        if (quit) Quit();
    }

    /// <summary>Deliver a hangup or SIGTERM to this app as the signal would, but end the loop after; for tests.</summary>
    internal void SignalForTest(QuitReason reason) => _queue.Writer.TryWrite(new Ending(reason, [this], null, Park: false));

    private void AddConsoleApp()
    {
        TerminalSession.Ending = OnEndingSignal;
        lock (ConsoleAppsGate) ConsoleApps.Add(this);
    }

    private void RemoveConsoleApp()
    {
        lock (ConsoleAppsGate) ConsoleApps.Remove(this);
    }

    /// <summary>
    /// A hangup or SIGTERM, on the signal thread, before the terminal is restored and the process ends: each running
    /// app with a handler runs it on its UI thread, innermost first, within <see cref="SignalTimeout"/>. A thread with
    /// nested apps runs them all in the innermost loop, since the outer ones are waiting on it.
    /// </summary>
    internal static void OnEndingSignal(QuitReason reason)
    {
        if (TerminalSession.IsConsoleSuspended) return;
        App[] apps;
        lock (ConsoleAppsGate) apps = [.. ConsoleApps];
        Array.Reverse(apps);

        var threads = apps.Where(a => a._running).GroupBy(a => a._uiThreadId)
            .Select(g => g.ToArray())
            .Where(g => g.Any(a => !a._ended && a._onQuitting is not null))
            .ToList();
        if (threads.Count == 0) return;

        using var done = new CountdownEvent(threads.Count);
        foreach (var thread in threads)
        {
            if (!thread[0]._queue.Writer.TryWrite(new Ending(reason, thread, done, Park: true))) done.Signal();
        }
        done.Wait(SignalTimeout);
    }

    /// <summary>
    /// True if <paramref name="error"/> is the terminal going away: an I/O error on a console that has hung up. The
    /// hangup's SIGHUP comes with a SIGCONT, whose redraw can fail before the SIGHUP is handled.
    /// </summary>
    private bool IsHangup(Exception error)
    {
        if (error is not IOException || _run is not { ConsoleListed: true }) return false;
        if (HungUpOverride is { } hungUp) return hungUp();
        return (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && UnixTerminal.IsHungUp(1);
    }

    // The terminal went away before (or without) a SIGHUP: run the handlers of the apps on this thread now
    private void HangUp()
    {
        if (_ended) return;
        App[] apps;
        lock (ConsoleAppsGate) apps = [.. ConsoleApps.Where(a => a._running && a._uiThreadId == _uiThreadId)];
        Array.Reverse(apps);
        RunEnding(new Ending(QuitReason.Hangup, apps.Length > 0 ? apps : [this], null, Park: true));
    }

    // On the UI thread: the handlers, then nothing more is handled or drawn
    private void RunEnding(Ending ending)
    {
        try
        {
            foreach (var app in ending.Apps)
            {
                app._ended = true;
                if (app._onQuitting is not { } quitting) continue;
                using var ui = app.EnterUi();
                app.Guard(() => quitting(ending.Reason));
            }
        }
        finally
        {
            try
            {
                ending.Done?.Signal();
            }
            catch (ObjectDisposedException)
            {
                // The signal thread stopped waiting
            }
        }
        if (!ending.Park) Quit();
    }

    // The process is ending; if it hasn't in a while, something cancelled the signal, so quit as Quit() does
    private bool WaitToEnd()
    {
        Thread.Sleep(EndedWait);
        Quit();
        return false;
    }

    /// <summary>A hangup or SIGTERM for <paramref name="Apps"/>, innermost first, all on the loop's thread.</summary>
    private sealed record Ending(QuitReason Reason, App[] Apps, CountdownEvent? Done, bool Park);
}
