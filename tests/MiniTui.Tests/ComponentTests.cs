
namespace MiniTui.Tests;

/// <summary>A component that records its lifecycle calls into a shared log.</summary>
internal class Probe : Component
{
    private readonly List<string> _log;
    private readonly string _name;

    public Probe(List<string> log, string name, params Probe[] children)
    {
        _log = log;
        _name = name;
        foreach (var child in children) AddChild(child);
    }

    public bool LifetimeCancelledAtUnmount { get; private set; }
    public Func<ConsoleKeyInfo, bool>? OnKeyPress { get; set; }
    public bool Captures { get; set; }

    public override bool CapturesText => Captures;
    public override string KeysGroup => _name;
    public override bool HandleKey(ConsoleKeyInfo key) => OnKeyPress?.Invoke(key) == true;
    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) => buf.SetString(area.X, area.Y, _name, Style.Default);

    public new FocusScope Focus => base.Focus;
    public new App App => base.App;
    public new CancellationToken Lifetime => base.Lifetime;
    public T Add<T>(T child) where T : Component => AddChild(child);
    public void Remove(Component child) => RemoveChild(child);

    protected override void OnMount() => _log.Add($"{_name} mount");
    protected override void OnUnmount()
    {
        LifetimeCancelledAtUnmount = Lifetime.IsCancellationRequested;
        _log.Add($"{_name} unmount");
    }
    protected override void OnActivated() => _log.Add($"{_name} activate");
    protected override void OnDeactivated() => _log.Add($"{_name} deactivate");
}

internal sealed class ProbeScreen : Screen
{
    private readonly Probe _body;

    public ProbeScreen(List<string> log, string name, params Probe[] children)
    {
        Name = name;
        _body = AddChild(new Probe(log, $"{name}.body", children));
        Focus.Add(_body);
        Log = log;
    }

    public string Name { get; }
    public List<string> Log { get; }
    public Probe Body => _body;
    public override string Title => Name;
    public Func<ConsoleKeyInfo, bool>? OnKeyPress { get; set; }
    public override bool HandleKey(ConsoleKeyInfo key) => OnKeyPress?.Invoke(key) == true;

    public override void Render(CellBuffer buf, Rect area, RenderContext ctx)
    {
        buf.SetString(area.X, area.Y, $"[{Name}]", Style.Default);
        _body.Render(buf, area with { Y = area.Y + 1, Height = Math.Max(0, area.Height - 1) }, ctx.For(_body));
    }

    protected override void OnMount() => Log.Add($"{Name} mount");
    protected override void OnUnmount() => Log.Add($"{Name} unmount");
    protected override void OnActivated() => Log.Add($"{Name} activate");
    protected override void OnDeactivated() => Log.Add($"{Name} deactivate");
}

public class ComponentLifecycleTests
{
    [Fact]
    public void PushAndPop_FollowTheC3Order()
    {
        var log = new List<string>();
        var a = new ProbeScreen(log, "A", new Probe(log, "a1"), new Probe(log, "a2"));
        using var harness = new ComponentHarness(a);
        Assert.Equal(["A mount", "A.body mount", "a1 mount", "a2 mount", "A activate", "A.body activate", "a1 activate", "a2 activate"], log);

        log.Clear();
        var b = new ProbeScreen(log, "B");
        harness.App.Screens.Push(b);
        Assert.Equal(["B mount", "B.body mount", "a2 deactivate", "a1 deactivate", "A.body deactivate", "A deactivate", "B activate", "B.body activate"], log);

        log.Clear();
        harness.App.Screens.Pop();
        Assert.Equal(["B.body deactivate", "B deactivate", "B.body unmount", "B unmount", "A activate", "A.body activate", "a1 activate", "a2 activate"], log);

        log.Clear();
        harness.App.Screens.Pop();
        Assert.Equal(["a2 deactivate", "a1 deactivate", "A.body deactivate", "A deactivate", "a2 unmount", "a1 unmount", "A.body unmount", "A unmount"], log);
        Assert.False(harness.App.IsRunning); // QuitWhenEmpty
    }

    [Fact]
    public void Replace_DoesNotActivateTheScreenBelow_AndPopToUnwinds()
    {
        var log = new List<string>();
        using var harness = new ComponentHarness(new ProbeScreen(log, "A"));
        var b = new ProbeScreen(log, "B");
        harness.App.Screens.Push(b);
        log.Clear();

        harness.App.Screens.Replace(new ProbeScreen(log, "C"));
        Assert.DoesNotContain("A activate", log);
        Assert.Equal(["A", "C"], harness.App.Screens.All.Select(s => s.Title));

        harness.App.Screens.Push(new ProbeScreen(log, "D"));
        log.Clear();
        harness.App.Screens.PopTo(harness.App.Screens.All[0]);
        Assert.Equal(["D.body deactivate", "D deactivate", "D.body unmount", "D unmount",
                      "C.body unmount", "C unmount", // C was already deactivated when D covered it
                      "A activate", "A.body activate"], log);
        Assert.Throws<InvalidOperationException>(() => harness.App.Screens.PopTo(b));
    }

