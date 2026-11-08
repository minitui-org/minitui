
namespace MiniTui.Tests;

/// <summary>Selecting paragraph text with the mouse, and Ctrl+C routing to a selection (1.x spec §20.5).</summary>
[Collection("Console")]
public sealed class ParagraphSelectionTests : IDisposable
{
    public ParagraphSelectionTests() => Clipboard.Reset();

    public void Dispose() => Clipboard.Reset();

    private static MouseEvent Press(int x, int y, int clicks = 1) => new(MouseAction.Press, MouseButton.Left, x, y) { ClickCount = clicks };
    private static MouseEvent Drag(int x, int y) => new(MouseAction.Drag, MouseButton.Left, x, y);
    private static MouseEvent Release(int x, int y) => new(MouseAction.Release, MouseButton.Left, x, y);

    private static ConsoleKeyInfo Key(string binding) => KeyBinding.Parse(binding).ToKeyInfo();

    // Draws the text at (0, 0) without a block, so screen cells are text cells
    private static CellBuffer Draw(ScrollState state, Text text, int width = 20, int height = 4, BlockBuilder? block = null, WrapMode wrap = WrapMode.None)
    {
        var buf = new CellBuffer(width, height);
        var paragraph = Widget.Paragraph().Text(text).State(state).Wrap(wrap);
        if (block is not null) paragraph.Block(block);
        paragraph.Draw(buf, buf.Area);
        return buf;
    }

    private static ScrollState Selectable(Text text, int width = 20, int height = 4, WrapMode wrap = WrapMode.None)
    {
        var state = new ScrollState { Selectable = true };
        Draw(state, text, width, height, wrap: wrap);
        return state;
    }

    // The mouse

    [Fact]
    public void APressAndDrag_SelectsBetweenThem_EitherWay()
    {
        var state = Selectable("hello world");

        state.HandleMouse(Press(2, 0));
        Assert.False(state.HasSelection);
        state.HandleMouse(Drag(7, 0));
        Assert.Equal("llo w", state.SelectedText);

        state.HandleMouse(Press(7, 0));
        state.HandleMouse(Drag(2, 0));
        Assert.Equal("llo w", state.SelectedText);
    }

    [Fact]
    public void ASelectionAcrossRows_HasALineBreakBetweenThem_AndKeepsTrailingSpaces()
    {
        var state = Selectable("first  \nsecond\nthird");

        state.HandleMouse(Press(3, 0));
        state.HandleMouse(Drag(2, 2));

        Assert.Equal("st  \nsecond\nth", state.SelectedText);
    }

    [Fact]
    public void AWrappedLine_CopiesAsTheRowsShown()
    {
        var state = Selectable("one two three", width: 8, wrap: WrapMode.Word);

        state.SelectAll();

        Assert.Equal("one two\nthree", state.SelectedText);
    }

    [Fact]
    public void DraggingPastTheBottom_ScrollsARow_AndSelectsToTheLastVisibleRow()
    {
        var text = string.Join("\n", Enumerable.Range(0, 10).Select(i => $"row {i}"));
        var state = Selectable(text, height: 3);

        state.HandleMouse(Press(0, 1));
        state.HandleMouse(Drag(2, 5));

        Assert.Equal(1, state.Offset);
        Assert.Equal("row 1\nrow 2\nro", state.SelectedText);
    }

    [Fact]
    public void DraggingPastTheTop_ScrollsUpARow()
    {
        var text = string.Join("\n", Enumerable.Range(0, 10).Select(i => $"row {i}"));
        var state = new ScrollState { Selectable = true, Offset = 5 };
        Draw(state, text, height: 3);

        state.HandleMouse(Press(3, 0));
        state.HandleMouse(Drag(-1, -1));

        Assert.Equal(4, state.Offset);
        Assert.Equal("row 4\nrow", state.SelectedText);
    }

    [Fact]
    public void ADoubleClick_SelectsAWord_AndATripleClickTheRow()
    {
        var state = Selectable("say hello_there, ok");

        state.HandleMouse(Press(6, 0, clicks: 2));
        Assert.Equal("hello_there", state.SelectedText);

        state.HandleMouse(Press(6, 0, clicks: 3));
        Assert.Equal("say hello_there, ok", state.SelectedText);
    }

    [Fact]
    public void WideCharacters_AreSelectedWhole()
    {
        var state = Selectable("a日本b");

        // Column 2 is the right half of 日: the press snaps before it, and the drag into 本's right half ends before 本
        state.HandleMouse(Press(2, 0));
        state.HandleMouse(Drag(4, 0));

        Assert.Equal("日", state.SelectedText);
    }

    [Fact]
    public void APressBelowTheText_IsTheEndOfTheLastRow()
    {
        var state = Selectable("ab\ncd");

        state.HandleMouse(Press(1, 0));
        state.HandleMouse(Drag(5, 3));

        Assert.Equal("b\ncd", state.SelectedText);
    }

