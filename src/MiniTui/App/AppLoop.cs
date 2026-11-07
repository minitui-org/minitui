using System.Runtime.ExceptionServices;

namespace MiniTui;

// The event loop (§6.3).
public partial class App
{
    /// <summary>Items handled per iteration before the next frame is drawn.</summary>
    private const int MaxItemsPerIteration = 256;

    /// <summary>Windows has no resize signal, so the loop wakes this often to check the size.</summary>
    private const int WindowsResizePollMs = 100;

    // The app running on this thread, for Input.Poll. Nested apps restore the outer one on exit.
    [ThreadStatic] private static App? t_current;

    private volatile bool _running;
    private IInputSource? _activeInput;
    private int _uiThreadId;
    private Terminal? _terminal;
    private ExceptionDispatchInfo? _failure;
    private Task<bool>? _pendingWait;

    // Keys and pastes from a batch that didn't fit in the previous iteration's budget
    private InputItem[]? _pendingKeys;
    private int _pendingIndex;

    // Mouse hit targets: those being recorded by the frame in progress, and those of the last frame drawn
    private List<MouseRouter.Hit> _hits = [];

    // Where the menu bar was drawn in the last frame, for the mouse
    private Rect _menuBarRow;

    /// <summary>The menu bar's row from the last frame, or empty.</summary>
    internal Rect MenuBarRow => _menuBarRow;
    private List<MouseRouter.Hit> _lastHits = [];
    private readonly HashSet<Component> _hoverChain = new(ReferenceEqualityComparer.Instance);
    private Component? _hoverTarget;
    private bool _pointerRead;
    private bool _pointerDrawn;
    private Rect _lastFrame;

    private int _tickPending;
    private long _lastTick;
    private ITimer? _ticker;
    private TimeSpan? _tickerPeriod;

    // The fastest tick rate a widget asked for while drawing the current frame (toasts, spinners)
    private TimeSpan? _frameTickRequest;
    private RunScope? _run;
    private Theme? _activeTheme;
    private bool _noColor;
    private bool _basicGlyphs;
    private Clipboard.Target? _clipboard;

    /// <summary>The component the last mouse press hit, which gets the drags after it (1.x spec §10.7).</summary>
    internal Component? PressTarget { get; set; }

    /// <summary>The system clipboard to use instead of the backend's; set by tests.</summary>
    internal Clipboard.Target? ClipboardTarget { get; set; }

    /// <summary>The app whose loop is running on the current thread.</summary>
    internal static App? Current => t_current;

    private void RunCore(CancellationToken cancellationToken)
    {
        StartRun(cancellationToken);
        try
        {
            using (EnterUi())
            {
                Setup();
                while (Step(wait: true)) { }
            }
        }
        finally
        {
            EndRun();
        }

        _failure?.Throw();
    }

    /// <summary>
    /// Enter the terminal and start input, ticks and console capture. Runs no user code. Pair with <see cref="EndRun"/>;
    /// user code runs inside <see cref="EnterUi"/>.
    /// </summary>
    internal void StartRun(CancellationToken cancellationToken)
    {
        if (_onRender is null && Screens.Count == 0 && _onSetup is null)
            throw new InvalidOperationException("Render callback is required. Call Render() or push a screen before Run().");
        if (_running) throw new InvalidOperationException("The app is already running.");

        if (_stopping.IsCancellationRequested) _stopping = new CancellationTokenSource();
        _failure = null;
        _ended = false;
        _uiThreadId = Environment.CurrentManagedThreadId;
        _running = true;

        var input = _input ?? InputPump.DefaultFor(_backend);
        var run = new RunScope(input, t_current);
        _run = run;
        try
        {
            var backend = _backend ?? Terminal.CreateDefaultBackend(_colorMode);
            run.Terminal = new Terminal(backend);
            _terminal = run.Terminal;
            _activeColorMode = ColorModeOf(backend);
            _noColor = _activeColorMode == ColorMode.None;
            StartTerminalInfo();
            _activeTheme = ResolveTheme(StartTheme());
            _basicGlyphs = Glyphs.DetectBasic(backend);
            _clipboard = ClipboardTarget ?? Clipboard.For(backend);
            _clipboardTimedOut = false;
            if (_clipboardRead && !OperatingSystem.IsWindows() && backend is AnsiBackend ansi) UseTerminalClipboard(ansi.WriteClipboardQuery);
            StartDebug();
            if (_captureConsole) run.Capture = ConsoleCapture.Start(Log);

            run.Subscription = InputPump.Subscribe(input, keys => _queue.Writer.TryWrite(new KeyBatch(keys)));
            _activeInput = input;
            if (_mouse && input is IMouseCapture capture)
            {
                capture.SetMouseCapture(true);
                if (_hover) capture.SetHoverCapture(true);
                run.MouseCapture = capture;
                run.HoverCapture = _hover;
            }

            // An app started from inside another one on the same input gets the keys the outer one hasn't handled yet
            var outer = run.PreviousApp is { _running: true } app && ReferenceEquals(app._activeInput, input) ? app : null;
            if (outer?.RemoveQueuedKeys() is { Count: > 0 } inherited) _pendingKeys = [.. inherited];
            StartKeyboard();
            SendQueries();
            StartTicker();
            run.Cancellation = cancellationToken.Register(Quit);
            TerminalSession.Resized += OnResizeSignal;
            run.ResizeHooked = true;
            if (IsConsole(run.Terminal.Backend))
                run.JobControl = TerminalSession.ListenForJobControl(OnStopSignal, OnContinueSignal);
            if (ConsoleOverride ?? IsConsole(run.Terminal.Backend))
            {
                AddConsoleApp();
                run.ConsoleListed = true;
            }
        }
        catch
        {
            EndRun();
            throw;
        }
    }