    [Fact]
    public void Lifetime_IsCancelledBeforeOnUnmount_AndAppThrowsWhenUnmounted()
    {
        var log = new List<string>();
        var probe = new Probe(log, "p");
        Assert.Throws<InvalidOperationException>(() => probe.App);

        using var harness = new ComponentHarness(new ProbeScreen(log, "A", probe));
        Assert.Same(harness.App, probe.App);
        harness.App.Screens.Pop();

        Assert.True(probe.LifetimeCancelledAtUnmount);
        Assert.Throws<InvalidOperationException>(() => probe.App);
    }

    [Fact]
    public void C4_C5_AddChildMountsNow_RemoveChildUnmountsAndLeavesFocus_ChildHasOneParent()
    {
        var log = new List<string>();
        var screen = new ProbeScreen(log, "A");
        using var harness = new ComponentHarness(screen);
        log.Clear();

        var child = screen.Body.Add(new Probe(log, "late"));
        screen.Body.Focus.Add(child);
        Assert.Equal(["late mount", "late activate"], log);
        Assert.Throws<InvalidOperationException>(() => new Probe(log, "other").Add(child));

        log.Clear();
        screen.Body.Remove(child);
        Assert.Equal(["late deactivate", "late unmount"], log);
        Assert.Null(screen.Body.Focus.Focused);
        Assert.Throws<InvalidOperationException>(() => screen.Body.Focus.Add(child));
    }

    [Fact]
    public void PoppedScreen_InFlightLoad_SeesCancelledToken_AndItsResultIsIgnored()
    {
        var log = new List<string>();
        using var harness = new ComponentHarness(new ProbeScreen(log, "Root"));
        var fetch = new TaskCompletionSource<string>();
        var screen = new LoadingScreen(fetch.Task);

        harness.App.Screens.Push(screen);
        Assert.Equal(LoadStatus.Loading, screen.Data.Status);
        harness.App.Screens.Pop();

        Assert.True(screen.Token.IsCancellationRequested);
        fetch.SetResult("late");
        harness.RunUntilIdle();
        Assert.Null(screen.Data.Value);
    }

    [Fact]
    public void ScreensAndRenderCallback_AreMutuallyExclusive()
    {
        var withRender = App.Create().Render((_, _) => { });
        Assert.Throws<InvalidOperationException>(() => withRender.Screens.Push(new ProbeScreen([], "A")));

        var withScreens = App.Create();
        withScreens.Screens.Push(new ProbeScreen([], "A"));
        Assert.Throws<InvalidOperationException>(() => withScreens.Render((_, _) => { }));
    }

    private sealed class LoadingScreen(Task<string> result) : Screen
    {
        public Loadable<string> Data { get; } = new();
        public CancellationToken Token { get; private set; }

        protected override void OnMount() => Load(Data, ct =>
        {
            Token = ct;
            return result;
        });

        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) { }
    }
}

public class KeyRoutingTests
{
    [Fact]
    public void Step1_OnKeyPreHook_StopsRouting()
    {
        var log = new List<string>();
        var screen = new ProbeScreen(log, "A");
        var reached = false;
        screen.OnKeyPress = _ => reached = true;
        using var harness = new ComponentHarness(screen);
        harness.App.OnKey(key => key.KeyChar == 'x');

        harness.Press("x");
        Assert.False(reached);
        harness.Press("y");
        Assert.True(reached);
    }

    [Fact]
    public void Step3_InnermostFirst_HandleKeyBeforeKeys_ThenGlobal()
    {
        var log = new List<string>();
        var inner = new Probe(log, "inner");
        var screen = new ProbeScreen(log, "A", inner);
        screen.Body.Focus.Add(inner);
        var order = new List<string>();
        inner.OnKeyPress = key => { order.Add("inner.HandleKey"); return key.KeyChar == '1'; };
        inner.Keys.Add("2", "two", () => order.Add("inner.Keys"));
        screen.Body.Keys.Add("3", "three", () => order.Add("body.Keys"));
        screen.OnKeyPress = key => { order.Add("screen.HandleKey"); return false; };
        using var harness = new ComponentHarness(screen);
        harness.App.Keys.Add("4", "four", () => order.Add("global"));

        harness.Press("1");
        Assert.Equal(["inner.HandleKey"], order);
        order.Clear();
        harness.Press("2");
        Assert.Equal(["inner.HandleKey", "inner.Keys"], order);
        order.Clear();
        harness.Press("3");
        Assert.Equal(["inner.HandleKey", "body.Keys"], order);
        order.Clear();
        harness.Press("4");
        Assert.Equal(["inner.HandleKey", "screen.HandleKey", "global"], order);
    }

