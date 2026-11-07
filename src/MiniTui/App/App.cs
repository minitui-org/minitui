using System.Threading.Channels;

namespace MiniTui;

/// <summary>
/// Fluent builder for creating TUI applications. Builder methods mutate the app and return it.
/// </summary>
/// <remarks>
/// All callbacks run on the thread that called <see cref="Run"/> (the UI thread). Other threads talk
/// to the app through <see cref="Post"/>, <see cref="RequestRedraw"/> and <see cref="Quit"/>.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "The Stopping source has no timer or links; it's replaced when cancelled and needs no disposal.")]
public partial class App
{
    private static readonly TimeSpan DefaultTickRate = TimeSpan.FromMilliseconds(250);

    private readonly Channel<object> _queue = Channel.CreateUnbounded<object>(
        new UnboundedChannelOptions { SingleReader = true });

    private Action<App>? _onSetup;
    private Action<CellBuffer, Rect>? _onRender;
    private Action? _onUpdate;
    private Func<ConsoleKeyInfo, bool>? _onKey;
    private Func<string, bool>? _onPaste;
    private Func<MouseEvent, bool>? _onMouse;
    private bool _mouse;
    private bool _hover;
    private bool _suspendKey;
    private Action<Event>? _onEvent;
    private Func<Exception, ErrorAction>? _onError;
    private TimeSpan? _tickRate = DefaultTickRate;
    private TimeProvider _time = TimeProvider.System;
    private TimeSpan _doubleClickTime = TimeSpan.FromMilliseconds(500);
    private (MouseButton Button, int X, int Y, long Time, int Count)? _lastPress;
    private ITerminalBackend? _backend;
    private IInputSource? _input;
    private ColorMode? _colorMode;
    private bool _captureConsole = true;
    private bool _clipboardRead;
    private Theme? _theme;
    private CancellationTokenSource _stopping = new();

    private bool _chromeStatusBar = true;
    private bool _chromeBreadcrumb = true;
    private bool _quitWhenEmpty = true;

    // Async work in flight (loads, RunTask) and dialogs waiting for the user, for ComponentHarness.RunUntilIdle
    private int _pendingWork;
    private int _awaitingUser;

    private App()
    {
        Screens = new ScreenStack(this);
        Overlays = new OverlayStack(this);
        Keys = new KeyMap()
            .Add("?", "Help", Overlays.ShowHelp)
            .Add("ctrl+c", "Quit", RequestQuit, showInHints: false);
    }

    /// <summary>
    /// Create a new App builder.
    /// </summary>
    public static App Create() => new();

    /// <summary>
    /// Set up code to run once on the UI thread before the first frame.
    /// </summary>
    public App OnSetup(Action setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        _onSetup = _ => setup();
        return this;
    }

    /// <summary>
    /// Set up code to run once on the UI thread before the first frame. It receives the app,
    /// for example to start loads with <see cref="Loadable{T}.Load"/>.
    /// </summary>
    public App OnSetup(Action<App> setup)
    {
        _onSetup = setup;
        return this;
    }

    /// <summary>
    /// Configure the render callback, called with the buffer and screen rect whenever a frame is drawn.
    /// </summary>
    /// <exception cref="InvalidOperationException">Screens have been pushed; an app uses either screens or a render callback.</exception>
    public App Render(Action<CellBuffer, Rect> render)
    {
        if (Screens.Count > 0)
            throw new InvalidOperationException("A Render callback can't be used with screens; use one or the other.");
        _onRender = render;
        return this;
    }

    /// <summary>
    /// The app's screens (§9.3). An app either pushes screens or sets a <see cref="Render"/> callback.
    /// With screens, the app draws the top screen, a status bar (see <see cref="WithChrome"/>), overlays and toasts.
    /// </summary>
    public ScreenStack Screens { get; }

    /// <summary>Modal overlays and dialogs, drawn over the screens (or over the <see cref="Render"/> output).</summary>
    public OverlayStack Overlays { get; }

    /// <summary>Toasts the app draws in the top-right corner, over everything else.</summary>
    public ToastQueue Toasts { get; } = new();

    /// <summary>
    /// Global key bindings, tried after the focus chain when screens are used (§9.4).
    /// Built in: <c>?</c> shows help and <c>ctrl+c</c> quits.
    /// </summary>
    public KeyMap Keys { get; }

