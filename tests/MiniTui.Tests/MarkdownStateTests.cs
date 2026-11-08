namespace MiniTui.Tests;

/// <summary><see cref="MarkdownState"/> and <see cref="Widget.Markdown"/> (1.x spec §27.3): drawing, scrolling and the block cursor.</summary>
[Collection("Console")]
public sealed class MarkdownStateTests : IDisposable
{
    private const string Garden = "# Garden plan\n\nTomatoes need six hours of **sun**.\n\n- milk\n- eggs\n";

    public MarkdownStateTests() => Clipboard.Reset();

    public void Dispose() => Clipboard.Reset();

    private static ConsoleKeyInfo Key(string binding) => KeyBinding.Parse(binding).ToKeyInfo();

    private static string[] Draw(MarkdownState state, int width = 30, int height = 8, bool focused = true, CellBuffer? buf = null)
    {
        buf ??= new CellBuffer(width, height);
        Widget.Markdown(state).Focused(focused).Empty("Nothing here").Draw(buf, buf.Area);
        return [.. buf.ToLines().Select(l => l.TrimEnd())];
    }

    private static MarkdownState Long(int paragraphs = 30) =>
        new(string.Join("\n\n", Enumerable.Range(1, paragraphs).Select(i => $"Paragraph {i}.")));

    // ---- Drawing ----

    [Fact]
    public void TheMarkdown_IsDrawnFormatted()
    {
        Assert.Equal(["Garden plan", "═══════════", "", "Tomatoes need six hours of", "sun.", "", "• milk", "• eggs"], Draw(new MarkdownState(Garden)));
    }

    [Fact]
    public void WithNothingToShow_TheEmptyTextIsDrawnMuted()
    {
        var buf = new CellBuffer(20, 2);
        Draw(new MarkdownState("   \n"), buf: buf);
        Assert.Equal("Nothing here", buf.ToLines()[0].TrimEnd());
        Assert.Equal(Theme.Default.TextMuted.Fg, buf.Get(0, 0).Fg);
    }

    [Fact]
    public void ABlockAroundIt_IsFocusedWithIt()
    {
        var buf = new CellBuffer(20, 4);
        Widget.Markdown(new MarkdownState("Hi")).Block(Widget.Block().Title(" Doc ")).Focused().Draw(buf, buf.Area);
        Assert.Equal("┌ Doc ─────────────┐", buf.ToLines()[0]);
        Assert.Equal("│ Hi               │", buf.ToLines()[1]);
        Assert.Equal(Theme.Default.BorderFocused.Fg, buf.Get(0, 0).Fg);
    }

    [Fact]
    public void Overflowing_ShowsAScrollbar_InTheLastColumn()
    {
        var lines = Draw(Long(), width: 20, height: 4);
        Assert.Equal("Paragraph 1.", lines[0][..12]);
        Assert.NotEqual(' ', lines[0][19]);
    }

    [Fact]
    public void TabsAreSpacesToTheNextMultipleOf4_AndCrLfIsALineBreak()
    {
        var state = new MarkdownState("```\na\tb\r\n\tc\n```");
        Assert.Equal(["  a   b", "      c"], Draw(state)[..2]);
    }

    // ---- Without the block cursor ----

    [Fact]
    public void WithoutTheBlockCursor_TheKeysScroll_AndNothingIsHighlighted()
    {
        var state = Long();
        Draw(state, height: 4);
        Assert.True(state.HandleKey(Key("down")));
        Assert.Equal(1, state.Scroll.Offset);
        Assert.True(state.HandleKey(Key("pgdn")));
        Assert.Equal(5, state.Scroll.Offset);
        Assert.Null(state.Highlight);
        Assert.False(state.HasSelection);
        Assert.Equal("", state.HighlightedSource);
        Assert.DoesNotContain(Draw(state, height: 4), l => l.StartsWith('▌'));
    }

    // ---- The block cursor ----

    [Fact]
    public void TheBlockCursor_IsABarBesideTheHighlightedBlock_MovedByTheArrows()
    {
        var state = new MarkdownState(Garden) { BlockCursor = true };
        Assert.Equal(["▌ Garden plan", "▌ ═══════════", "", "  Tomatoes need six hours of"], Draw(state)[..4]);
        var changed = 0;
        state.HighlightChanged += (_, _) => changed++;
        state.HandleKey(Key("down"));
        Assert.Equal((1, 1), state.Highlight);
        Assert.Equal(1, changed);
        Assert.Equal(["  Garden plan", "  ═══════════", "", "▌ Tomatoes need six hours of", "▌ sun."], Draw(state)[..5]);
        state.HandleKey(Key("shift+down"));
        state.HandleKey(Key("shift+down"));
        Assert.Equal((1, 3), state.Highlight);
        state.HandleKey(Key("up"));
        Assert.Equal((2, 2), state.Highlight);
        state.HandleKey(Key("k"));
        state.HandleKey(Key("up"));
        Assert.Equal((0, 0), state.Highlight);
    }

    [Fact]
    public void TheBar_IsInTheFocusedOrUnfocusedBorderColor()
    {
        var buf = new CellBuffer(20, 3);
        var state = new MarkdownState("Hi") { BlockCursor = true };
        Draw(state, focused: true, buf: buf);
        Assert.Equal(Theme.Default.BorderFocused.Fg, buf.Get(0, 0).Fg);
        Draw(state, focused: false, buf: buf);
        Assert.Equal(Theme.Default.Border.Fg, buf.Get(0, 0).Fg);
    }