    [Fact]
    public void TextCapture_TypedQIsInserted_WhileCtrlCStillQuits()
    {
        var log = new List<string>();
        var field = new FieldComponent();
        var screen = new ProbeScreen(log, "A");
        screen.Body.Add(field);
        screen.Body.Focus.Add(field);
        screen.Body.Keys.Add("x", "Ancestor x", () => log.Add("ancestor x"));
        using var harness = new ComponentHarness(screen);
        harness.App.Keys.Add("q", "Quit", harness.App.Quit);

        harness.Type("qx");
        Assert.Equal("qx", field.State.Text);
        Assert.DoesNotContain("ancestor x", log);
        Assert.True(harness.App.IsRunning);

        harness.Press("ctrl+c");
        Assert.False(harness.App.IsRunning);
    }

    [Fact]
    public void TextCapture_SkipsPlainCharacterBindings_EvenForKeysTheFieldDeclines()
    {
        var log = new List<string>();
        var field = Component.From((_, _, _) => { }, handleKey: _ => false, capturesText: true);
        var screen = new ProbeScreen([], "A");
        screen.Body.Add(field);
        screen.Body.Focus.Add(field);
        screen.Body.Keys.Add("x", "Ancestor x", () => log.Add("ancestor x"));
        field.Keys.Add("y", "Own y", () => log.Add("own y"));
        using var harness = new ComponentHarness(screen);
        harness.App.Keys.Add("q", "Quit", harness.App.Quit);

        harness.Type("qxy");

        Assert.True(harness.App.IsRunning);
        Assert.Equal(["own y"], log); // the capturing component's own bindings still apply
    }

    [Fact]
    public void TextCapture_StillLetsCtrlAndNavigationBindingsBubble()
    {
        var log = new List<string>();
        var field = new FieldComponent();
        var screen = new ProbeScreen([], "A");
        screen.Body.Add(field);
        screen.Body.Focus.Add(field);
        screen.Body.Keys.Add("ctrl+s", "Save", () => log.Add("save")).Add("f5", "Refresh", () => log.Add("refresh"));
        using var harness = new ComponentHarness(screen);

        harness.Press("ctrl+s f5");

        Assert.Equal(["save", "refresh"], log);
    }

    [Fact]
    public void Overlays_SwallowEveryKey_AndEscClosesThemBeforePopping()
    {
        var log = new List<string>();
        var screen = new ProbeScreen(log, "A");
        screen.Keys.Add("r", "Refresh", () => log.Add("refresh"));
        using var harness = new ComponentHarness(new ProbeScreen(log, "Root"));
        harness.App.Screens.Push(screen);
        harness.App.Keys.Add("g", "Global", () => log.Add("global"));
        _ = harness.App.Overlays.ShowMessageAsync("Note", "hello");
        harness.RunUntilIdle();

        harness.Press("r g ? q tab");
        Assert.DoesNotContain(log, l => l is "refresh" or "global");
        Assert.True(harness.App.Overlays.Top is MessageDialog);

        harness.Press("esc");
        Assert.False(harness.App.Overlays.Any);
        Assert.Equal(2, harness.App.Screens.Count);

        harness.Press("esc");
        Assert.Equal(1, harness.App.Screens.Count);
        harness.Press("esc");
        Assert.Equal(1, harness.App.Screens.Count); // the last screen isn't popped
    }

    [Fact]
    public void Tab_CyclesTheInnermostScopeWithTwoMembers()
    {
        var log = new List<string>();
        var a1 = new Probe(log, "a1");
        var a2 = new Probe(log, "a2");
        var a = new Probe(log, "a", a1, a2);
        a.Focus.Add(a1).Add(a2);
        var b = new Probe(log, "b");
        var screen = new ProbeScreen(log, "S", a, b);
        screen.Body.Focus.Add(a).Add(b);
        using var harness = new ComponentHarness(screen);

        harness.Press("tab");
        Assert.True(a.Focus.IsFocused(a2));
        Assert.True(screen.Body.Focus.IsFocused(a));
        harness.Press("tab");
        Assert.True(a.Focus.IsFocused(a1)); // wraps
        harness.Press("shift+tab");
        Assert.True(a.Focus.IsFocused(a2));
    }