    /// <summary>Run <c>OnSetup</c>. Call inside <see cref="EnterUi"/>.</summary>
    internal void Setup()
    {
        Guard(() => _onSetup?.Invoke(this));
        if (_onRender is null && Screens.Count == 0 && _failure is null)
            throw new InvalidOperationException("Render callback is required. Call Render() or push a screen before Run().");
    }

    /// <summary>
    /// Make the calling thread the UI thread until disposed: <see cref="Current"/>, the theme, glyphs and the
    /// synchronization context. Restores the previous values, so it nests.
    /// </summary>
    internal UiBinding EnterUi() => new(this);

    /// <summary>
    /// One loop iteration: optionally wait for work, handle resizes and up to 256 queued items, then update and
    /// draw if anything happened. Call inside <see cref="EnterUi"/>.
    /// </summary>
    /// <returns>True while the app is still running.</returns>
    internal bool Step(bool wait)
    {
        if (!_running || _run is not { Terminal: { } terminal } run) return false;
        if (_ended) return WaitToEnd();
        if (!run.FirstFrame && wait) WaitForWork();
        else if (wait && HoldFirstFrame(out var hold)) WaitForQueue(hold);
        var dirty = run.FirstFrame;

        // 1. Resize
        if (terminal.CheckResize())
        {
            dirty = true;
            var resize = new ResizeEvent(terminal.Width, terminal.Height);
            Guard(() => _onEvent?.Invoke(resize));
        }

        // 2. Drain the queue in FIFO order
        for (var budget = MaxItemsPerIteration; budget > 0 && _running && !_ended && TryTake(out var item); budget--)
        {
            switch (item)
            {
                case InputItem { Mouse: { Action: MouseAction.Move } move }:
                    // 1.x spec §17.5: only the last of the moves waiting is routed, and it draws only if it changes something
                    if (!_hover || NextIsMove()) break;
                    Guard(() => dirty |= HandleMove(move));
                    break;
                case InputItem { Mouse: { } mouse }:
                    dirty = true;
                    Guard(() => HandleMouse(mouse));
                    break;
                case InputItem { Paste: { } text }:
                    dirty = true;
                    Guard(() => HandlePaste(text));
                    break;
                case InputItem { Reply: { } reply }:
                    dirty = true;
                    Guard(() => HandleReply(reply));
                    break;
                case InputItem input:
                    dirty = true;
                    Guard(() => HandleKey(input.Key));
                    break;
                case Action action:
                    dirty = true;
                    Guard(action);
                    break;
                case TickEvent tick:
                    Interlocked.Exchange(ref _tickPending, 0);
                    dirty = true;
                    Guard(() => _onEvent?.Invoke(tick));
                    break;
                case RedrawRequest:
                    dirty = true;
                    break;
                case Ending ending:
                    RunEnding(ending);
                    break;
            }
        }

        if (!_running) return false;
        if (_ended) return WaitToEnd();

        // With a theme pair, the first frame waits briefly for the terminal's background (1.x spec §12.3)
        if (run.FirstFrame && HoldFirstFrame(out _)) return true;

        // 3–4. Update, render
        Guard(() => _onUpdate?.Invoke());
        if (!_running) return false;
        if (dirty)
        {
            _frameTickRequest = null;
            Guard(() =>
            {
                var start = _time.GetTimestamp();
                var rendered = start;
                var render = TimeSpan.Zero;
                terminal.Draw(buf =>
                {
                    render = RenderFrame(buf, new Rect(0, 0, buf.Width, buf.Height));
                    rendered = _time.GetTimestamp();
                });
                RecordFrame(terminal, render, _time.GetElapsedTime(rendered));
            });
            UpdateTicker();
        }
        run.FirstFrame = false;
        return _running;
    }