    /// <summary>
    /// With screens, reserve the bottom row for a status bar showing key hints on the left and, if
    /// <paramref name="breadcrumb"/> is set, the screen titles on the right. On by default.
    /// </summary>
    public App WithChrome(bool statusBar = true, bool breadcrumb = true)
    {
        _chromeStatusBar = statusBar;
        _chromeBreadcrumb = breadcrumb;
        return this;
    }

    /// <summary>
    /// Show <paramref name="menuBar"/> on the top row, above the screens (1.x spec §15.5): F10 opens its first menu,
    /// Alt and a title's mnemonic opens that one. Null removes it.
    /// </summary>
    public App WithMenuBar(MenuBar? menuBar)
    {
        MenuBar?.Attach(null);
        MenuBar = menuBar;
        menuBar?.Attach(this);
        if (menuBar is not null && !_menuKey)
        {
            Keys.Add("f10", "Menu", () => MenuBar?.Open(), showInHints: false);
            _menuKey = true;
        }
        RequestRedraw();
        return this;
    }

    /// <summary>The menu bar, or null.</summary>
    public MenuBar? MenuBar { get; private set; }

    private bool _menuKey;

    /// <summary>Quit when the last screen is popped. On by default.</summary>
    public App QuitWhenEmpty(bool quit = true)
    {
        _quitWhenEmpty = quit;
        return this;
    }

    /// <summary>
    /// Configure update logic that runs on every loop iteration, before rendering.
    /// </summary>
    public App OnUpdate(Action update)
    {
        _onUpdate = update;
        return this;
    }

    /// <summary>
    /// Handle a key before anything else sees it (§9.4 step 1). Return true if the key was handled, so routing stops.
    /// </summary>
    public App OnKey(Func<ConsoleKeyInfo, bool> handler)
    {
        _onKey = handler;
        return this;
    }

    /// <summary>
    /// Handle a paste before the focus chain sees it. Return true if it was handled, so routing stops. Pastes
    /// arrive from <see cref="TerminalInputSource"/>; with <see cref="ConsoleInputSource"/>, pasted text arrives
    /// as keys.
    /// </summary>
    public App OnPaste(Func<string, bool> handler)
    {
        _onPaste = handler;
        return this;
    }

    /// <summary>
    /// Ask the terminal to report mouse presses, releases and the wheel while the app runs (1.x spec §4.6). Off by
    /// default: while the app captures the mouse, selecting text in the terminal needs Shift+drag (Option+drag
    /// in iTerm2). Needs <see cref="TerminalInputSource"/>, the default input.
    /// </summary>
    public App WithMouse(bool enabled = true)
    {
        _mouse = enabled;
        return this;
    }

    /// <summary>
    /// Read the system clipboard through the terminal (an OSC 52 query) when Ctrl+V or Shift+Insert pastes on Linux and
    /// macOS (1.x spec §20.3). Off by default: many terminals refuse, some ask the user each time, and one that
    /// doesn't answer costs a 500 ms wait, after which the app stops asking. Windows reads its clipboard anyway.
    /// </summary>
    public App WithClipboardRead(bool enabled = true)
    {
        _clipboardRead = enabled;
        return this;
    }

    /// <summary>
    /// Ask the terminal to report every pointer move too (all-motion tracking, 1.x spec §17.5), so components can
    /// show what's under the pointer through <see cref="RenderContext.Hover"/>, and menus follow it. Turns
    /// <see cref="WithMouse"/> on as well. Off by default: each move is an event, and often a frame.
    /// </summary>
    public App WithHover(bool enabled = true)
    {
        _hover = enabled;
        if (enabled) _mouse = true;
        return this;
    }

    /// <summary>Where the pointer was last reported (by any mouse event), or null before the first report.</summary>
    public Vec2? PointerPosition => _pointer is { } p ? new Vec2(p.X, p.Y) : null;

    /// <summary>True when <see cref="WithHover"/> is on.</summary>
    internal bool HoverEnabled => _hover;

    /// <summary>
    /// Make an unhandled Ctrl+Z call <see cref="SuspendProcess"/>, so it returns to the shell as in most terminal
    /// programs (1.x spec §5.5). Off by default. A component or key binding for <c>ctrl+z</c> still gets it first,
    /// and an open overlay swallows it. Has no effect on Windows, which has no job control.
    /// </summary>
    public App WithSuspendKey(bool enabled = true)
    {
        _suspendKey = enabled;
        return this;
    }