    [Fact]
    public void UnhandledKeys_AreDropped()
    {
        using var harness = new ComponentHarness(new ProbeScreen([], "A"));

        harness.Press("z f9 ctrl+alt+x");

        Assert.True(harness.App.IsRunning);
    }

    [Fact]
    public void RenderContext_ForChild_IsFocusedOnlyOnTheChain()
    {
        var log = new List<string>();
        RenderContext? first = null, second = null;
        var c1 = Component.From((_, _, ctx) => first = ctx);
        var c2 = Component.From((_, _, ctx) => second = ctx);
        var screen = new ContainerScreen(c1, c2);
        using var harness = new ComponentHarness(screen);

        Assert.True(first!.Focused);
        Assert.False(second!.Focused);
        harness.Press("tab");
        Assert.False(first!.Focused);
        Assert.True(second!.Focused);

        _ = harness.App.Overlays.ConfirmAsync("?");
        harness.RunUntilIdle();
        Assert.False(second!.Focused); // the overlay has focus
    }

    private sealed class ContainerScreen : Screen
    {
        private readonly Component[] _children;

        public ContainerScreen(params Component[] children)
        {
            _children = children;
            foreach (var child in children) Focus.Add(AddChild(child));
        }

        public override void Render(CellBuffer buf, Rect area, RenderContext ctx)
        {
            foreach (var child in _children) child.Render(buf, area, ctx.For(child));
        }
    }

    internal sealed class FieldComponent : Component
    {
        public TextFieldState State { get; } = new();
        public override bool CapturesText => true;
        public override bool HandleKey(ConsoleKeyInfo key) => State.HandleKey(key);
        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
            Widget.TextField().State(State).Focused(ctx.Focused).Draw(buf, area);
    }
}

public class HintsAndHelpTests
{
    [Fact]
    public void InnerBinding_ShadowsOuterBinding()
    {
        var log = new List<string>();
        var screen = new ProbeScreen([], "Services");
        screen.Keys.Add("r", "Reload", () => log.Add("reload")).Add("x", "Export", () => { });
        screen.Body.Keys.Add("r", "Refresh", () => log.Add("refresh"));
        using var harness = new ComponentHarness(screen);
        harness.App.Keys.Add("q", "Quit", harness.App.Quit).Add("x", "Global x", () => { });

        var hints = KeyRouter.Hints(harness.App, 200);

        Assert.Equal("r Refresh  x Export  ? Help  q Quit", hints);
        harness.Press("r");
        Assert.Equal(["refresh"], log);
        Assert.StartsWith(" r Refresh  x Export", harness.Lines[^1], StringComparison.Ordinal);
        Assert.EndsWith("Services ", harness.Lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Help_GroupsByComponentInChainOrder_ThenGlobal_WithoutShadowedBindings()
    {
        var log = new List<string>();
        var screen = new ProbeScreen(log, "Services");
        screen.Keys.Add("r", "Reload", () => { }).Add("enter", "Open", () => { });
        screen.Body.Keys.Add("r", "Refresh", () => { });
        using var harness = new ComponentHarness(screen, 60, 20);

        Assert.Equal(
            [
                "Services",
                "  enter   Open",
                "",
                "Services.body",
                "  r       Refresh",
                "",
                "Global",
                "  ?, f1   Help",
                "  ctrl+c  Quit",
            ],
            KeyRouter.HelpLines(harness.App));

        harness.Press("?");
        Assert.IsType<HelpOverlay>(harness.App.Overlays.Top);
        Assert.Contains(harness.Lines, l => l.Contains("ctrl+c  Quit", StringComparison.Ordinal));
        harness.Press("?");
        Assert.False(harness.App.Overlays.Any);
    }

    [Fact]
    public void Breadcrumb_JoinsTitles_AndTruncatesFromTheLeft()
    {
        using var harness = new ComponentHarness(new ProbeScreen([], "Home"), 40, 5);
        harness.App.Screens.Push(new ProbeScreen([], "Services"));
        harness.App.Screens.Push(new ProbeScreen([], "api-gateway"));
        harness.RunUntilIdle();

        // 1.x spec §22.3: the hints first, then the breadcrumb in what's left, cut from the left
        Assert.StartsWith(" esc Back  ? Help", harness.Lines[^1], StringComparison.Ordinal);
        Assert.EndsWith("…rvices › api-gateway ", harness.Lines[^1], StringComparison.Ordinal);
    }
}

public class DialogTests
{
    [Theory]
    [InlineData("enter", true)]
    [InlineData("y", true)]
    [InlineData("esc", false)]
    [InlineData("n", false)]
    [InlineData("right enter", false)]
    public void ConfirmAsync_Answers_AndResumesOnTheUiThread(string keys, bool expected)
    {
        using var harness = new ComponentHarness(new ProbeScreen([], "A"));
        bool? answer = null;
        var onUiThread = false;
        _ = harness.App.RunTask(async _ =>
        {
            answer = await harness.App.Overlays.ConfirmAsync("Delete it?");
            onUiThread = harness.App.IsUiThread;
        });
        harness.RunUntilIdle();
        Assert.Contains(harness.Lines, l => l.Contains("Delete it?", StringComparison.Ordinal));

        harness.Press(keys);

        Assert.Equal(expected, answer);
        Assert.True(onUiThread);
        Assert.False(harness.App.Overlays.Any);
    }

    [Fact]
    public void ShowMessageAsync_ScrollsAndCompletesWhenClosed()
    {
        using var harness = new ComponentHarness(new ProbeScreen([], "A"), 40, 12);
        var body = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"line {i}"));
        var closed = harness.App.Overlays.ShowMessageAsync("Log", body);
        harness.RunUntilIdle();
        Assert.Contains(harness.Lines, l => l.Contains("line 1 ", StringComparison.Ordinal));

        harness.Press("end");
        Assert.Contains(harness.Lines, l => l.Contains("line 30", StringComparison.Ordinal));
        Assert.False(closed.IsCompleted);

        harness.Press("enter");
        Assert.True(closed.IsCompleted);
    }

