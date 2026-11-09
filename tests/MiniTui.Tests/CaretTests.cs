namespace MiniTui.Tests;

/// <summary>A caret in selectable text, shown with F7 (1.x spec §27.4).</summary>
[Collection("Console")]
public sealed class CaretTests : IDisposable
{
    public CaretTests() => Clipboard.Reset();

    public void Dispose() => Clipboard.Reset();

    private static ConsoleKeyInfo Key(string binding) => KeyBinding.Parse(binding).ToKeyInfo();

    private static CellBuffer Draw(ScrollState state, Text text, int width = 20, int height = 4, WrapMode wrap = WrapMode.Word)
    {
        var buf = new CellBuffer(width, height);
        Widget.Paragraph().Text(text).State(state).Wrap(wrap).Draw(buf, buf.Area);
        return buf;
    }

    // Where the caret was drawn: the one cell in the cursor style
    private static (int X, int Y)? Caret(CellBuffer buf)
    {
        for (var y = 0; y < buf.Height; y++)
            for (var x = 0; x < buf.Width; x++)
                if ((buf.Get(x, y).Modifiers & Modifiers.Reversed) != 0) return (x, y);
        return null;
    }

    private static void Press(ScrollState state, params string[] keys)
    {
        foreach (var key in keys) Assert.True(state.HandleKey(Key(key)), key);
    }

    [Fact]
    public void F7_ShowsTheCaretAtTheTopOfTheView_OnlyInSelectableText()
    {
        var plain = new ScrollState();
        Assert.False(plain.HandleKey(Key("f7")));
        plain.CaretVisible = true;
        Assert.False(plain.CaretVisible);

        var state = new ScrollState { Selectable = true };
        Text text = "one\ntwo\nthree\nfour\nfive\nsix";
        Draw(state, text);
        state.Offset = 2;
        Draw(state, text);
        Press(state, "f7");
        Assert.True(state.CaretVisible);
        Assert.Equal((0, 0), Caret(Draw(state, text)));
        Press(state, "down");
        Assert.Equal((0, 1), Caret(Draw(state, text)));
        Assert.Equal(2, state.Offset); // the arrows move the caret, not the view
    }

    [Fact]
    public void TheArrows_MoveTheCaret_AcrossRows()
    {
        var state = new ScrollState { Selectable = true };
        Text text = "hello world\nhi\nbye there";
        Draw(state, text);
        Press(state, "f7", "right", "right");
        Assert.Equal((2, 0), Caret(Draw(state, text)));
        Press(state, "end", "right");
        Assert.Equal((0, 1), Caret(Draw(state, text)));
        Press(state, "left");
        Assert.Equal((11, 0), Caret(Draw(state, text)));

        // ↑ ↓ keep the column they started from, through a shorter row
        Press(state, "home");
        for (var i = 0; i < 6; i++) Press(state, "right");
        Press(state, "down");
        Assert.Equal((2, 1), Caret(Draw(state, text)));
        Press(state, "down");
        Assert.Equal((6, 2), Caret(Draw(state, text)));

        Press(state, "ctrl+home");
        Assert.Equal((0, 0), Caret(Draw(state, text)));
        Press(state, "ctrl+end");
        Assert.Equal((9, 2), Caret(Draw(state, text)));
        Press(state, "right"); // at the end it stays
        Assert.Equal((9, 2), Caret(Draw(state, text)));
    }

    [Fact]
    public void ShiftSelects_CtrlCCopies_AndEscClearsThenHides()
    {
        var state = new ScrollState { Selectable = true };
        Text text = "hello world";
        Draw(state, text);
        Press(state, "f7");
        for (var i = 0; i < 5; i++) Press(state, "shift+right");
        Assert.True(state.HasSelection);
        Assert.Equal("hello", state.SelectedText);
        Press(state, "ctrl+c");
        Assert.Equal("hello", Clipboard.GetText());

        Press(state, "esc");
        Assert.False(state.HasSelection);
        Assert.True(state.CaretVisible);
        Assert.Equal((5, 0), Caret(Draw(state, text)));
        Press(state, "esc");
        Assert.False(state.CaretVisible);
        Assert.Null(Caret(Draw(state, text)));
        Assert.False(state.HandleKey(Key("esc")));

        Press(state, "f7", "f7");
        Assert.False(state.CaretVisible);
    }