    [Fact]
    public void TheWheel_ScrollsAndKeepsTheSelection()
    {
        var text = string.Join("\n", Enumerable.Range(0, 10).Select(i => $"row {i}"));
        var state = Selectable(text, height: 3);
        state.HandleMouse(Press(0, 0));
        state.HandleMouse(Drag(3, 0));

        Assert.True(state.HandleMouse(new MouseEvent(MouseAction.ScrollDown, MouseButton.None, 0, 0)));

        Assert.Equal(3, state.Offset);
        Assert.Equal("row", state.SelectedText);
    }

    [Fact]
    public void ACenteredRow_IsSelectedWhereItIsDrawn()
    {
        var state = new ScrollState { Selectable = true };
        var buf = new CellBuffer(10, 1);
        Widget.Paragraph().Text("abcd").Alignment(Alignment.Center).State(state).Draw(buf, buf.Area);

        // "abcd" starts at column 3
        state.HandleMouse(Press(4, 0));
        state.HandleMouse(Drag(6, 0));

        Assert.Equal("bc", state.SelectedText);
    }

    [Fact]
    public void WithoutSelectable_TheMouseDoesntSelect()
    {
        var state = new ScrollState();
        Draw(state, "hello");

        Assert.False(state.HandleMouse(Press(0, 0)));
        state.HandleMouse(Drag(3, 0));

        Assert.False(state.HasSelection);
        Assert.False(state.HandleKey(Key("ctrl+a")));
    }

    // Keys

    [Fact]
    public void CtrlA_SelectsAll_CtrlC_Copies_AndEscClears()
    {
        var state = Selectable("ab\ncd");

        Assert.False(state.HandleKey(Key("ctrl+c")));
        Assert.False(state.HandleKey(Key("esc")));
        Assert.True(state.HandleKey(Key("ctrl+a")));
        Assert.True(state.HandleKey(Key("ctrl+c")));
        Assert.Equal("ab\ncd", Clipboard.GetText());
        Clipboard.SetText("");
        Assert.True(state.HandleKey(Key("ctrl+insert")));
        Assert.Equal("ab\ncd", Clipboard.GetText());
        Assert.True(state.HandleKey(Key("esc")));

        Assert.False(state.HasSelection);
        Assert.Equal("", state.SelectedText);
    }

    // Changing text

    [Fact]
    public void WhenTheTextGrows_TheSelectionStaysOnItsRows()
    {
        var state = Selectable("one\ntwo");
        state.HandleMouse(Press(0, 1));
        state.HandleMouse(Drag(2, 1));

        Draw(state, "one\ntwo\nthree");

        Assert.Equal("tw", state.SelectedText);
    }

    [Fact]
    public void WhenItsRowsAreGone_TheSelectionIsCleared()
    {
        var state = Selectable("one\ntwo");
        state.HandleMouse(Press(0, 1));
        state.HandleMouse(Drag(2, 1));

        Draw(state, "one");

        Assert.False(state.HasSelection);
    }

    // Drawing

    [Fact]
    public void TheSelection_IsDrawnFocused_OrUnfocusedByTheBlock()
    {
        var state = Selectable("hello");
        state.HandleMouse(Press(1, 0));
        state.HandleMouse(Drag(3, 0));

        var plain = Draw(state, "hello");
        Assert.Equal(Theme.Current.Selection.Bg, plain.Get(1, 0).Bg);
        Assert.Equal(Theme.Current.Selection.Bg, plain.Get(2, 0).Bg);
        Assert.NotEqual(Theme.Current.Selection.Bg, plain.Get(3, 0).Bg);

        // In a block the text starts at (2, 1), inside the padding; the same text cells are selected
        var unfocused = Draw(state, "hello", height: 3, block: Widget.Block());
        Assert.Equal(Theme.Current.SelectionUnfocused.Bg, unfocused.Get(3, 1).Bg);
        var focused = Draw(state, "hello", height: 3, block: Widget.Block().Focused());
        Assert.Equal(Theme.Current.Selection.Bg, focused.Get(3, 1).Bg);
    }

    // Through an app