    [Fact]
    public void CommandLineOverlay_RunsCommands_AndToastsTheResult()
    {
        var added = new List<string>();
        var registry = CommandRegistry.Create()
            .Add("add").Handler(cmd => { added.Add(cmd.ArgsAsString); return CommandResult.Ok("Added"); })
            .Build();
        using var harness = new ComponentHarness(new ProbeScreen([], "A"), 60, 10);
        harness.App.Keys.Add(":", "Command", () => harness.App.Overlays.Show(new CommandLineOverlay(registry)));

        harness.Type(":add milk q");
        Assert.True(harness.App.IsRunning);
        harness.Press("enter");

        Assert.Equal(["milk q"], added);
        Assert.False(harness.App.Overlays.Any);
        Assert.Equal(1, harness.App.Toasts.Count);

        harness.Type(":nope");
        harness.Press("enter");
        Assert.Contains(harness.Lines, l => l.Contains("Unknown command: nope", StringComparison.Ordinal));
    }
}

public class ScreenModeRunTests
{
    [Fact]
    public async Task Run_WithScreens_RendersTheTopScreenAndChrome_AndQuitsWhenEmpty()
    {
        var backend = new TestBackend(40, 6);
        var input = new ScriptedInputSource();
        var app = AppHarness.Create(backend, input);
        app.Screens.Push(new ProbeScreen([], "Home"));

        var run = AppHarness.Run(app);
        await AppHarness.WaitUntil(app, () => backend.Lines[0].StartsWith("[Home]", StringComparison.Ordinal));
        Assert.EndsWith("Home ", backend.Lines[^1], StringComparison.Ordinal);

        app.Post(() => app.Screens.Push(new ProbeScreen([], "Next")));
        await AppHarness.WaitUntil(app, () => backend.Lines[0].StartsWith("[Next]", StringComparison.Ordinal));
        input.Enqueue(new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false));
        await AppHarness.WaitUntil(app, () => app.Screens.Count == 1);
        app.Post(() => app.Screens.Pop());
        await run;
    }

    [Fact]
    public async Task RenderCallbackApp_CanUseOverlays_WhichAreModal()
    {
        var backend = new TestBackend(40, 10);
        var input = new ScriptedInputSource();
        var keys = new List<char>();
        bool? answer = null;
        var app = AppHarness.Create(backend, input);
        app.OnKey(key => { keys.Add(key.KeyChar); return false; })
            .Render((buf, _) => buf.SetString(0, 0, "main", Style.Default));
        app.OnSetup(a => a.RunTask(async _ => answer = await a.Overlays.ConfirmAsync("Sure?")));

        var run = AppHarness.Run(app);
        await AppHarness.WaitUntil(app, () => backend.Lines.Any(l => l.Contains("Sure?", StringComparison.Ordinal)));
        input.EnqueueText("n");
        await AppHarness.WaitUntil(app, () => answer is not null);
        app.Quit();
        await run;

        Assert.False(answer);
        Assert.Equal(['n'], keys); // OnKey still sees the key first
    }
}
