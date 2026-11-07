using System.Diagnostics;

namespace MiniTui.Tests;

/// <summary>TextAreaState and the TextArea widget (1.x spec §6).</summary>
public class TextAreaTests
{
    private static void Press(TextAreaState state, params string[] keys)
    {
        foreach (var key in keys) Assert.True(state.HandleKey(KeyBinding.Parse(key).ToKeyInfo()), key);
    }

    private static void Type(TextAreaState state, string text)
    {
        foreach (var c in text) Assert.True(state.HandleKey(new ConsoleKeyInfo(c, 0, false, false, false)));
    }

    private static string[] Draw(TextAreaState state, int width, int height, WrapMode wrap = WrapMode.Word,
        string? placeholder = null, bool focused = true)
    {
        var buf = new CellBuffer(width, height);
        Widget.TextArea().State(state).Wrap(wrap).Placeholder(placeholder).Focused(focused).Draw(buf, buf.Area);
        return [.. buf.ToLines().Select(l => l.TrimEnd())];
    }

    private static (int X, int Y)? DrawCursor(TextAreaState state, int width, int height, WrapMode wrap = WrapMode.Word)
    {
        var buf = new CellBuffer(width, height);
        Widget.TextArea().State(state).Wrap(wrap).Focused().Draw(buf, buf.Area);
        return buf.CursorRequest;
    }

    private static TextPosition P(int line, int index) => new(line, index);

    // Text model

    [Fact]
    public void Text_RoundTrips_AndNormalizesLineBreaksTabsAndControls()
    {
        var state = new TextAreaState("a\r\nb\rc\n\td\u0007e");

        Assert.Equal("a\nb\nc\n    de", state.Text);
        Assert.Equal(["a", "b", "c", "    de"], state.Lines);
        Assert.Equal(P(3, 6), state.Cursor);
    }

    [Fact]
    public void EmptyText_IsOneEmptyLine()
    {
        var state = new TextAreaState();

        Assert.Equal([""], state.Lines);
        Assert.Equal(P(0, 0), state.Cursor);
        state.Text = "x";
        state.Clear();
        Assert.Equal("", state.Text);
    }

    [Fact]
    public void Tab_BecomesSpacesToTheNextMultipleOfFour()
    {
        var state = new TextAreaState();

        state.Insert("ab\tc\t\td");

        Assert.Equal("ab  c       d", state.Text);
    }

    [Fact]
    public void Cursor_IsClampedAndSnapsToAClusterBoundary()
    {
        var state = new TextAreaState("éx\nab")
        {
            Cursor = P(0, 1),
        };
        Assert.Equal(P(0, 2), state.Cursor);

        state.Cursor = P(9, 9);
        Assert.Equal(P(1, 2), state.Cursor);
        state.Cursor = P(-1, -1);
        Assert.Equal(P(0, 0), state.Cursor);
    }

    // Editing

    [Theory]
    [InlineData(0, "\nabc", 1, 0)]
    [InlineData(1, "a\nbc", 1, 0)]
    [InlineData(3, "abc\n", 1, 0)]
    public void Enter_SplitsTheLine(int at, string expected, int line, int index)
    {
        var state = new TextAreaState("abc") { Cursor = P(0, at) };

        Press(state, "enter");

        Assert.Equal(expected, state.Text);
        Assert.Equal(P(line, index), state.Cursor);
    }

    [Fact]
    public void BackspaceAndDelete_JoinLines()
    {
        var state = new TextAreaState("ab\ncd") { Cursor = P(1, 0) };

        Press(state, "backspace");
        Assert.Equal("abcd", state.Text);
        Assert.Equal(P(0, 2), state.Cursor);

        state.Text = "ab\ncd";
        state.Cursor = P(0, 2);
        Press(state, "delete");
        Assert.Equal("abcd", state.Text);
        Assert.Equal(P(0, 2), state.Cursor);
    }