    private sealed class Log : Screen
    {
        public ScrollState State { get; } = new() { Selectable = true };
        public override bool HasSelection => State.HasSelection;
        public override bool HandleKey(ConsoleKeyInfo key) => State.HandleKey(key);
        public override bool HandleMouse(MouseEvent mouse) => State.HandleMouse(mouse);
        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) => Widget.Paragraph().Text("alpha\nbeta").State(State).Draw(buf, area);
    }

    [Fact]
    public void CtrlC_CopiesASelection_AndQuitsWithout()
    {
        using var harness = new ComponentHarness(new Log(), 20, 3);
        harness.RunUntilIdle();
        harness.Mouse(Press(0, 0));
        harness.Mouse(Drag(3, 1));

        harness.Press("ctrl+c");
        Assert.True(harness.App.IsRunning);
        Assert.Equal("alpha\nbet", Clipboard.GetText());

        harness.Press("esc");
        harness.Press("ctrl+insert");
        Assert.True(harness.App.IsRunning);
        harness.Press("ctrl+c");
        Assert.False(harness.App.IsRunning);
    }

    [Fact]
    public void AMessageDialogsText_CanBeSelectedAndCopied()
    {
        var screen = new Log();
        using var harness = new ComponentHarness(screen, 40, 12);
        harness.RunUntilIdle();
        _ = harness.App.Overlays.ShowMessageAsync("Error", "Disk on fire");
        harness.RunUntilIdle();
        var y = Array.FindIndex(harness.Lines, l => l.Contains("Disk on fire", StringComparison.Ordinal));
        var x = harness.Lines[y].IndexOf("Disk", StringComparison.Ordinal);

        harness.DoubleClick(x + 5, y);
        Assert.Equal(Theme.Current.Selection.Bg, harness.CellAt(x + 5, y).Bg);
        harness.Press("ctrl+c");

        Assert.Equal("on", Clipboard.GetText());
        Assert.True(harness.App.IsRunning);
        Assert.False(screen.HasSelection);
        // Esc clears the selection first, then closes
        harness.Press("esc");
        Assert.Contains(harness.Lines, l => l.Contains("Disk on fire", StringComparison.Ordinal));
        harness.Press("esc");
        Assert.DoesNotContain(harness.Lines, l => l.Contains("Disk on fire", StringComparison.Ordinal));
    }

    [Fact]
    public void AMessageDialogsText_CanBeSelectedWithTheCaret_AndEscHidesItBeforeClosing()
    {
        var screen = new Log();
        using var harness = new ComponentHarness(screen, 40, 12);
        harness.RunUntilIdle();
        _ = harness.App.Overlays.ShowMessageAsync("Error", "Disk on fire");
        harness.RunUntilIdle();

        harness.Press("f7");
        harness.Press("shift+end");
        harness.Press("ctrl+c");
        Assert.Equal("Disk on fire", Clipboard.GetText());
        Assert.True(harness.App.IsRunning);
        harness.Press("esc"); // the selection
        harness.Press("esc"); // the caret
        Assert.Contains(harness.Lines, l => l.Contains("Disk on fire", StringComparison.Ordinal));
        harness.Press("esc");
        Assert.DoesNotContain(harness.Lines, l => l.Contains("Disk on fire", StringComparison.Ordinal));
    }

    // Esc in the text states (1.x spec §10.3)

    [Fact]
    public void InATextField_EscClearsTheSelection_KeepingTheCursor_ThenGoesOn()
    {
        var state = new TextFieldState { Text = "hello" };
        state.Select(1, 4);

        Assert.True(state.HandleKey(Key("esc")));
        Assert.False(state.HasSelection);
        Assert.Equal(4, state.Cursor);
        Assert.Equal("hello", state.Text);
        Assert.False(state.HandleKey(Key("esc")));
    }

    [Fact]
    public void InATextArea_EscClearsTheSelection_ThenGoesOn()
    {
        var state = new TextAreaState("one\ntwo");
        state.Select(new TextPosition(0, 1), new TextPosition(1, 2));

        Assert.True(state.HandleKey(Key("esc")));
        Assert.False(state.HasSelection);
        Assert.Equal(new TextPosition(1, 2), state.Cursor);
        Assert.False(state.HandleKey(Key("esc")));
    }

    [Fact]
    public void InACommandLine_EscClearsTheSelection_ThenTheText()
    {
        var state = new CommandLineState { Text = ":open file" };
        state.SelectAll();

        Assert.True(state.HandleKey(Key("esc")));
        Assert.Equal(":open file", state.Text);
        Assert.True(state.HandleKey(Key("esc")));
        Assert.Equal("", state.Text);
    }

    private sealed class Form : Screen
    {
        public TextFieldState State { get; } = new() { Text = "hello" };
        public override bool CapturesText => true;
        public override bool HandleKey(ConsoleKeyInfo key) => State.HandleKey(key);
        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) => Widget.TextField().State(State).Focused().Draw(buf, area);
    }

    [Fact]
    public void InAPushedScreen_EscClearsTheSelection_BeforeGoingBack()
    {
        using var harness = new ComponentHarness(new Log(), 20, 3);
        harness.RunUntilIdle();
        var form = new Form();
        harness.App.Screens.Push(form);
        harness.RunUntilIdle();
        form.State.SelectAll();

        harness.Press("esc");
        Assert.Same(form, harness.App.Screens.Top);
        Assert.False(form.State.HasSelection);

        harness.Press("esc");
        Assert.NotSame(form, harness.App.Screens.Top);
    }
}