    /// <summary>Leave the terminal and stop input, ticks and capture. Idempotent.</summary>
    internal void EndRun()
    {
        if (_run is not { } run) return;
        _run = null;
        _running = false;
        StopTicker();
        CancelStopping();
        if (run.ResizeHooked) TerminalSession.Resized -= OnResizeSignal;
        run.JobControl?.Dispose();
        if (run.ConsoleListed) RemoveConsoleApp();
        run.Cancellation.Dispose();
        if (run.HoverCapture) run.MouseCapture?.SetHoverCapture(false);
        run.MouseCapture?.SetMouseCapture(false);
        run.Subscription?.Dispose();

        // Restore the writers before leaving the terminal, and replay stderr after, so it lands on the real screen
        run.Capture?.Restore();
        run.Terminal?.Dispose();
        _terminal = null;
        run.Capture?.ReplayErrors();

        // Keys this app didn't get to go back to the outer app, ahead of anything it received since
        if (run.PreviousApp is { _running: true } outerApp && ReferenceEquals(outerApp._activeInput, run.Input))
            outerApp.ReturnKeys(RemoveQueuedKeys());
        _activeInput = null;
        DiscardQueue();
    }

    /// <summary>True while a run is started and hasn't stopped.</summary>
    internal bool IsRunning => _running;

    /// <summary>Take the exception a run stopped with, if any.</summary>
    internal ExceptionDispatchInfo? TakeFailure()
    {
        var failure = _failure;
        _failure = null;
        return failure;
    }

    /// <summary>True when keys or other items are waiting to be handled.</summary>
    internal bool HasQueuedWork => _pendingKeys is not null || _queue.Reader.TryPeek(out _);

    /// <summary>Queue keys as if the input source had read them.</summary>
    internal void InjectKeys(ConsoleKeyInfo[] keys) => _queue.Writer.TryWrite(new KeyBatch([.. keys.Select(InputItem.FromKey)]));

    /// <summary>Queue a paste as if the input source had read it.</summary>
    internal void InjectPaste(string text) => _queue.Writer.TryWrite(new KeyBatch([InputItem.FromPaste(text)]));

    internal void InjectMouse(MouseEvent mouse) => _queue.Writer.TryWrite(new KeyBatch([InputItem.FromMouse(mouse)]));

    /// <summary>Wait up to <paramref name="timeout"/> for something to be queued.</summary>
    internal void WaitForQueue(TimeSpan timeout)
    {
        if (HasQueuedWork) return;
        _pendingWait ??= _queue.Reader.WaitToReadAsync().AsTask();
        if (_pendingWait.Wait(timeout)) _pendingWait = null;
    }

    /// <summary>What a run started and must undo.</summary>
    private sealed class RunScope(IInputSource input, App? previousApp)
    {
        public IInputSource Input { get; } = input;
        public App? PreviousApp { get; } = previousApp;
        public Terminal? Terminal { get; set; }
        public ConsoleCapture? Capture { get; set; }
        public IDisposable? Subscription { get; set; }
        public CancellationTokenRegistration Cancellation { get; set; }
        public bool ResizeHooked { get; set; }
        public IMouseCapture? MouseCapture { get; set; }
        public bool HoverCapture { get; set; }
        public IDisposable? JobControl { get; set; }
        public bool ConsoleListed { get; set; }
        public bool FirstFrame { get; set; } = true;
    }