    [Fact]
    public void BackspaceAtTheStart_AndDeleteAtTheEnd_DoNothing_ButAreHandled()
    {
        var state = new TextAreaState("ab") { Cursor = P(0, 0) };

        Press(state, "backspace");
        state.Cursor = P(0, 2);
        Press(state, "delete");

        Assert.Equal("ab", state.Text);
    }

    [Fact]
    public void CtrlW_DeletesTheWordBefore_AcrossALineBreak()
    {
        var state = new TextAreaState("one two\nthree");
        state.Cursor = P(1, 0);

        Press(state, "ctrl+w");

        Assert.Equal("one three", state.Text);
        Assert.Equal(P(0, 4), state.Cursor);
    }

    [Fact]
    public void CtrlU_AndCtrlK_WorkOnTheLine_AndJoinAtItsEnds()
    {
        var state = new TextAreaState("ab\ncd\nef") { Cursor = P(1, 1) };

        Press(state, "ctrl+u");
        Assert.Equal("ab\nd\nef", state.Text);
        Press(state, "ctrl+u");
        Assert.Equal("abd\nef", state.Text);
        Assert.Equal(P(0, 2), state.Cursor);

        Press(state, "ctrl+k");
        Assert.Equal("ab\nef", state.Text);
        Press(state, "ctrl+k");
        Assert.Equal("abef", state.Text);
    }

    [Fact]
    public void Typing_JoinsSurrogatePairs_AndNeverSplitsClusters()
    {
        var state = new TextAreaState();

        Type(state, "a😀");
        state.Insert("é");
        Press(state, "left");
        Assert.Equal(P(0, 3), state.Cursor);
        Press(state, "backspace");

        Assert.Equal("aé", state.Text);
    }

    [Fact]
    public void Paste_KeepsLineBreaks_AndTheCursorEndsAfterIt()
    {
        var state = new TextAreaState("[]") { Cursor = P(0, 1) };

        Assert.True(state.Paste("one\ntwo\nthree"));

        Assert.Equal(["[one", "two", "three]"], state.Lines);
        Assert.Equal(P(2, 5), state.Cursor);
    }

