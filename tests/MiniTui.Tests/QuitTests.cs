using System.Diagnostics;

namespace MiniTui.Tests;

/// <summary>Asking before quitting: Ctrl+C, RequestQuit and the signals (1.x spec §25.3).</summary>
public sealed class QuitTests
{
    private static ComponentHarness Harness(List<QuitReason> asked, bool answer, out ProbeScreen screen)
    {
        screen = new ProbeScreen([], "main");
        var harness = new ComponentHarness(screen, 30, 5);
        harness.App.OnQuitting(reason =>
        {
            asked.Add(reason);
            return answer;
        });
        return harness;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ctrl_c_asks_and_quits_only_on_yes(bool answer)
    {
        var asked = new List<QuitReason>();
        using var harness = Harness(asked, answer, out _);

        harness.Press("ctrl+c");

        Assert.Equal([QuitReason.Requested], asked);
        Assert.Equal(!answer, harness.App.IsRunning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequestQuit_asks_as_ctrl_c_does(bool answer)
    {
        var asked = new List<QuitReason>();
        using var harness = Harness(asked, answer, out _);

        harness.App.RequestQuit();
        harness.RunUntilIdle();

        Assert.Equal([QuitReason.Requested], asked);
        Assert.Equal(!answer, harness.App.IsRunning);
    }

    [Fact]
    public async Task RequestQuit_from_another_thread_asks_on_the_ui_thread()
    {
        int? thread = null;
        using var harness = new ComponentHarness(new ProbeScreen([], "main"), 30, 5);
        harness.App.OnQuitting(_ =>
        {
            thread = Environment.CurrentManagedThreadId;
            return false;
        });

        await Task.Run(harness.App.RequestQuit);
        harness.RunUntilIdle();

        Assert.Equal(Environment.CurrentManagedThreadId, thread);
        Assert.True(harness.App.IsRunning);
    }

    [Fact]
    public void The_ctrl_c_binding_in_help_asks_too()
    {
        var asked = new List<QuitReason>();
        using var harness = Harness(asked, false, out _);

        Assert.True(harness.App.Keys.Handle(AppHarness.CtrlC));

        Assert.Equal([QuitReason.Requested], asked);
    }

    [Fact]
    public void Ctrl_c_that_copies_does_not_ask()
    {
        var asked = new List<QuitReason>();
        using var harness = Harness(asked, true, out var screen);
        screen.Body.Captures = true;
        screen.Body.OnKeyPress = key => key.Key == ConsoleKey.C;

        harness.Press("ctrl+c");

        Assert.Empty(asked);
        Assert.True(harness.App.IsRunning);
    }

    [Fact]
    public void Quit_and_popping_the_last_screen_do_not_ask()
    {
        var asked = new List<QuitReason>();
        using (var harness = Harness(asked, false, out _))
        {
            harness.App.Quit();
            harness.RunUntilIdle();
            Assert.False(harness.App.IsRunning);
        }
        using (var harness = Harness(asked, false, out _))
        {
            harness.App.Screens.Pop();
            harness.RunUntilIdle();
            Assert.False(harness.App.IsRunning);
        }
        Assert.Empty(asked);
    }

    [Fact]
    public async Task Cancelling_RunAsync_does_not_ask()
    {
        var asked = 0;
        using var cancel = new CancellationTokenSource();
        var app = AppHarness.Create(new TestBackend(20, 5), new ScriptedInputSource()).Render((_, _) => { })
            .OnQuitting(_ =>
            {
                asked++;
                return false;
            })
            .OnSetup(_ => cancel.Cancel());

        await app.RunAsync(cancel.Token).WaitAsync(AppHarness.Timeout);

        Assert.Equal(0, asked);
    }

    [Fact]
    public void Without_a_handler_ctrl_c_quits_as_before()
    {
        using var harness = new ComponentHarness(new ProbeScreen([], "main"), 30, 5);
        harness.Press("ctrl+c");
        Assert.False(harness.App.IsRunning);
    }

    [Fact]
    public void A_handler_that_throws_goes_to_OnError_and_is_not_a_yes()
    {
        var errors = new List<Exception>();
        using var harness = new ComponentHarness(new ProbeScreen([], "main"), 30, 5);
        harness.App.OnError(e =>
        {
            errors.Add(e);
            return ErrorAction.Continue;
        });
        harness.App.OnQuitting(_ => throw new IOException("disk full"));

        harness.Press("ctrl+c");

        Assert.IsType<IOException>(Assert.Single(errors));
        Assert.True(harness.App.IsRunning);
    }

    [Fact]
    public void A_handler_that_throws_is_rethrown_by_default()
    {
        using var harness = new ComponentHarness(new ProbeScreen([], "main"), 30, 5);
        harness.App.OnQuitting(_ => throw new IOException("disk full"));

        Assert.Throws<IOException>(() => harness.Press("ctrl+c"));
    }

    [Theory]
    [InlineData(QuitReason.Hangup)]
    [InlineData(QuitReason.Terminated)]
    public void A_signal_runs_the_handler_and_ends_whatever_it_answers(QuitReason reason)
    {
        var asked = new List<QuitReason>();
        using var harness = Harness(asked, false, out _);

        var clock = Stopwatch.StartNew();
        harness.Signal(reason);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), "the harness stops at once, not after the wait for the process to end");
        Assert.Equal([reason], asked);
        Assert.False(harness.App.IsRunning);
    }

    // ---- The signal path, with apps on their own UI threads ----

    private static async Task Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(clock.Elapsed < AppHarness.Timeout, "Timed out");
            await Task.Delay(5);
        }
    }

    private static App SignalledApp(TestBackend backend, Func<QuitReason, bool> quitting, Func<string> text)
    {
        var app = AppHarness.Create(backend, new ScriptedInputSource())
            .Render((buf, area) => buf.SetString(0, 0, text(), Style.Default))
            .OnQuitting(quitting);
        app.ConsoleOverride = true;
        app.EndedWait = TimeSpan.FromMilliseconds(200);
        return app;
    }

    [Fact]
    public async Task A_signal_waits_for_the_handler_and_nothing_is_handled_or_drawn_after_it()
    {
        var backend = new TestBackend(20, 3);
        var text = "editing";
        QuitReason? asked = null;
        var ranAfter = false;
        App? app = null;
        app = SignalledApp(backend, reason =>
        {
            asked = reason;
            text = "saved";                       // drawn if the loop drew again
            app!.Post(() => ranAfter = true);     // run if the loop went on
            return false;
        }, () => text);
        var run = app.RunAsync();
        await AppHarness.WaitUntil(app, () => backend.Lines[0].StartsWith("editing", StringComparison.Ordinal));

        // The signal arrives while the loop is busy with something that needs a frame, so both are in one iteration
        Task? signal = null;
        app.Post(() =>
        {
            signal = Task.Run(() => App.OnEndingSignal(QuitReason.Hangup));
            SpinWait.SpinUntil(() => app.HasQueuedWork, AppHarness.Timeout);
        });
        await Until(() => signal is not null);     // not on the UI thread, which is parked once the handler ran
        await signal!.WaitAsync(AppHarness.Timeout);

        Assert.Equal(QuitReason.Hangup, asked);
        await run.WaitAsync(AppHarness.Timeout);     // the process didn't end, so the loop quits after a while
        Assert.StartsWith("editing", backend.Lines[0], StringComparison.Ordinal);
        Assert.False(ranAfter);
    }

    [Fact]
    public async Task Nested_apps_on_one_thread_all_run_their_handlers_innermost_first()
    {
        var order = new List<string>();
        var innerBackend = new TestBackend(20, 3);
        var inner = SignalledApp(innerBackend, _ =>
        {
            order.Add("inner");
            return true;
        }, () => "inner");
        var outer = SignalledApp(new TestBackend(20, 3), _ =>
        {
            order.Add("outer");
            return true;
        }, () => "outer");
        outer.OnSetup(_ => outer.Post(inner.Run));
        var run = outer.RunAsync();
        var deadline = Stopwatch.StartNew();
        while (!innerBackend.Lines[0].StartsWith("inner", StringComparison.Ordinal))
        {
            Assert.True(deadline.Elapsed < AppHarness.Timeout, "the inner app didn't start");
            await Task.Delay(5);
        }

        App.OnEndingSignal(QuitReason.Terminated);

        Assert.Equal(["inner", "outer"], order);
        await run.WaitAsync(AppHarness.Timeout);
    }

    [Fact]
    public async Task A_stuck_handler_holds_the_signal_for_2_seconds_at_most()
    {
        using var release = new ManualResetEventSlim();
        var backend = new TestBackend(20, 3);
        var app = SignalledApp(backend, _ =>
        {
            release.Wait(AppHarness.Timeout);
            return true;
        }, () => "app");
        var run = app.RunAsync();
        await AppHarness.WaitUntil(app, () => backend.Lines[0].StartsWith("app", StringComparison.Ordinal));

        var clock = Stopwatch.StartNew();
        App.OnEndingSignal(QuitReason.Hangup);
        clock.Stop();
        release.Set();

        Assert.InRange(clock.Elapsed, App.SignalTimeout - TimeSpan.FromMilliseconds(50), App.SignalTimeout + TimeSpan.FromSeconds(1));
        await run.WaitAsync(AppHarness.Timeout);
    }

    [Fact]
    public async Task Apps_without_a_handler_do_not_hold_the_signal()
    {
        var backend = new TestBackend(20, 3);
        var app = AppHarness.Create(backend, new ScriptedInputSource()).Render((buf, _) => buf.SetString(0, 0, "app", Style.Default));
        app.ConsoleOverride = true;
        var run = app.RunAsync();
        await AppHarness.WaitUntil(app, () => backend.Lines[0].StartsWith("app", StringComparison.Ordinal));

        var clock = Stopwatch.StartNew();
        App.OnEndingSignal(QuitReason.Hangup);

        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(500));
        Assert.True(await AppHarness.OnUi(app, () => app.IsRunning));     // and it isn't stopped
        app.Quit();
        await run.WaitAsync(AppHarness.Timeout);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_io_error_once_the_terminal_hung_up_is_a_hangup(bool hungUp)
    {
        var backend = new TestBackend(20, 3);
        var asked = new List<QuitReason>();
        var broken = false;
        var app = SignalledApp(backend, reason =>
        {
            asked.Add(reason);
            return false;
        }, () => broken ? throw new IOException("Input/output error") : "app");
        app.HungUpOverride = () => hungUp;
        var run = app.RunAsync();
        await AppHarness.WaitUntil(app, () => backend.Lines[0].StartsWith("app", StringComparison.Ordinal));

        app.Post(() => broken = true);

        if (hungUp)
        {
            await Until(() => asked.Count > 0);

            // The SIGHUP that follows doesn't run the handler again, or wait for it
            var clock = Stopwatch.StartNew();
            App.OnEndingSignal(QuitReason.Hangup);
            Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(500));

            await run.WaitAsync(AppHarness.Timeout);
            Assert.Equal([QuitReason.Hangup], asked);
        }
        else
        {
            await Assert.ThrowsAsync<IOException>(() => run.WaitAsync(AppHarness.Timeout));
            Assert.Empty(asked);
        }
    }

    [Fact]
    public async Task A_handler_that_throws_on_a_signal_is_reported_and_the_app_still_ends_quietly()
    {
        var backend = new TestBackend(20, 3);
        var errors = new List<Exception>();
        var app = SignalledApp(backend, _ => throw new IOException("disk full"), () => "app")
            .OnError(e =>
            {
                errors.Add(e);
                return ErrorAction.Rethrow;
            });
        var run = app.RunAsync();
        await AppHarness.WaitUntil(app, () => backend.Lines[0].StartsWith("app", StringComparison.Ordinal));

        App.OnEndingSignal(QuitReason.Terminated);

        await run.WaitAsync(AppHarness.Timeout);     // doesn't throw
        Assert.IsType<IOException>(Assert.Single(errors));
    }
}