    /// <summary>The thread-local state of the UI thread, restored on dispose.</summary>
    internal readonly struct UiBinding : IDisposable
    {
        private readonly App? _previousApp;
        private readonly SynchronizationContext? _previousContext;
        private readonly Theme.Scope _theme;
        private readonly Glyphs.Scope _glyphs;
        private readonly Clipboard.Scope _clipboard;

        public UiBinding(App app)
        {
            app._uiThreadId = Environment.CurrentManagedThreadId;
            _previousApp = t_current;
            _previousContext = SynchronizationContext.Current;
            t_current = app;
            _theme = Theme.Use(app._activeTheme ?? app._theme ?? Theme.Default);
            _glyphs = Glyphs.Use(app._basicGlyphs);
            _clipboard = Clipboard.Use(app._clipboard);
            SynchronizationContext.SetSynchronizationContext(new MiniTuiSynchronizationContext(app));
        }

        public void Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(_previousContext);
            _clipboard.Dispose();
            _glyphs.Dispose();
            _theme.Dispose();
            t_current = _previousApp;
        }
    }

    // §4.7 of the 1.x spec: OnEvent, OnMouse, then the component under the pointer. Unhandled events are dropped.
    private void HandleMouse(MouseEvent mouse)
    {
        // 1.x spec §10.7: a drag past the screen's edge stays at the edge, so a selection can reach it
        if (mouse.Action == MouseAction.Drag && !_lastFrame.IsEmpty)
            mouse = mouse with { X = Math.Clamp(mouse.X, _lastFrame.X, _lastFrame.Right - 1), Y = Math.Clamp(mouse.Y, _lastFrame.Y, _lastFrame.Bottom - 1) };
        _pointer = (mouse.X, mouse.Y);
        if (mouse.Action == MouseAction.Press) mouse = mouse with { ClickCount = CountClick(mouse) };
        _onEvent?.Invoke(mouse);
        if (_onMouse?.Invoke(mouse) == true) return;
        if (_onRender is not null) return;
        MouseRouter.Route(this, mouse, _lastHits, _lastFrame);
    }

    // 1.x spec §17.3: the same button in the same cell within the double-click time counts on, up to 3
    private int CountClick(MouseEvent press)
    {
        var now = _time.GetTimestamp();
        var count = _lastPress is { } last && last.Button == press.Button && last.X == press.X && last.Y == press.Y
            && last.Count < 3 && _time.GetElapsedTime(last.Time, now) <= _doubleClickTime
            ? last.Count + 1
            : 1;
        _lastPress = (press.Button, press.X, press.Y, now, count);
        return count;
    }

    // A move: OnEvent, OnMouse, then the component under the pointer and its ancestors, without click to focus.
    // A redraw is needed when the hovered component changes, something handled the move, or the hovered components
    // drew with the pointer last frame (a row under it may change).
    private bool HandleMove(MouseEvent move)
    {
        var before = HoverTarget();
        _pointer = (move.X, move.Y);
        _onEvent?.Invoke(move);
        var handled = _onMouse?.Invoke(move) == true;
        if (!handled && _onRender is null) handled = MouseRouter.Route(this, move, _lastHits, _lastFrame);
        return handled || _pointerDrawn || !ReferenceEquals(before, HoverTarget()) || _onRender is not null;
    }

    private bool NextIsMove()
    {
        if (_pendingKeys is { } keys && _pendingIndex < keys.Length) return keys[_pendingIndex] is { Mouse.Action: MouseAction.Move };
        return _queue.Reader.TryPeek(out var next) && next is KeyBatch { Keys: [{ Mouse.Action: MouseAction.Move }, ..] };
    }

    /// <summary>The component under the pointer in the last frame, as a click would find it (1.x spec §17.6).</summary>
    internal Component? HoverTarget()
    {
        if (!_hover || _pointer is not { } p || !_lastFrame.Contains(p.X, p.Y)) return null;
        Component? root = Overlays.Top is { } overlay ? overlay : Screens.Top;
        return root is null ? null : MouseRouter.Target(_lastHits, root, p.X, p.Y);
    }

    /// <summary>The pointer for <paramref name="component"/>'s render context: set for the hovered component and its ancestors.</summary>
    internal Vec2? PointerFor(Component? component)
    {
        if (_hoverTarget is null || _pointer is not { } p) return null;
        if (component is not null && !_hoverChain.Contains(component)) return null;
        _pointerRead = true;
        return new Vec2(p.X, p.Y);
    }

    /// <summary>The pointer for the menu bar, when hover is on and it's on the bar's row.</summary>
    internal Vec2? BarPointer(Rect row)
    {
        if (!_hover || _pointer is not { } p || !row.Contains(p.X, p.Y)) return null;
        _pointerRead = true;
        return new Vec2(p.X, p.Y);
    }

    // 1.x spec §20.3: one query per read, answered by the OSC 52 reply; 500 ms without one stops the asking
    private static readonly TimeSpan ClipboardWait = TimeSpan.FromMilliseconds(500);
    private readonly List<TaskCompletionSource<string?>> _clipboardReads = [];
    private bool _clipboardTimedOut;

    /// <summary>Read the clipboard by writing a query with <paramref name="writeQuery"/> and waiting for the reply.</summary>
    internal void UseTerminalClipboard(Func<bool> writeQuery) =>
        _clipboard = (_clipboard ?? new Clipboard.Target(null, null)) with { ReadAsync = ct => ReadTerminalClipboardAsync(writeQuery, ct) };

    private async Task<string?> ReadTerminalClipboardAsync(Func<bool> writeQuery, CancellationToken ct)
    {
        if (_clipboardTimedOut) return null;
        var reply = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _clipboardReads.Add(reply);
        if (!writeQuery())
        {
            _clipboardReads.Remove(reply);
            return null;
        }
        var winner = await Task.WhenAny(reply.Task, Task.Delay(ClipboardWait, _time, ct)).ConfigureAwait(true);
        if (winner != reply.Task)
        {
            _clipboardReads.Remove(reply);
            ct.ThrowIfCancellationRequested();
            _clipboardTimedOut = true;
            return null;
        }
        return await reply.Task.ConfigureAwait(true);
    }

    // The oldest read gets the reply; an empty one (a refusal) reads as nothing
    private void AnswerClipboard(string text)
    {
        if (_clipboardReads.Count == 0) return;
        var read = _clipboardReads[0];
        _clipboardReads.RemoveAt(0);
        read.TrySetResult(text.Length > 0 ? text : null);
    }

    /// <summary>Record a hit target for this frame (1.x spec §4.7) and set its <see cref="Component.Area"/>.</summary>
    internal void RecordHit(Component component, Rect hit, Rect area)
    {
        component.Area = area;
        _hits.Add(new MouseRouter.Hit(component, hit));
    }

    // §3.4 of the 1.x spec: OnEvent, OnPaste, then the focus chain. Unhandled pastes are dropped.
    private void HandlePaste(string text)
    {
        _onEvent?.Invoke(new PasteEvent(text));
        if (_onPaste?.Invoke(text) == true) return;
        KeyRouter.RoutePaste(this, text);
    }

    private void HandleKey(ConsoleKeyInfo key)
    {
        _onEvent?.Invoke(new KeyEvent(key));
        if (_onKey?.Invoke(key) == true) return;
        if (HandleDebugKey(key)) return;

        // Ctrl+C asks to quit unless OnKey took it, or the focused text component, or one with a selection, copied with
        // it (1.x spec §10.4, §20.5, §25.3)
        if (KeyInput.IsCtrlC(key))
        {
            if (KeyRouter.Chain(this) is [.., var focused] && (focused.CapturesText || focused.HasSelection) && focused.HandleKey(key)) return;
            RequestQuit();
            return;
        }

        if (KeyRouter.Route(this, key)) return;

        // 1.x spec §5.5: Ctrl+Z suspends only if nothing else took it
        if (_suspendKey && KeyInput.IsCtrlZ(key)) SuspendProcess();
    }

    /// <summary>
    /// Draw a frame: the <see cref="Render"/> callback, or the top screen and the chrome (§9.3); then overlays,
    /// bottom to top, and toasts.
    /// </summary>
    /// <returns>The time taken, without the debug overlay.</returns>
    private TimeSpan RenderFrame(CellBuffer buf, Rect area)
    {
        _hits.Clear();
        _hoverTarget = HoverTarget();
        _hoverChain.Clear();
        for (var c = _hoverTarget; c is not null && _hoverChain.Count < 64; c = c.Parent) _hoverChain.Add(c);
        _pointerRead = false;
        try
        {
            var start = _time.GetTimestamp();

            // A theme with its own background (Theme.Dark, Theme.Light) fills the screen with it (1.x spec §12.4)
            if (Theme.Current.Text.Bg is not null) WidgetText.Clear(buf, area);
            RenderLayers(buf, area);
            var render = _time.GetElapsedTime(start);
            if (_showDebug) DrawDebug(buf, area, render);
            return render;
        }
        finally
        {
            (_lastHits, _hits) = (_hits, _lastHits);
            _lastFrame = area;
            _pointerDrawn = _pointerRead;
        }
    }

    private void RenderLayers(CellBuffer buf, Rect area)
    {
        var body = area;
        if (_onRender is not null)
        {
            _onRender(buf, area);
        }
        else
        {
            var chrome = _chromeStatusBar && area.Height >= 2;
            if (chrome) body = area with { Height = area.Height - 1 };

            // 1.x spec §15.5: the menu bar takes the top row; the screens and overlays get the rows below it
            if (MenuBar is { } bar && body.Height >= 2)
            {
                _menuBarRow = body with { Height = 1 };
                body = body with { Y = body.Y + 1, Height = body.Height - 1 };
                bar.Draw(buf, _menuBarRow);
            }
            else
            {
                _menuBarRow = default;
            }
            if (Screens.Top is { } top)
            {
                RecordHit(top, body, body);
                top.Render(buf, body, new RenderContext(this, top, focused: !Overlays.Any));
            }
            if (chrome) DrawStatusBar(buf, new Rect(area.X, area.Bottom - 1, area.Width, 1));
        }

        var theme = Theme.Current;
        foreach (var overlay in Overlays.All)
        {
            var rect = overlay.Placement(body);
            WidgetText.Clear(buf, rect);
            if (!overlay.Framed)
            {
                RecordHit(overlay, rect, rect);
                overlay.Render(buf, rect, new RenderContext(this, overlay, focused: ReferenceEquals(overlay, Overlays.Top)));
                continue;
            }
            var inner = Widget.Block()
                // 1.x spec §22.3: a space each side, so " Keys " and "Keys" look the same
                .Title(overlay.Title?.Trim() is { Length: > 0 } title ? new Line($" {title} ") : null)
                .BorderStyle(theme.ModalBorder)
                .TitleStyle(theme.ModalTitle)
                .Padding(new Margin(1, 0))
                .Draw(buf, rect);
            RecordHit(overlay, rect, inner);
            overlay.Render(buf, inner, new RenderContext(this, overlay, focused: ReferenceEquals(overlay, Overlays.Top)));
        }

        // 1.x spec §22.3: below the menu bar, not over it
        var toasts = _menuBarRow.IsEmpty ? area : area with { Y = _menuBarRow.Bottom, Height = area.Bottom - _menuBarRow.Bottom };
        Widget.Toasts().Queue(Toasts).Draw(buf, toasts);
    }

    // Hints on the left; the breadcrumb (screen titles, truncated from the left) on the right. 1.x spec §22.3: the
    // hints get the room first, and the breadcrumb what's left
    private void DrawStatusBar(CellBuffer buf, Rect row)
    {
        var hints = KeyRouter.Hints(this, row.Width - 2);
        var crumb = _chromeBreadcrumb ? string.Join(" › ", Screens.All.Select(s => s.Title)) : "";
        var room = row.Width - 2 - UnicodeWidth.Of(hints) - 1;
        crumb = room > 0 ? WidgetText.TruncateStart(crumb, room) : "";
        Widget.StatusBar().Left(hints).Right(crumb).Draw(buf, row);
    }

    private void WaitForWork()
    {
        if (_pendingKeys is not null) return;
        _pendingWait ??= _queue.Reader.WaitToReadAsync().AsTask();
        if (_pendingWait.Wait(OperatingSystem.IsWindows() ? WindowsResizePollMs : Timeout.Infinite))
            _pendingWait = null;
    }

    private bool TryTake(out object? item)
    {
        while (true)
        {
            if (_pendingKeys is { } keys)
            {
                item = keys[_pendingIndex++];
                if (_pendingIndex == keys.Length)
                {
                    _pendingKeys = null;
                    _pendingIndex = 0;
                }
                return true;
            }

            if (!_queue.Reader.TryRead(out item)) return false;
            if (item is not KeyBatch batch) return true;
            if (batch.Keys.Length > 0) _pendingKeys = batch.Keys;
        }
    }

    /// <summary>Take this app's unhandled keys and pastes out of its queue, keeping everything else queued.</summary>
    private List<InputItem> RemoveQueuedKeys()
    {
        var keys = new List<InputItem>();
        if (_pendingKeys is { } pending) keys.AddRange(pending.AsSpan(_pendingIndex));
        _pendingKeys = null;
        _pendingIndex = 0;

        var others = new List<object>();
        while (_queue.Reader.TryRead(out var item))
        {
            if (item is KeyBatch batch) keys.AddRange(batch.Keys);
            else others.Add(item);
        }
        foreach (var item in others) _queue.Writer.TryWrite(item);
        return keys;
    }

    /// <summary>Put keys and pastes back so they're handled before any this app already has queued.</summary>
    private void ReturnKeys(List<InputItem> keys)
    {
        if (keys.Count == 0) return;
        if (_pendingKeys is { } pending) keys.AddRange(pending.AsSpan(_pendingIndex));
        _pendingKeys = [.. keys];
        _pendingIndex = 0;
    }

    private void DiscardQueue()
    {
        while (_queue.Reader.TryRead(out _)) { }
        _pendingKeys = null;
        _pendingIndex = 0;
    }

    /// <summary>
    /// Called by widgets while drawing (toasts, spinners) to get ticks at least this often until a frame
    /// is drawn without them. Only has an effect on the UI thread.
    /// </summary>
    internal void RequestTicks(TimeSpan rate)
    {
        if (!IsUiThread) return;
        if (_frameTickRequest is not { } current || rate < current) _frameTickRequest = rate;
    }

    private static ColorMode? ColorModeOf(ITerminalBackend backend) => backend switch
    {
        AnsiBackend ansi => ansi.ColorMode,
        LegacyConsoleBackend legacy => legacy.ColorMode,
        _ => null,
    };

    /// <summary>
    /// The theme to draw with. On a terminal without colors (<c>NO_COLOR</c>), <see cref="Theme.Default"/>, chosen or
    /// not, becomes <see cref="Theme.Monochrome"/>, since its selection would otherwise be invisible.
    /// A custom theme is used as is.
    /// </summary>
    private Theme ResolveTheme(Theme? theme) =>
        _noColor && (theme is null || ReferenceEquals(theme, Theme.Default) || ReferenceEquals(theme, Theme.Dark)
                     || ReferenceEquals(theme, Theme.Light))
            ? Theme.Monochrome
            : theme ?? Theme.Default;

    private void StartTicker()
    {
        _lastTick = _time.GetTimestamp();
        Interlocked.Exchange(ref _tickPending, 0);
        _frameTickRequest = null;
        UpdateTicker();
    }

    // The tick period is the configured rate, or faster while a widget asks for it
    private void UpdateTicker()
    {
        var period = _tickRate;
        if (_frameTickRequest is { } requested && (period is not { } p || requested < p)) period = requested;
        if (period == _tickerPeriod && (_ticker is not null || period is null)) return;

        _tickerPeriod = period;
        if (period is not { } value)
        {
            StopTicker();
            return;
        }
        if (_ticker is null) _ticker = _time.CreateTimer(_ => OnTimer(), null, value, value);
        else _ticker.Change(value, value);
    }

    private void StopTicker()
    {
        _ticker?.Dispose();
        _ticker = null;
        _tickerPeriod = null;
    }

    private void OnTimer()
    {
        // At most one tick waits in the queue; a slow frame doesn't pile them up
        if (Interlocked.Exchange(ref _tickPending, 1) == 1) return;
        var now = _time.GetTimestamp();
        var elapsed = _time.GetElapsedTime(Interlocked.Exchange(ref _lastTick, now), now);
        _queue.Writer.TryWrite(new TickEvent(elapsed));
    }

    private void OnResizeSignal() => _queue.Writer.TryWrite(Wake.Instance);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Every callback failure goes to OnError.")]
    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "A throwing OnError handler is treated as Rethrow.")]
    private void HandleError(Exception error)
    {
        // The process is ending (1.x spec §25.3): report it, but don't quit or rethrow from under the signal
        if (_ended)
        {
            try
            {
                _onError?.Invoke(error);
            }
            catch (Exception)
            {
            }
            return;
        }
        if (IsHangup(error))
        {
            HangUp();
            return;
        }

        ErrorAction decision;
        try
        {
            decision = _onError?.Invoke(error) ?? ErrorAction.Rethrow;
        }
        catch (Exception handlerError)
        {
            error = handlerError;
            decision = ErrorAction.Rethrow;
        }

        switch (decision)
        {
            case ErrorAction.Continue:
                return;
            case ErrorAction.Quit:
                Quit();
                return;
            default:
                _failure ??= ExceptionDispatchInfo.Capture(error);
                Quit();
                return;
        }
    }
}