    [Fact]
    public void Paste_TenThousandLines_IsQuick()
    {
        var state = new TextAreaState();
        var text = string.Join('\n', Enumerable.Range(0, 10_000).Select(i => $"line {i}"));
        var clock = Stopwatch.StartNew();

        state.Paste(text);
        Draw(state, 40, 10);

        Assert.Equal(10_000, state.Lines.Count);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"took {clock.Elapsed}");
    }

    [Theory]
    [InlineData("tab")]
    [InlineData("shift+tab")]
    [InlineData("esc")]
    [InlineData("ctrl+s")]
    [InlineData("alt+enter")]
    public void KeysForTheApp_AreNotHandled(string key)
    {
        var state = new TextAreaState("x");

        Assert.False(state.HandleKey(KeyBinding.Parse(key).ToKeyInfo()));
        Assert.Equal("x", state.Text);
    }

    // Movement

    [Fact]
    public void LeftAndRight_CrossLineBreaks()
    {
        var state = new TextAreaState("ab\ncd") { Cursor = P(1, 0) };

        Press(state, "left");
        Assert.Equal(P(0, 2), state.Cursor);
        Press(state, "right");
        Assert.Equal(P(1, 0), state.Cursor);

        state.Cursor = P(0, 0);
        Press(state, "left");
        Assert.Equal(P(0, 0), state.Cursor);
    }

    [Fact]
    public void CtrlLeftAndRight_MoveByWord_AcrossLines()
    {
        var state = new TextAreaState("one two\nthree") { Cursor = P(1, 3) };

        Press(state, "ctrl+left");
        Assert.Equal(P(1, 0), state.Cursor);
        Press(state, "ctrl+left");
        Assert.Equal(P(0, 4), state.Cursor);
        Press(state, "ctrl+right");
        Assert.Equal(P(0, 7), state.Cursor);
        Press(state, "ctrl+right");
        Assert.Equal(P(1, 5), state.Cursor);
    }

    [Fact]
    public void UpAndDown_KeepTheGoalColumn_ThroughAShortLine()
    {
        var state = new TextAreaState("abcdef\nab\nabcdef") { Cursor = P(0, 5) };

        Press(state, "down");
        Assert.Equal(P(1, 2), state.Cursor);
        Press(state, "down");
        Assert.Equal(P(2, 5), state.Cursor);
        Press(state, "up", "up");
        Assert.Equal(P(0, 5), state.Cursor);
    }

    [Fact]
    public void UpAndDown_UseDisplayColumns_WithWideCharacters()
    {
        var state = new TextAreaState("日本語\nabcdef") { Cursor = P(1, 4) };

        Press(state, "up");
        Assert.Equal(P(0, 2), state.Cursor); // column 4 is the start of 語

        Press(state, "down");
        Assert.Equal(P(1, 4), state.Cursor);
    }

    [Fact]
    public void UpOnTheFirstRow_GoesToTheStart_AndDownOnTheLast_ToTheEnd()
    {
        var state = new TextAreaState("abc\ndef") { Cursor = P(0, 2) };

        Press(state, "up");
        Assert.Equal(P(0, 0), state.Cursor);

        state.Cursor = P(1, 1);
        Press(state, "down");
        Assert.Equal(P(1, 3), state.Cursor);
    }

    [Fact]
    public void UpAndDown_MoveByWrappedRow()
    {
        var state = new TextAreaState("aaaa bbbb cccc");
        Draw(state, 6, 5);
        state.Cursor = P(0, 1);

        Press(state, "down");
        Assert.Equal(P(0, 6), state.Cursor);
        Press(state, "down");
        Assert.Equal(P(0, 11), state.Cursor);
        Press(state, "up", "up");
        Assert.Equal(P(0, 1), state.Cursor);
    }

    [Fact]
    public void DownIntoAShorterWrappedRow_StaysOnThatRow()
    {
        // Rows: "aaaa " and "bbbbbbb"; column 6 is past the first row's end, so the cursor stops before the wrap
        var state = new TextAreaState("aaaa bbbbbbb\nxxxxxxx");
        Draw(state, 7, 5);
        state.Cursor = P(1, 6);

        Press(state, "up");
        Assert.Equal(P(0, 11), state.Cursor);
        Press(state, "up");
        Assert.Equal(P(0, 4), state.Cursor);
    }

    [Fact]
    public void HomeAndEnd_GoToTheRow_ThenTheLine()
    {
        var state = new TextAreaState("aaaa bbbb cccc");
        Draw(state, 6, 5);
        state.Cursor = P(0, 7);

        Press(state, "home");
        Assert.Equal(P(0, 5), state.Cursor);
        Press(state, "home");
        Assert.Equal(P(0, 0), state.Cursor);

        state.Cursor = P(0, 6);
        Press(state, "end");
        Assert.Equal(P(0, 9), state.Cursor);
        Press(state, "end");
        Assert.Equal(P(0, 14), state.Cursor);
    }

    [Fact]
    public void CtrlHomeAndEnd_GoToTheStartAndEndOfTheText()
    {
        var state = new TextAreaState("ab\ncd\nef") { Cursor = P(1, 1) };

        Press(state, "ctrl+end");
        Assert.Equal(P(2, 2), state.Cursor);
        Press(state, "ctrl+home");
        Assert.Equal(P(0, 0), state.Cursor);
    }

    [Fact]
    public void PageUpAndDown_MoveByTheDrawnHeight()
    {
        var state = new TextAreaState(string.Join('\n', Enumerable.Range(0, 20).Select(i => $"line{i}")));
        Draw(state, 10, 5);
        state.Cursor = P(2, 1);

        Press(state, "pgdn");
        Assert.Equal(P(7, 1), state.Cursor);
        Press(state, "pgup", "pgup");
        Assert.Equal(P(0, 0), state.Cursor);
    }

    // Layout and drawing

    [Fact]
    public void WordWrap_KeepsTheSpacesAtTheWrapPoint()
    {
        var rows = TextAreaLayout.Build(["aaaa bbbb cc"], 6, WrapMode.Word, P(0, 0));

        Assert.Equal([(0, 5), (5, 10), (10, 12)], rows.Select(r => (r.Start, r.End)));
    }

    [Fact]
    public void WordWrap_BreaksAWordWiderThanTheArea()
    {
        var rows = TextAreaLayout.Build(["abcdefghij"], 4, WrapMode.Word, P(0, 0));

        Assert.Equal([(0, 4), (4, 8), (8, 10)], rows.Select(r => (r.Start, r.End)));
    }

    [Fact]
    public void CharWrap_BreaksAnywhere()
    {
        var rows = TextAreaLayout.Build(["ab cd ef"], 3, WrapMode.Char, P(0, 0));

        Assert.Equal([(0, 3), (3, 6), (6, 8)], rows.Select(r => (r.Start, r.End)));
    }

    [Fact]
    public void AWideCharacterAtTheEdge_MovesToTheNextRow()
    {
        var state = new TextAreaState("abc日");

        Assert.Equal(["abc", "日"], Draw(state, 4, 3, focused: false)[..2]);
    }

    [Fact]
    public void Drawing_WrapsLines_AndShowsTheCursor()
    {
        var state = new TextAreaState("The quick brown fox\njumps") { Cursor = P(0, 4) };

        Assert.Equal(["The quick", "brown fox", "jumps", ""], Draw(state, 10, 4));
        Assert.Equal((4, 0), DrawCursor(state, 10, 4));
    }

    [Fact]
    public void TheCursorAtAWrapPoint_ShowsAtTheStartOfTheNextRow()
    {
        var state = new TextAreaState("aaaa bbbb") { Cursor = P(0, 5) };

        Assert.Equal((0, 1), DrawCursor(state, 6, 3));
    }

    [Fact]
    public void TheCursorAfterAFullRow_GetsARowOfItsOwn()
    {
        var state = new TextAreaState("abcd");

        Assert.Equal((0, 1), DrawCursor(state, 4, 3));
        Assert.Equal(["abcd", "", ""], Draw(state, 4, 3));

        state.Cursor = P(0, 0);
        Assert.Equal(2, TextAreaLayout.Build(state.Lines, 4, WrapMode.Word, P(0, 4)).Count);
        Assert.Single(TextAreaLayout.Build(state.Lines, 4, WrapMode.Word, P(0, 0)));
    }

    [Fact]
    public void Placeholder_ShowsWhileEmpty_WithTheCursorAtTheStart()
    {
        var state = new TextAreaState();

        Assert.Equal(["Write a note", ""], Draw(state, 20, 2, placeholder: "Write a note"));
        Assert.Equal((0, 0), DrawCursor(state, 20, 2));
    }

    [Fact]
    public void Unfocused_ShowsNoCursor()
    {
        var state = new TextAreaState("hi");
        var buf = new CellBuffer(10, 2);

        Widget.TextArea().State(state).Draw(buf, buf.Area);

        Assert.Null(buf.CursorRequest);
    }

    [Fact]
    public void ABlock_DrawsAroundTheText()
    {
        var state = new TextAreaState("hello");
        var buf = new CellBuffer(11, 3);

        Widget.TextArea().State(state).Block(Widget.Block()).Focused().Draw(buf, buf.Area);

        // A column of padding inside each border (1.x spec §24.2)
        Assert.Equal("│ hello   │", buf.ToLines()[1]);
        Assert.Equal((7, 1), buf.CursorRequest);
    }

    [Fact]
    public void ScrollsToFollowTheCursor_AndClampsAtTheBottom()
    {
        var state = new TextAreaState(string.Join('\n', Enumerable.Range(0, 10).Select(i => $"l{i}")));

        Assert.Equal(["l7", "l8", "l9"], Draw(state, 5, 3));
        Assert.Equal(7, state.ScrollOffset);

        state.Cursor = P(1, 0);
        Assert.Equal(["l1", "l2", "l3"], Draw(state, 5, 3));
        state.Cursor = P(2, 0);
        Assert.Equal(["l1", "l2", "l3"], Draw(state, 5, 3));
    }

    [Fact]
    public void NoWrap_ScrollsHorizontallyToTheCursor()
    {
        var state = new TextAreaState("abcdefghij\nxy");

        Assert.Equal(["abcde", "xy", ""], Draw(state, 5, 3, WrapMode.None));
        state.Cursor = P(0, 10);
        Assert.Equal(["ghij", "", ""], Draw(state, 5, 3, WrapMode.None));
        Assert.Equal((4, 0), DrawCursor(state, 5, 3, WrapMode.None));
        state.Cursor = P(1, 0);
        Assert.Equal(["abcde", "xy", ""], Draw(state, 5, 3, WrapMode.None));
    }

    // Mouse

    private static MouseEvent Click(int x, int y) => new(MouseAction.Press, MouseButton.Left, x, y);

    [Fact]
    public void Click_PlacesTheCursor_OnACharacter_PastARow_AndBelowTheText()
    {
        var state = new TextAreaState("aaaa bbbb\ncc");
        Draw(state, 6, 5);

        Assert.True(state.HandleMouse(Click(2, 0)));
        Assert.Equal(P(0, 2), state.Cursor);
        Assert.True(state.HandleMouse(Click(5, 1)));
        Assert.Equal(P(0, 9), state.Cursor);
        Assert.True(state.HandleMouse(Click(5, 0)));
        Assert.Equal(P(0, 4), state.Cursor); // past a wrapped row's end: before the wrap
        Assert.True(state.HandleMouse(Click(1, 4)));
        Assert.Equal(P(1, 2), state.Cursor);
    }

    [Fact]
    public void Click_SetsTheGoalColumn()
    {
        var state = new TextAreaState("abcdef\nabcdef");
        Draw(state, 10, 3);

        state.HandleMouse(Click(4, 0));
        Press(state, "down");

        Assert.Equal(P(1, 4), state.Cursor);
    }

    [Fact]
    public void Mouse_OutsideTheArea_OrBeforeADraw_IsNotHandled()
    {
        var state = new TextAreaState("abc");
        Assert.False(state.HandleMouse(Click(0, 0)));

        var buf = new CellBuffer(10, 4);
        Widget.TextArea().State(state).Block(Widget.Block()).Draw(buf, buf.Area);
        Assert.False(state.HandleMouse(Click(0, 0)));
        Assert.False(state.HandleMouse(new MouseEvent(MouseAction.Press, MouseButton.Right, 1, 1)));
        Assert.True(state.HandleMouse(Click(1, 1)));
    }

    [Fact]
    public void Wheel_ScrollsWithoutMovingTheCursor_UntilTheCursorMoves()
    {
        var state = new TextAreaState(string.Join('\n', Enumerable.Range(0, 10).Select(i => $"l{i}"))) { Cursor = P(0, 0) };
        Draw(state, 5, 3);

        Assert.True(state.HandleMouse(new MouseEvent(MouseAction.ScrollDown, MouseButton.None, 0, 0)));
        Assert.Equal(["l3", "l4", "l5"], Draw(state, 5, 3));
        Assert.Equal(P(0, 0), state.Cursor);
        state.HandleMouse(new MouseEvent(MouseAction.ScrollDown, MouseButton.None, 0, 0));
        state.HandleMouse(new MouseEvent(MouseAction.ScrollDown, MouseButton.None, 0, 0));
        Assert.Equal(7, state.ScrollOffset);

        Press(state, "right");
        Assert.Equal(["l0", "l1", "l2"], Draw(state, 5, 3));
    }

    // In an app

    private sealed class NotesScreen : Screen
    {
        public TextAreaState Notes { get; } = new();
        public int Enters { get; private set; }

        public NotesScreen() => Keys.Add("enter", "Submit", () => Enters++);

        public override bool CapturesText => true;
        public override bool HandleKey(ConsoleKeyInfo key) => Notes.HandleKey(key);
        public override bool HandlePaste(string text) => Notes.Paste(text);
        public override bool HandleMouse(MouseEvent mouse) => Notes.HandleMouse(mouse);
        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
            Widget.TextArea().State(Notes).Focused(ctx.Focused).Block(Widget.Block()).Draw(buf, area);
    }

    [Fact]
    public void InAScreen_EnterIsALineBreak_PasteAndClicksWork()
    {
        var screen = new NotesScreen();
        using var harness = new ComponentHarness(screen, 20, 6);

        harness.Type("hi");
        harness.Press("enter");
        harness.Paste("a\nb");
        harness.Click(3, 1);

        Assert.Equal("hi\na\nb", screen.Notes.Text);
        Assert.Equal(0, screen.Enters);
        Assert.Equal(P(0, 1), screen.Notes.Cursor);
        Assert.Equal("│ hi               │", harness.Lines[1]);
    }
    // ---- Changed (1.x spec §25.2) ----

    private static Func<int> CountChanges(TextAreaState state)
    {
        var changes = 0;
        state.Changed += (_, _) => changes++;
        return () => changes;
    }

    [Theory]
    [InlineData("backspace")]
    [InlineData("delete")]
    [InlineData("enter")]
    [InlineData("ctrl+w")]
    [InlineData("ctrl+u")]
    [InlineData("ctrl+k")]
    public void Changed_is_raised_once_for_each_edit(string key)
    {
        var state = new TextAreaState("one two\nthree");
        state.Cursor = new TextPosition(0, 4);
        var changes = CountChanges(state);
        Press(state, key);
        Assert.Equal(1, changes());
    }

    [Fact]
    public void Changed_is_raised_for_typing_pasting_inserting_undo_and_redo()
    {
        var state = new TextAreaState();
        var changes = CountChanges(state);
        Type(state, "ab");
        Assert.Equal(2, changes());
        Assert.True(state.Paste("c\nd"));
        state.Insert("e");
        Assert.Equal(4, changes());
        Assert.True(state.Undo());
        Assert.True(state.Redo());
        Assert.Equal(6, changes());
    }

    [Fact]
    public void Changed_is_raised_for_indenting_and_outdenting()
    {
        var state = new TextAreaState("    a\nb") { TabIndents = true };
        state.SelectAll();
        var changes = CountChanges(state);
        Press(state, "shift+tab");
        Press(state, "tab");
        Assert.Equal(2, changes());
    }

    [Fact]
    public void Changed_sees_the_state_after_the_edit()
    {
        var state = new TextAreaState("ab");
        string? seen = null;
        TextPosition? cursor = null;
        state.Changed += (_, _) => (seen, cursor) = (state.Text, state.Cursor);
        Type(state, "c");
        Assert.Equal("abc", seen);
        Assert.Equal(new TextPosition(0, 3), cursor);
    }

    [Fact]
    public void Changed_is_not_raised_for_moves_selection_or_edits_that_change_nothing()
    {
        var state = new TextAreaState("one\ntwo");
        state.Cursor = default;
        var changes = CountChanges(state);
        Press(state, "right", "down", "end", "ctrl+home", "shift+right", "ctrl+a");
        state.Cursor = default;
        Press(state, "backspace");                        // at the start
        state.Cursor = state.Cursor with { Line = 1, Index = 3 };
        Press(state, "delete");                           // at the end
        state.Insert("");
        Assert.False(state.Undo());                       // nothing to undo
        Assert.Equal(0, changes());
    }

    [Fact]
    public void Setting_Text_raises_Changed_only_for_different_text()
    {
        var state = new TextAreaState("same");
        var changes = CountChanges(state);
        state.Text = "same";
        Assert.Equal(0, changes());
        state.Text = "other";
        state.Clear();
        Assert.Equal(2, changes());
        state.Text = "";
        Assert.Equal(2, changes());
    }
}