    /// <summary>
    /// Handle a mouse event before the components see it. Return true if it was handled, so routing stops. In
    /// an app with a <see cref="Render"/> callback, this is the only place mouse events go.
    /// </summary>
    public App OnMouse(Func<MouseEvent, bool> handler)
    {
        _onMouse = handler;
        return this;
    }

    /// <summary>
    /// Observe every event (keys, pastes, mouse events, resizes, resumes and ticks) before it's handled.
    /// </summary>
    public App OnEvent(Action<Event> handler)
    {
        _onEvent = handler;
        return this;
    }

    /// <summary>
    /// Decide what happens when a callback, posted action or <see cref="RunTask"/> throws.
    /// Without a handler, the terminal is restored and the exception is rethrown from <see cref="Run"/>.
    /// </summary>
    public App OnError(Func<Exception, ErrorAction> handler)
    {
        _onError = handler;
        return this;
    }

    /// <summary>
    /// How often a <see cref="TickEvent"/> is delivered, which also redraws. The default is 250 ms.
    /// Null means no ticks: the app only redraws after input, posted actions and redraw requests.
    /// </summary>
    public App WithTickRate(TimeSpan? rate)
    {
        if (rate is { } value) ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero, nameof(rate));
        _tickRate = rate;
        return this;
    }

    /// <summary>
    /// The longest time between the presses of a double- or triple-click (1.x spec §17.3). 500 ms by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is outside 100 ms to 2 s.</exception>
    public App WithDoubleClickTime(TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.FromMilliseconds(100));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(interval, TimeSpan.FromSeconds(2));
        _doubleClickTime = interval;
        return this;
    }

    /// <summary>
    /// The clock for ticks, <see cref="Loadable{T}.LoadedAt"/> and log timestamps. Tests can pass a fake one.
    /// </summary>
    public App WithTimeProvider(TimeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _time = provider;
        Log.Time = provider;
        return this;
    }

    /// <summary>
    /// Draw through a specific backend, such as <see cref="TestBackend"/>, instead of the console.
    /// </summary>
    public App WithBackend(ITerminalBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        _backend = backend;
        return this;
    }

    /// <summary>
    /// Read keys from a specific source, such as <see cref="ScriptedInputSource"/>, instead of the console.
    /// The app doesn't dispose it.
    /// </summary>
    public App WithInput(IInputSource input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = input;
        return this;
    }

    /// <summary>
    /// Override the detected color mode of the default console backend.
    /// </summary>
    public App WithColorMode(ColorMode mode)
    {
        _colorMode = mode;
        return this;
    }

    /// <summary>
    /// The theme widgets draw with while the app runs (<see cref="Theme.Current"/>). Without one, the app uses
    /// <see cref="Theme.Default"/>. When the terminal has no colors (<c>NO_COLOR</c>), <see cref="Theme.Default"/>
    /// is replaced by <see cref="Theme.Monochrome"/> so the selection stays visible; custom themes are used as is.
    /// Can be called while running, from any thread; the next frame uses the new theme.
    /// </summary>
    public App WithTheme(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _theme = theme;
        _themePair = null;
        if (_running) InvokeOnUiThread(() =>
        {
            _activeTheme = ResolveTheme(theme);
            Theme.ReplaceCurrent(_activeTheme);
        });
        return this;
    }

    /// <summary>
    /// While running, redirect <see cref="Console.Out"/> and <see cref="Console.Error"/> into <see cref="Log"/>
    /// so stray writes can't corrupt the screen. On by default. Captured error lines are written to stderr on exit.
    /// </summary>
    public App CaptureConsoleOutput(bool capture = true)
    {
        _captureConsole = capture;
        return this;
    }

    /// <summary>
    /// Lines written to the console while the app runs (see <see cref="CaptureConsoleOutput"/>).
    /// </summary>
    public LogBuffer Log { get; } = new();

    /// <summary>
    /// Cancelled when <see cref="Quit"/> is called or the loop exits. Pass it to background work.
    /// </summary>
    public CancellationToken Stopping => _stopping.Token;

    /// <summary>
    /// The terminal size in cells, or (0, 0) when the app isn't running.
    /// </summary>
    public (int Width, int Height) Size => _terminal is { } terminal ? (terminal.Width, terminal.Height) : (0, 0);

    /// <summary>
    /// Run <paramref name="action"/> on the UI thread. Thread-safe. If it throws, <see cref="OnError"/> decides what happens.
    /// </summary>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _queue.Writer.TryWrite(action);
    }

    /// <summary>
    /// Start async work on the UI thread, so its continuations resume there. The token is <see cref="Stopping"/>.
    /// Exceptions go to <see cref="OnError"/>; cancellation after <see cref="Quit"/> is ignored.
    /// </summary>
    /// <returns>A task that completes when the work finishes. It never faults.</returns>
    public Task RunTask(Func<CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginWork();
        InvokeOnUiThread(() =>
        {
            // Run under the app's context even if the caller's thread has another one, so awaits resume on the UI thread
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new MiniTuiSynchronizationContext(this));
            try
            {
                _ = RunTaskCore(work, completion);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        });
        return completion.Task;
    }

    /// <summary>
    /// Draw a frame soon, even if nothing else happened. Thread-safe.
    /// </summary>
    public void RequestRedraw() => _queue.Writer.TryWrite(RedrawRequest.Instance);

    /// <summary>
    /// Stop the loop after the current item. Thread-safe.
    /// </summary>
    public void Quit()
    {
        _running = false;
        CancelStopping();
        _queue.Writer.TryWrite(Wake.Instance);
    }

    /// <summary>
    /// Force a full screen redraw on the next frame.
    /// </summary>
    public void Invalidate() => _terminal?.Invalidate();

    /// <summary>
    /// Run the application on this thread. Blocks until <see cref="Quit"/> is called.
    /// </summary>
    public void Run() => RunCore(CancellationToken.None);

    /// <summary>
    /// Run the application on a new UI thread. Cancelling <paramref name="cancellationToken"/> quits it.
    /// </summary>
    /// <returns>A task that completes when the app exits, or faults with the exception <see cref="Run"/> would throw.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "The exception is handed to the returned task.")]
    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                RunCore(cancellationToken);
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }) { Name = "MiniTui UI" };
        thread.Start();
        return completion.Task;
    }

    internal bool HasRenderCallback => _onRender is not null;

    /// <summary>Async loads and tasks in flight.</summary>
    internal int PendingWork => Volatile.Read(ref _pendingWork);

    /// <summary>Dialogs waiting for the user, which tasks in flight may be awaiting.</summary>
    internal int AwaitingUser => Volatile.Read(ref _awaitingUser);

    internal void BeginWork() => Interlocked.Increment(ref _pendingWork);

    internal void EndWork() => Interlocked.Decrement(ref _pendingWork);

    internal void BeginUserWait() => Interlocked.Increment(ref _awaitingUser);

    internal void EndUserWait() => Interlocked.Decrement(ref _awaitingUser);

    /// <summary>Run now on the UI thread (or before the app runs), otherwise post.</summary>
    internal void OnUiThread(Action action)
    {
        if (_running && !IsUiThread) Post(action);
        else action();
    }

    internal void OnScreensEmpty()
    {
        if (_quitWhenEmpty && _running) Quit();
    }

    /// <summary>The clock the app runs on.</summary>
    internal TimeProvider Time => _time;

    /// <summary>True on the thread running this app's loop.</summary>
    internal bool IsUiThread => _running && Environment.CurrentManagedThreadId == _uiThreadId;

    /// <summary>Run inline on the UI thread, otherwise post.</summary>
    internal void InvokeOnUiThread(Action action)
    {
        if (IsUiThread) action();
        else Post(action);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Every task failure goes to OnError.")]
    private async Task RunTaskCore(Func<CancellationToken, Task> work, TaskCompletionSource completion)
    {
        var stopping = Stopping;
        try
        {
            await work(stopping);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
        finally
        {
            EndWork();
            completion.TrySetResult();
        }
    }

    private void CancelStopping()
    {
        try
        {
            _stopping.Cancel();
        }
        catch (AggregateException ex)
        {
            // A Stopping callback threw; report it like any other callback failure
            if (IsUiThread) HandleError(ex);
        }
    }

    /// <summary>Asks the loop to redraw.</summary>
    private sealed class RedrawRequest
    {
        public static readonly RedrawRequest Instance = new();
    }

    /// <summary>Wakes the loop without doing anything else, e.g. to notice <see cref="Quit"/> or a resize signal.</summary>
    private sealed class Wake
    {
        public static readonly Wake Instance = new();
    }

    /// <summary>Keys and pastes read in one burst by the input thread.</summary>
    private sealed record KeyBatch(InputItem[] Keys);
}
