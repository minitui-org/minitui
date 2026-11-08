using Microsoft.Extensions.Time.Testing;
using static MiniTui.Tests.Snapshot;

namespace MiniTui.Tests;

public class WidgetSweepTests
{
    private static readonly IReadOnlyList<string> Items = ["alpha", "日本語", "😀 emoji", "é", "\x1b[31mevil"];

    private static readonly Dictionary<string, Action<CellBuffer, Rect>> Draws = new()
    {
        ["Block"] = (b, r) => Widget.Block().Title(" Title ").TitleBottom("bottom").Padding(new Margin(1)).Focused().Draw(b, r),
        ["Paragraph"] = (b, r) => Widget.Paragraph().Text("Some long text that wraps\n日本語のテキスト").Wrap(WrapMode.Word)
            .State(new ScrollState()).Block(Widget.Block()).Draw(b, r),
        ["ParagraphChar"] = (b, r) => Widget.Paragraph().Text("😀😀😀 wide").Wrap(WrapMode.Char).Alignment(Alignment.Right).Draw(b, r),
        ["List"] = (b, r) => Widget.List().Title("Items").Items(Items).State(new SelectionState { Selected = 3 })
            .HighlightSymbol("> ").Focused().Draw(b, r),
        ["Table"] = (b, r) => Widget.Table().Header("Name", "Value").Columns(Constraint.Min(3), Constraint.Percentage(40))
            .Rows(Items, s => new Row(s, s)).State(new SelectionState { Selected = 4, SortColumn = 1 })
            .HighlightSymbol("▶").Block(Widget.Block()).Focused().Draw(b, r),
        ["Scrollbar"] = (b, r) => Widget.Scrollbar().ContentLength(100).Position(50).ViewportLength(3).Draw(b, r),
        ["ScrollbarH"] = (b, r) => Widget.Scrollbar().Horizontal().ContentLength(100).Position(99).ViewportLength(3).Draw(b, r),
        ["Gauge"] = (b, r) => Widget.Gauge().Ratio(0.57).Block(Widget.Block()).Draw(b, r),
        ["Sparkline"] = (b, r) => Widget.Sparkline().Data([1, 5, double.NaN, -2, 8, 3]).Draw(b, r),
        ["Canvas"] = (b, r) => Widget.Canvas().XBounds(-1, 1).YBounds(-1, 1).Block(Widget.Block()).Paint(p =>
        {
            p.Line(-2, -2, 2, 2);
            p.Circle(0, 0, 0.8);
            p.Rectangle(-0.5, -0.5, 1, 1);
            p.Print(-1, 0, "label \x1b[31m日本");
        }).Draw(b, r),
        ["Chart"] = (b, r) => Widget.Chart().Datasets(new Dataset("sin \x1b", [(0, 0), (1, 1), (2, 0)]),
                new Dataset("pts", [(0.5, 0.5), (1.5, double.NaN)]) { Type = GraphType.Scatter, Marker = Marker.Dot })
            .XAxis(new Axis { Title = "x 日本" }).YAxis(new Axis { Title = "y", Labels = ["lo", "\x1bhi"] }).Block(Widget.Block()).Draw(b, r),
        ["BarChart"] = (b, r) => Widget.BarChart().Bars(new Bar("日本", 3), new Bar("b\x1b", 1), new Bar("c", -1)).Block(Widget.Block()).Draw(b, r),
        ["BarChartH"] = (b, r) => Widget.BarChart().Direction(Direction.Horizontal).Bars(new Bar("日本語", 3) { ValueText = "\x1b3" }, new Bar("b", 1.5)).Draw(b, r),
        ["Markdown"] = (b, r) => Widget.Markdown(new MarkdownState("# 日本\n\n- a **b** \x1b[31m\n\n| x | y |\n|---|--:|\n| 1 | 😀 |\n\n```\ncode\n```") { BlockCursor = true })
            .Block(Widget.Block()).Focused().Draw(b, r),
        ["MarkdownEmpty"] = (b, r) => Widget.Markdown(new MarkdownState()).Empty("Nothing \x1b here").Draw(b, r),
        ["Spinner"] = (b, r) => Widget.Spinner().Label("Loading").Draw(b, r),
        ["Toasts"] = (b, r) =>
        {
            var q = new ToastQueue();
            q.Push("Saved 日本", ToastLevel.Success);
            q.Push("Failed", ToastLevel.Error);
            Widget.Toasts().Queue(q).Draw(b, r);
        },
        ["StatusBar"] = (b, r) => Widget.StatusBar().Left("left side text").Right("right").Draw(b, r),
        ["TextField"] = (b, r) => Widget.TextField().State(new TextFieldState("日本語 text")).Placeholder("…")
            .Block(Widget.Block()).Focused().Draw(b, r),
        ["CommandLine"] = (b, r) => Widget.CommandLine().State(new CommandLineState { Text = ":open" }).Draw(b, r),
        ["TabBar"] = (b, r) => Widget.TabBar().Tabs("One", "Two", "日本").State(new TabBarState { Selected = 2 }).Focused().Draw(b, r),
        ["Button"] = (b, r) => Widget.Button().Label("OK").Focused().Draw(b, r),
        ["Checkbox"] = (b, r) => Widget.Checkbox().Label("Enabled").Checked().Focused().Draw(b, r),
        ["ConfirmDialog"] = (b, r) => DrawOverlay(new ConfirmDialog("Really delete 日本?"), b, r),
        ["MessageDialog"] = (b, r) => DrawOverlay(new MessageDialog("Note", string.Join("\n", Items)), b, r),
        ["HelpOverlay"] = (b, r) => DrawOverlay(new HelpOverlay(["Group", "  r  Refresh", "", "  q  Quit"]), b, r),
    };