    [Fact]
    public void TheHighlight_IsKeptInView()
    {
        var state = Long();
        state.BlockCursor = true;
        Draw(state, height: 5);
        state.HandleKey(Key("end"));
        Draw(state, height: 5);
        Assert.Equal((29, 29), state.Highlight);
        Assert.StartsWith("▌ Paragraph 30. ", Draw(state, height: 5)[^1], StringComparison.Ordinal);

        state.HandleKey(Key("home"));
        Assert.StartsWith("▌ Paragraph 1. ", Draw(state, height: 5)[0], StringComparison.Ordinal);

        state.HandleKey(Key("pgdn"));
        Draw(state, height: 5);
        Assert.Equal(state.RowsOf(state.Highlight!.Value.First)!.Value.First, state.Scroll.Offset);
        Assert.Equal(state.Highlight!.Value.First, state.BlockAt(state.Scroll.Offset));

        state.SetHighlight(20);
        Assert.Contains(Draw(state, height: 5), l => l.StartsWith("▌ Paragraph 21. ", StringComparison.Ordinal));
    }

    [Fact]
    public void AClick_HighlightsTheBlockUnderIt_AndTheWheelBringsTheHighlightAlong()
    {
        var state = new MarkdownState(Garden) { BlockCursor = true };
        Draw(state);
        state.HandleMouse(new MouseEvent(MouseAction.Press, MouseButton.Left, 5, 6));
        Assert.Equal((2, 2), state.Highlight);
        Assert.Null(state.BlockAt(2)); // the blank row

        var page = Long();
        page.BlockCursor = true;
        Draw(page, height: 4);
        for (var i = 0; i < 5; i++) page.HandleMouse(new MouseEvent(MouseAction.ScrollDown, MouseButton.None, 5, 1));
        Assert.Equal(page.BlockAt(page.Scroll.Offset) ?? page.BlockAt(page.Scroll.Offset + 1), page.Highlight!.Value.First);
    }

    [Fact]
    public void TheHighlightedBlocks_AsWrittenAndAsShown()
    {
        var state = new MarkdownState("Intro\n\n- **milk**\n  and more\n- eggs\n\n| a |\n|---|\n| 1 |") { BlockCursor = true };
        Draw(state);
        state.SetHighlight(1, 2);
        Assert.Equal("- **milk**\n  and more\n- eggs", state.HighlightedSource);
        Assert.Equal("• milk and more\n• eggs", state.HighlightedText);
        state.SetHighlight(3);
        Assert.Equal("| a |\n|---|\n| 1 |", state.HighlightedSource);
        Assert.Equal("a\n─\n1", state.HighlightedText);

        // A nested item loses the indentation its lines share
        state.Source = "- a\n  - b\n    more";
        state.SetHighlight(1);
        Assert.Equal("- b\n  more", state.HighlightedSource);
    }

    [Fact]
    public void CtrlC_CopiesTheHighlight_OrAMouseSelection()
    {
        var state = new MarkdownState(Garden) { BlockCursor = true };
        state.Scroll.Selectable = true;
        Draw(state);
        state.SetHighlight(1);
        Assert.True(state.HasSelection);
        Assert.True(state.HandleKey(Key("ctrl+c")));
        Assert.Equal("Tomatoes need six hours of\nsun.", Clipboard.GetText());

        state.HandleMouse(new MouseEvent(MouseAction.Press, MouseButton.Left, 2, 3));
        state.HandleMouse(new MouseEvent(MouseAction.Drag, MouseButton.Left, 10, 3));
        state.HandleMouse(new MouseEvent(MouseAction.Release, MouseButton.Left, 10, 3));
        Assert.True(state.Scroll.HasSelection);
        Assert.True(state.HandleKey(Key("ctrl+c")));
        Assert.Equal("Tomatoes", Clipboard.GetText());
        Assert.True(state.HandleKey(Key("esc")));
        Assert.False(state.Scroll.HasSelection);
        Assert.False(state.HandleKey(Key("esc")));
    }

    [Fact]
    public void NewSource_StartsAtTheTop_WithTheFirstBlockHighlighted()
    {
        var state = Long();
        state.BlockCursor = true;
        Draw(state, height: 4);
        state.HandleKey(Key("end"));
        Draw(state, height: 4);
        state.Source = Garden;
        Assert.Equal((0, 0), state.Highlight);
        Assert.Equal(0, state.Scroll.Offset);
        Assert.Equal(4, state.Blocks.Count);
        Assert.Equal("▌ Garden plan", Draw(state)[0]);
        state.SetHighlight(-5, 99);
        Assert.Equal((0, 3), state.Highlight);
    }

    [Fact]
    public void PlainText_ShowsTheSourceAsWritten()
    {
        var state = new MarkdownState("# not a heading\n**kept**\n\nnext") { PlainText = true };
        Assert.Equal(["# not a heading", "**kept**", "", "next"], Draw(state)[..4]);
        Assert.Equal([MarkdownBlockKind.Verbatim, MarkdownBlockKind.Verbatim], state.Blocks.Select(b => b.Kind));
    }

    [Fact]
    public void BlockLookups_BeforeDrawing_AreNull()
    {
        var state = new MarkdownState(Garden);
        Assert.Null(state.BlockAt(0));
        Assert.Null(state.RowsOf(0));
        Assert.Equal("", state.HighlightedText);
    }
}