    [Fact]
    public void TheCaret_KeepsItsRowInView()
    {
        var state = new ScrollState { Selectable = true };
        Text text = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"line {i}"));
        Draw(state, text);
        Press(state, "f7");
        for (var i = 0; i < 6; i++) Press(state, "down");
        Assert.Equal(3, state.Offset);
        Assert.Equal((0, 3), Caret(Draw(state, text)));
        Press(state, "pgdn"); // a page is the view less a row: the caret to row 9
        Assert.Equal(6, state.Offset);
        Press(state, "ctrl+home");
        Assert.Equal(0, state.Offset);
    }

    [Fact]
    public void WideCharacters_AreOneStep()
    {
        var state = new ScrollState { Selectable = true };
        Text text = "日本語";
        Draw(state, text);
        Press(state, "f7", "right");
        Assert.Equal((2, 0), Caret(Draw(state, text)));
        Press(state, "shift+right");
        Assert.Equal("本", state.SelectedText);
    }

    [Fact]
    public void AClick_MovesTheCaret_AndTheCaretSurvivesANewWidth()
    {
        var state = new ScrollState { Selectable = true };
        Text text = "Tomatoes need six hours of sun";
        Draw(state, text);
        Press(state, "f7");
        state.HandleMouse(new MouseEvent(MouseAction.Press, MouseButton.Left, 9, 0));
        state.HandleMouse(new MouseEvent(MouseAction.Release, MouseButton.Left, 9, 0));
        Assert.Equal((9, 0), Caret(Draw(state, text)));
        Press(state, "shift+end");
        Assert.Equal("need six", state.SelectedText); // the first row ends at "six"

        // At 12 columns "need" starts the second row; the selection stays on the same words
        Draw(state, text, width: 12, height: 5);
        Assert.StartsWith("need six", state.SelectedText, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCaret_ShownBeforeAnythingIsDrawn_AppearsAtTheFirstDraw()
    {
        var state = new ScrollState { Selectable = true };
        Press(state, "f7");
        Assert.True(state.HandleKey(Key("right"))); // nowhere to move yet, but the key is the caret's
        Assert.Equal((0, 0), Caret(Draw(state, "abc")));
        Press(state, "right");
        Assert.Equal((1, 0), Caret(Draw(state, "abc")));
    }

    [Fact]
    public void AtTheEndOfAFullRow_TheCaretIsOnItsLastCell()
    {
        var state = new ScrollState { Selectable = true };
        Draw(state, "abcd", width: 4, height: 1);
        Press(state, "f7", "end");
        Assert.Equal((3, 0), Caret(Draw(state, "abcd", width: 4, height: 1)));
    }

    [Fact]
    public void WithoutTheCaret_TheArrowsScrollAsBefore()
    {
        var state = new ScrollState { Selectable = true };
        Text text = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"line {i}"));
        Draw(state, text);
        Press(state, "down", "down");
        Assert.Equal(2, state.Offset);
        Assert.Null(Caret(Draw(state, text)));
    }

    // ---- Markdown ----

    [Fact]
    public void InMarkdownWithTheBlockCursor_TheCaretStartsAtTheHighlight_AndHidingItGivesTheArrowsBack()
    {
        var doc = new MarkdownState("# Garden\n\nTomatoes need sun.\n\nWater daily.") { BlockCursor = true };
        doc.Scroll.Selectable = true;
        var buf = new CellBuffer(30, 8);
        Widget.Markdown(doc).Draw(buf, buf.Area);
        doc.SetHighlight(1);
        Widget.Markdown(doc).Draw(buf, buf.Area);

        Assert.True(doc.HandleKey(Key("f7")));
        buf = new CellBuffer(30, 8);
        Widget.Markdown(doc).Draw(buf, buf.Area);
        Assert.Equal((2, 3), Caret(buf)); // after the bar's 2 columns, on "Tomatoes"

        for (var i = 0; i < 8; i++) Assert.True(doc.HandleKey(Key("shift+right")));
        Assert.Equal((1, 1), doc.Highlight);
        Assert.True(doc.HandleKey(Key("ctrl+c")));
        Assert.Equal("Tomatoes", Clipboard.GetText());

        Assert.True(doc.HandleKey(Key("esc")));
        Assert.True(doc.HandleKey(Key("esc")));
        Assert.False(doc.Scroll.CaretVisible);
        Assert.True(doc.HandleKey(Key("down")));
        Assert.Equal((2, 2), doc.Highlight);
    }

    [Fact]
    public void InMarkdownWithoutTheBlockCursor_F7WorksAsInAParagraph()
    {
        var doc = new MarkdownState("Hello there");
        doc.Scroll.Selectable = true;
        var buf = new CellBuffer(20, 3);
        Widget.Markdown(doc).Draw(buf, buf.Area);
        Assert.True(doc.HandleKey(Key("f7")));
        Assert.True(doc.HandleKey(Key("shift+end")));
        Assert.Equal("Hello there", doc.Scroll.SelectedText);
    }
}