    public static TheoryData<string> Names => [.. Draws.Keys];

    // An overlay's placement and content, as the app draws them (border included)
    private static void DrawOverlay(Overlay overlay, CellBuffer buf, Rect screen)
    {
        var rect = overlay.Placement(screen);
        var inner = Widget.Block().Title(overlay.Title is { } t ? new Line(t) : null).Draw(buf, rect);
        overlay.Render(buf, inner, new RenderContext(App.Create(), overlay, focused: true));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void W1_NeverThrows_ForAnyArea(string widget)
    {
        foreach (var (buf, area) in WidgetSweep.Cases())
            Draws[widget](buf, area);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void W2_NeverWritesControlCharacters(string widget)
    {
        var buf = new CellBuffer(30, 12);

        Draws[widget](buf, buf.Area);

        foreach (var line in buf.ToLines())
            Assert.DoesNotContain(line, c => char.IsControl(c));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void W3_Monochrome_UsesNoColors(string widget)
    {
        var buf = new CellBuffer(30, 12);

        using (Theme.Use(Theme.Monochrome)) Draws[widget](buf, buf.Area);

        for (var y = 0; y < buf.Height; y++)
        for (var x = 0; x < buf.Width; x++)
            Assert.Equal((Color.Reset, Color.Reset), (buf.Get(x, y).Fg, buf.Get(x, y).Bg));
    }
}

public class BlockAndParagraphTests
{
    [Fact]
    public void Block_DrawsTitlesAndReturnsTheInnerArea()
    {
        var buf = new CellBuffer(12, 4);

        var inner = Widget.Block().Title(" Title ").TitleBottom("end").TitleAlignment(Alignment.Center)
            .BorderType(BoxStyle.Rounded).Padding(new Margin(1, 0)).Draw(buf, buf.Area);

        Assert.Equal(new Rect(2, 1, 8, 2), inner);
        AssertTrimmed(buf, """
            ╭─ Title ──╮
            │          │
            │          │
            ╰───end────╯
            """);
    }

    [Fact]
    public void Block_TruncatesTitleWithEllipsis_AndUsesFocusStyles()
    {
        var buf = new CellBuffer(8, 2);

        Widget.Block().Title("A long title").Borders(Borders.Top | Borders.Left).Focused().Draw(buf, buf.Area);

        AssertTrimmed(buf, """
            ┌A long…
            │
            """);
        AssertStyle(buf, 0, 0, Color.Cyan, Color.Reset);
        AssertStyle(buf, 1, 0, Color.Yellow, Color.Reset);
    }

    [Theory]
    [InlineData("The quick brown fox", 10, WrapMode.Word, new[] { "The quick", "brown fox" })]
    [InlineData("  indented text here", 10, WrapMode.Word, new[] { "  indented", "text here" })]
    [InlineData("a supercalifragilistic word", 8, WrapMode.Word, new[] { "a", "supercal", "ifragili", "stic", "word" })]
    [InlineData("line one\n\nline three", 20, WrapMode.Word, new[] { "line one", "", "line three" })]
    [InlineData("trailing     spaces", 9, WrapMode.Word, new[] { "trailing", "spaces" })]
    [InlineData("日本語のテキスト", 5, WrapMode.Word, new[] { "日本", "語の", "テキ", "スト" })]
    [InlineData("abcdefg", 3, WrapMode.Char, new[] { "abc", "def", "g" })]
    [InlineData("no wrap at all here", 5, WrapMode.None, new[] { "no wrap at all here" })]
    public void Paragraph_WrapRules(string text, int width, WrapMode mode, string[] expected)
    {
        var rows = TextWrap.Wrap(text, width, mode);

        Assert.Equal(expected, rows.Select(r => string.Concat(r.Spans.Select(s => s.Content))));
        Assert.Equal(expected.Length, Paragraph.MeasureLines(text, width, mode));
    }

    [Fact]
    public void Paragraph_KeepsSpanStylesAcrossWraps()
    {
        var line = new Line([new Span("red words ", new Style(Color.Red)), new Span("blue", new Style(Color.Blue))]);
        var buf = new CellBuffer(6, 3);

        Widget.Paragraph().Text(line).Wrap(WrapMode.Word).Draw(buf, buf.Area);

        AssertTrimmed(buf, """
            red
            words
            blue
            """);
        AssertStyle(buf, 0, 1, Color.Red, Color.Reset);
        AssertStyle(buf, 0, 2, Color.Blue, Color.Reset);
    }

    [Fact]
    public void Paragraph_AlignsAndTruncatesUnwrappedLines()
    {
        var buf = new CellBuffer(6, 2);

        Widget.Paragraph().Text("ab\ntoo long line").Alignment(Alignment.Right).Draw(buf, buf.Area);

        AssertTrimmed(buf, """
                ab
            too l…
            """);
    }

    [Fact]
    public void Paragraph_WithState_ScrollsAndShowsAScrollbar()
    {
        var buf = new CellBuffer(4, 2);
        var state = new ScrollState { Offset = 1 };

        Widget.Paragraph().Text("1\n2\n3\n4").State(state).Draw(buf, buf.Area);

        AssertTrimmed(buf, """
            2  █
            3  ░
            """);
    }
}

public class ListAndTableTests
{
    [Fact]
    public void List_FormatsOnlyVisibleItems_AndShowsScrollInfo()
    {
        var buf = new CellBuffer(24, 5);
        var formatted = new List<int>();
        var items = Enumerable.Range(0, 1000).ToList();
        var state = new SelectionState { Selected = 500 };

        Widget.List().Title("N").Items(items, i => { formatted.Add(i); return $"item {i}"; })
            .State(state).HighlightSymbol("> ").Focused().Draw(buf, buf.Area);

        Assert.Equal([498, 499, 500], formatted);
        Assert.Equal("┌N [499-501 of 1000]───┐", buf.ToLines()[0]);
        Assert.Equal("│ > item 500           ░", buf.ToLines()[3]); // the scrollbar replaces the right border
        AssertStyle(buf, 1, 3, Color.Black, Color.Cyan);
    }

    [Fact]
    public void Table_HandlesHundredThousandRows_FormattingOnlyVisibleOnes()
    {
        var rows = Enumerable.Range(0, 100_000).ToArray();
        var calls = 0;
        var buf = new CellBuffer(30, 12);
        var state = new SelectionState { Selected = 99_999 };

        Widget.Table().Header("N").Rows(rows, n => { calls++; return new Row($"{n}"); }).State(state).Draw(buf, buf.Area);

        Assert.Equal(11, calls);
        Assert.Equal(99_989, state.Offset);
        Assert.StartsWith("99999", buf.ToLines()[11], StringComparison.Ordinal);
    }

    [Fact]
    public void Table_ShowsSortMarker_AndRowStyles()
    {
        var buf = new CellBuffer(16, 3);
        var state = new SelectionState { SortColumn = 0, SortDescending = true };

        Widget.Table()
            .Header("Name", "Ver")
            .Columns(Constraint.Length(8), Constraint.Fill())
            .Rows([new Row("api", "1.0") { Style = new Style(Color.Red) }, new Row(new Line("web", new Style(Color.Green)), "2.0")])
            .State(state)
            .Draw(buf, buf.Area);

        AssertTrimmed(buf, """
            Name ▼   Ver
            api      1.0
            web      2.0
            """);
        AssertStyle(buf, 9, 1, Color.Black, Color.DarkGray);
        AssertStyle(buf, 0, 2, Color.Green, Color.Reset);
        AssertStyle(buf, 0, 0, Color.Yellow, Color.Reset, Modifiers.Bold);
    }
}

public class SmallWidgetTests
{
    [Fact]
    public void Gauge_UsesEighthBlocks_AndAPercentLabel()
    {
        var buf = new CellBuffer(10, 1);

        Widget.Gauge().Ratio(0.4625).Draw(buf, buf.Area);

        // 0.4625 * 10 = 4.625 cells: 4 full and a 5/8 block; the label is centered over cells 3-5
        AssertTrimmed(buf, "███46%░░░░");
        var noLabel = new CellBuffer(10, 1);
        Widget.Gauge().Ratio(0.4625).Label("").Draw(noLabel, noLabel.Area);
        AssertTrimmed(noLabel, "████▋░░░░░");
        Assert.True(buf.Get(3, 0).Modifiers.HasFlag(Modifiers.Reversed));
    }

    [Fact]
    public void Sparkline_IsRightAlignedToTheNewestValue()
    {
        var buf = new CellBuffer(6, 2);

        Widget.Sparkline().Data([0, 2, 4, 8]).Draw(buf, buf.Area);

        // Two rows are 16 eighths: 2 → ▄, 4 → one full row, 8 → both rows
        AssertTrimmed(buf, """
                 █
               ▄██
            """);
    }

    [Fact]
    public void Spinner_FrameComesFromTheClock()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        string Frame()
        {
            var buf = new CellBuffer(10, 1);
            Widget.Spinner().Frames(SpinnerFrames.Line).Label("Wait").Time(time).Draw(buf, buf.Area);
            return buf.ToLines()[0];
        }

        Assert.Equal("- Wait    ", Frame());
        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal("\\ Wait    ", Frame());
        time.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal("- Wait    ", Frame());
    }

    [Fact]
    public void Toasts_ShowUpToThree_AndExpireFromWhenShown()
    {
        var time = new FakeTimeProvider();
        var queue = new ToastQueue(time);
        for (var i = 1; i <= 4; i++) queue.Push($"toast {i}", duration: TimeSpan.FromSeconds(i));
        string[] Draw()
        {
            var buf = new CellBuffer(20, 12);
            Widget.Toasts().Queue(queue).Draw(buf, buf.Area);
            return buf.ToLines();
        }

        var first = Draw();
        Assert.Equal("        │ toast 1 │ ", first[1]);
        Assert.DoesNotContain(first, l => l.Contains("toast 4", StringComparison.Ordinal));

        time.Advance(TimeSpan.FromSeconds(1));
        var second = Draw();
        Assert.Equal("        │ toast 2 │ ", second[1]);
        Assert.Contains(second, l => l.Contains("toast 4", StringComparison.Ordinal));

        time.Advance(TimeSpan.FromSeconds(10));
        Assert.All(Draw(), l => Assert.Equal(new string(' ', 20), l));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void StatusBar_OneRowArea_DrawsOnThatRow_AndRightTextWins()
    {
        var buf = new CellBuffer(16, 3);

        Widget.StatusBar().Left("a long left message").Right("right").Draw(buf, new Rect(0, 1, 16, 1));

        Assert.Equal(" a long … right ", buf.ToLines()[1]);
        AssertStyle(buf, 0, 1, Color.White, Color.DarkGray);
        Assert.Equal(new string(' ', 16), buf.ToLines()[2]);
    }

    [Fact]
    public void TabBar_ScrollsToTheSelectedTab()
    {
        var buf = new CellBuffer(14, 1);

        Widget.TabBar().Tabs("One", "Two", "Three").State(new TabBarState { Selected = 2 }).Draw(buf, buf.Area);

        // "[ Two ] [ Three ]" is 17 columns, so the bar starts at the selected tab
        AssertTrimmed(buf, "[ Three ]");
    }
}

public class ThemeTests
{
    [Fact]
    public void Default_MatchesTheSpec()
    {
        var t = Theme.Default;

        Assert.Equal(Style.Default, t.Text);
        Assert.Equal(new Style(Color.Black, Color.Cyan), t.Selection);
        Assert.Equal(new Style(Color.Black, Color.DarkGray), t.SelectionUnfocused);
        Assert.Equal(new Style(Color.Yellow).Bold(), t.TableHeader);
        Assert.Equal(new Style(Color.White, Color.DarkGray), t.StatusBar);
        Assert.Equal(Style.Default.Reversed(), t.Cursor);
    }

    [Fact]
    public void Monochrome_MatchesTheSpec()
    {
        var t = Theme.Monochrome;

        Assert.Equal(Style.Default.Reversed(), t.Selection);
        Assert.Equal(Style.Default.Underlined(), t.SelectionUnfocused);
        Assert.Equal(Style.Default.Bold(), t.BorderFocused);
        Assert.Equal(Style.Default.Bold(), t.TitleFocused);
        Assert.Equal(Style.Default.Bold().Underlined(), t.Error);
    }

    [Fact]
    public async Task App_SetsCurrentWhileRunning_AndRestoresAfter()
    {
        var custom = Theme.Default with { Selection = new Style(Color.Magenta) };
        Theme? seen = null;
        var app = AppHarness.Create(new TestBackend(10, 3), new ScriptedInputSource()).WithTheme(custom);
        app.OnSetup(a => { seen = Theme.Current; a.Quit(); }).Render((_, _) => { });

        await AppHarness.Run(app);

        Assert.Same(custom, seen);
        Assert.Same(Theme.Default, Theme.Current);
    }

    [Fact]
    public void App_UsesMonochrome_ForAnExplicitDefaultTheme_WhenTheTerminalHasNoColors()
    {
        var custom = Theme.Default with { Selection = Style.Default.Bold() };
        Theme? seen = null, afterSwitch = null, afterCustom = null;
        var app = App.Create().WithBackend(new AnsiBackend(ColorMode.None, new FakeConsoleHost(10, 3)))
            .WithInput(new ScriptedInputSource()).WithTickRate(null).CaptureConsoleOutput(false)
            .WithTheme(Theme.Default);
        app.OnSetup(a =>
        {
            seen = Theme.Current;
            a.WithTheme(Theme.Default);
            afterSwitch = Theme.Current;
            a.WithTheme(custom);
            afterCustom = Theme.Current;
            a.Quit();
        }).Render((_, _) => { });

        app.Run();

        Assert.Same(Theme.Monochrome, seen);
        Assert.Same(Theme.Monochrome, afterSwitch);
        Assert.Same(custom, afterCustom); // a custom theme is the app's choice
    }

    [Fact]
    public void App_UsesMonochrome_WhenTheTerminalHasNoColors()
    {
        var host = new FakeConsoleHost(10, 3);
        Theme? seen = null;
        var app = App.Create().WithBackend(new AnsiBackend(ColorMode.None, host)).WithInput(new ScriptedInputSource())
            .WithTickRate(null).CaptureConsoleOutput(false);
        app.OnSetup(a => { seen = Theme.Current; a.Quit(); }).Render((_, _) => { });

        app.Run();

        Assert.Same(Theme.Monochrome, seen);
    }
}

public class ListTitleTests
{
    [Fact]
    public void List_PaddedTitle_KeepsPaddingAroundScrollInfo()
    {
        var buf = new CellBuffer(16, 3);

        Widget.List().Title(" Fruit ").Items(["a"]).Draw(buf, buf.Area);

        Assert.Equal("┌ Fruit [1] ───┐", buf.ToLines()[0]);
    }
}
