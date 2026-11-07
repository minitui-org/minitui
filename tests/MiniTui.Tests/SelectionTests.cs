using System.Text;

namespace MiniTui.Tests;

/// <summary>Selection, the clipboard and undo in text fields and text areas (1.x spec §10). The in-app clipboard is
/// process-wide, so every test that uses it is in this class, which xUnit runs one test at a time.</summary>
[Collection("Console")]
public sealed class SelectionTests : IDisposable
{
    public SelectionTests() => Clipboard.Reset();

    public void Dispose() => Clipboard.Reset();

    private static void Press(TextFieldState state, params string[] keys)
    {
        foreach (var key in keys) Assert.True(state.HandleKey(KeyBinding.Parse(key).ToKeyInfo()), key);
    }

    private static void Press(TextAreaState state, params string[] keys)
    {
        foreach (var key in keys) Assert.True(state.HandleKey(KeyBinding.Parse(key).ToKeyInfo()), key);
    }

    private static void Type(TextFieldState state, string text)
    {
        foreach (var c in text) Assert.True(state.HandleKey(new ConsoleKeyInfo(c, 0, false, false, false)));
    }

    private static void Type(TextAreaState state, string text)
    {
        foreach (var c in text) Assert.True(state.HandleKey(new ConsoleKeyInfo(c, 0, false, false, false)));
    }

    private static bool Handles(TextFieldState state, string key) => state.HandleKey(KeyBinding.Parse(key).ToKeyInfo());

    private static bool Handles(TextAreaState state, string key) => state.HandleKey(KeyBinding.Parse(key).ToKeyInfo());

    private static TextPosition P(int line, int index) => new(line, index);

    // Text field: selecting

    [Fact]
    public void Field_ShiftMoves_SetTheAnchorOnce_AndExtend()
    {
        var state = new TextFieldState("hello world");
        state.Cursor = 0;

        Press(state, "shift+right", "shift+right");
        Assert.Equal(0, state.SelectionAnchor);
        Assert.Equal("he", state.SelectedText);

        Press(state, "ctrl+shift+right");
        Assert.Equal("hello", state.SelectedText);

        Press(state, "shift+end");
        Assert.Equal("hello world", state.SelectedText);
        Assert.Equal((0, 11), (state.SelectionStart, state.SelectionEnd));
    }

    [Fact]
    public void Field_SelectingBackwards_OrdersStartAndEnd()
    {
        var state = new TextFieldState("hello world");

        Press(state, "ctrl+shift+left");

        Assert.Equal(11, state.SelectionAnchor);
        Assert.Equal(6, state.Cursor);
        Assert.Equal((6, 11), (state.SelectionStart, state.SelectionEnd));
        Assert.Equal("world", state.SelectedText);
    }

    [Fact]
    public void Field_ShiftBackToTheAnchor_ClearsTheSelection()
    {
        var state = new TextFieldState("ab");

        Press(state, "shift+left", "shift+right");

        Assert.False(state.HasSelection);
        Assert.Null(state.SelectionAnchor);
    }

    [Fact]
    public void Field_LeftAndRight_CollapseTheSelection()
    {
        var state = new TextFieldState("hello world");
        state.Select(2, 8);

        Press(state, "left");
        Assert.Equal(2, state.Cursor);
        Assert.False(state.HasSelection);

        state.Select(8, 2);
        Press(state, "right");
        Assert.Equal(8, state.Cursor);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void Field_OtherMoves_ClearTheSelection_AndMoveFromTheCursor()
    {
        var state = new TextFieldState("hello world");
        state.Select(2, 8);

        Press(state, "home");

        Assert.Equal(0, state.Cursor);
        Assert.False(state.HasSelection);

        state.Select(2, 8);
        Press(state, "ctrl+left");
        Assert.Equal(6, state.Cursor);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void Field_CtrlA_SelectsAll()
    {
        var state = new TextFieldState("hello");
        state.Cursor = 2;

        Press(state, "ctrl+a");

        Assert.Equal("hello", state.SelectedText);
        Assert.Equal(5, state.Cursor);
    }

    [Fact]
    public void Field_TypingAndPaste_ReplaceTheSelection()
    {
        var state = new TextFieldState("hello world");
        state.Select(0, 5);

        Type(state, "bye");
        Assert.Equal("bye world", state.Text);
        Assert.Equal(3, state.Cursor);
        Assert.False(state.HasSelection);

        state.Select(4, 9);
        state.Paste("there\n");
        Assert.Equal("bye there", state.Text);
    }

    [Theory]
    [InlineData("backspace")]
    [InlineData("delete")]
    [InlineData("ctrl+w")]
    [InlineData("ctrl+backspace")]
    [InlineData("ctrl+u")]
    [InlineData("ctrl+k")]
    public void Field_DeleteKeys_DeleteOnlyTheSelection(string key)
    {
        var state = new TextFieldState("one two three");
        state.Select(4, 7);

        Press(state, key);

        Assert.Equal("one  three", state.Text);
        Assert.Equal(4, state.Cursor);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void Field_Selection_NeverSplitsACluster()
    {
        var state = new TextFieldState("a👍🏽éb");
        state.Cursor = 0;

        Press(state, "right", "shift+right");
        Assert.Equal("👍🏽", state.SelectedText);

        Press(state, "shift+right");
        Assert.Equal("👍🏽é", state.SelectedText);

        // Both ends snap to the next boundary, as Cursor does
        state.Select(2, 6);
        Assert.Equal((5, 7), (state.SelectionStart, state.SelectionEnd));
    }

    [Fact]
    public void Field_Select_Clamps_AndEqualEndsClear()
    {
        var state = new TextFieldState("abc");

        state.Select(-5, 99);
        Assert.Equal("abc", state.SelectedText);

        state.Select(1, 1);
        Assert.False(state.HasSelection);
        Assert.Equal(1, state.Cursor);
    }

    [Fact]
    public void Field_SettingTextOrCursor_ClearsTheSelection()
    {
        var state = new TextFieldState("abc");
        state.SelectAll();
        state.Cursor = 1;
        Assert.False(state.HasSelection);

        state.SelectAll();
        state.Text = "xyz";
        Assert.False(state.HasSelection);

        state.SelectAll();
        state.ClearSelection();
        Assert.False(state.HasSelection);
        Assert.Equal(3, state.Cursor);
    }

    // Text field: clipboard

    [Fact]
    public void Field_CopyAndCut_UseTheClipboard_OnlyWithASelection()
    {
        var state = new TextFieldState("hello world");

        Assert.False(Handles(state, "ctrl+c"));
        Assert.False(Handles(state, "ctrl+x"));
        Assert.Null(Clipboard.GetText());

        state.Select(0, 5);
        Press(state, "ctrl+c");
        Assert.Equal("hello", Clipboard.GetText());
        Assert.Equal("hello world", state.Text);

        state.Select(5, 11);
        Press(state, "ctrl+x");
        Assert.Equal(" world", Clipboard.GetText());
        Assert.Equal("hello", state.Text);
    }

    [Fact]
    public void Field_CtrlV_PastesTheClipboard_OverTheSelection()
    {
        var state = new TextFieldState("hello world");
        Clipboard.SetText("big\nbad");

        state.Select(6, 11);
        Press(state, "ctrl+v");

        Assert.Equal("hello big bad", state.Text);
    }

    [Fact]
    public void Field_CtrlV_WithAnEmptyClipboard_IsHandled()
    {
        var state = new TextFieldState("abc");

        Press(state, "ctrl+v");

        Assert.Equal("abc", state.Text);
    }

    // Text field: undo

    [Fact]
    public void Field_TypingASentence_UndoesByWord()
    {
        var state = new TextFieldState();
        Type(state, "one two three");

        Press(state, "ctrl+z");
        Assert.Equal("one two ", state.Text);
        Press(state, "ctrl+z");
        Assert.Equal("one ", state.Text);
        Press(state, "ctrl+z");
        Assert.Equal("", state.Text);
        Assert.False(state.CanUndo);

        Press(state, "ctrl+y", "ctrl+y");
        Assert.Equal("one two ", state.Text);
        Assert.Equal(8, state.Cursor);
    }

    [Fact]
    public void Field_GroupBoundaries()
    {
        var state = new TextFieldState();
        Type(state, "abc");
        Press(state, "backspace", "backspace");   // a new kind of edit
        Press(state, "left");                     // a move
        Type(state, "x");

        Assert.Equal("xa", state.Text);
        Press(state, "ctrl+z");
        Assert.Equal("a", state.Text);
        Press(state, "ctrl+z");
        Assert.Equal("abc", state.Text);
        Press(state, "ctrl+z");
        Assert.Equal("", state.Text);
    }

    [Fact]
    public void Field_OneOffEdits_AreGroupsOfTheirOwn()
    {
        var state = new TextFieldState("one two");
        Press(state, "ctrl+w", "ctrl+w");
        state.Paste("a");
        state.Paste("b");

        Press(state, "ctrl+z");
        Assert.Equal("a", state.Text);
        Press(state, "ctrl+z", "ctrl+z");
        Assert.Equal("one ", state.Text);
    }

    [Fact]
    public void Field_Undo_RestoresTheCursorAndSelection()
    {
        var state = new TextFieldState("hello world");
        state.Select(0, 5);
        Type(state, "bye");

        Press(state, "ctrl+z");

        Assert.Equal("hello world", state.Text);
        Assert.Equal("hello", state.SelectedText);
        Assert.Equal(5, state.Cursor);
    }

    [Fact]
    public void Field_ReplacingASelection_IsUndoneWithTheTypingAfterIt()
    {
        var state = new TextFieldState("hello world");
        state.Select(0, 5);
        Type(state, "bye");

        Press(state, "ctrl+z");

        Assert.False(state.CanUndo);
    }

    [Fact]
    public void Field_ANewEdit_ClearsRedo()
    {
        var state = new TextFieldState();
        Type(state, "ab");
        Press(state, "ctrl+z");
        Assert.True(state.CanRedo);

        Type(state, "c");

        Assert.False(state.CanRedo);
        Assert.False(state.Redo());
    }

    [Fact]
    public void Field_SettingText_ClearsTheHistory()
    {
        var state = new TextFieldState();
        Type(state, "ab");

        state.Text = "new";

        Assert.False(state.CanUndo);
        Assert.False(state.Undo());
        Assert.True(Handles(state, "ctrl+z"));
        Assert.True(Handles(state, "ctrl+y"));
        Assert.Equal("new", state.Text);
    }

    [Fact]
    public void Field_History_KeepsAHundredGroups()
    {
        var state = new TextFieldState();
        for (var i = 0; i < 120; i++) state.Insert("x");

        var undone = 0;
        while (state.Undo()) undone++;

        Assert.Equal(100, undone);
        Assert.Equal(20, state.Text.Length);
    }

    [Fact]
    public void Field_EditsThatChangeNothing_AreNotRecorded()
    {
        var state = new TextFieldState("a");
        state.Cursor = 0;

        Press(state, "backspace", "ctrl+u", "end", "delete", "ctrl+k");

        Assert.False(state.CanUndo);
    }

    [Fact]
    public void CommandLine_GetsSelectionToo()
    {
        var state = new CommandLineState();
        Type(state, "quit now");

        Press(state, "ctrl+shift+left", "backspace");

        Assert.Equal("quit ", state.Text);
    }

    // Text field: drawing

    [Fact]
    public void Field_DrawsTheSelection_FocusedAndUnfocused()
    {
        var state = new TextFieldState("hello world");
        state.Select(0, 5);
        var theme = Theme.Default;

        var buf = new CellBuffer(20, 1);
        Widget.TextField().State(state).Focused().Draw(buf, buf.Area);
        Snapshot.AssertStyle(buf, 0, 0, theme.Selection.Fg!.Value, theme.Selection.Bg!.Value);
        Snapshot.AssertStyle(buf, 4, 0, theme.Selection.Fg!.Value, theme.Selection.Bg!.Value);
        Assert.NotEqual(theme.Selection.Bg, buf.Get(6, 0).Bg);
        Assert.Equal((5, 0), buf.CursorRequest);

        buf = new CellBuffer(20, 1);
        Widget.TextField().State(state).Draw(buf, buf.Area);
        Assert.Equal(theme.SelectionUnfocused.Bg, buf.Get(2, 0).Bg);
    }

    [Fact]
    public void Field_DrawsTheSelection_ScrolledAndWide()
    {
        var state = new TextFieldState("abcdef中文");
        state.Select(6, 8);

        var buf = new CellBuffer(6, 1);
        Widget.TextField().State(state).Focused().Draw(buf, buf.Area);

        // Scrolled so the cursor's column after the end shows: "f中文" from column 5
        Assert.Equal(Theme.Default.Selection.Bg, buf.Get(1, 0).Bg);
        Assert.Equal(Theme.Default.Selection.Bg, buf.Get(4, 0).Bg);
        Assert.NotEqual(Theme.Default.Selection.Bg, buf.Get(0, 0).Bg);
    }

    // Text area: selecting

    [Fact]
    public void Area_ShiftMoves_AcrossLines()
    {
        var state = new TextAreaState("one\ntwo\nthree");
        state.Cursor = P(0, 1);

        Press(state, "shift+down");
        Assert.Equal("ne\nt", state.SelectedText);

        Press(state, "ctrl+shift+end");
        Assert.Equal("ne\ntwo\nthree", state.SelectedText);
        Assert.Equal((P(0, 1), P(2, 5)), (state.SelectionStart, state.SelectionEnd));

        Press(state, "ctrl+shift+home");
        Assert.Equal("o", state.SelectedText);
        Assert.Equal(P(0, 0), state.Cursor);
    }

    [Fact]
    public void Area_ShiftUpDown_KeepTheGoalColumn()
    {
        var state = new TextAreaState("abcdef\nab\nabcdef");
        state.Cursor = P(0, 4);

        Press(state, "shift+down", "shift+down");

        Assert.Equal(P(2, 4), state.Cursor);
        Assert.Equal("ef\nab\nabcd", state.SelectedText);
    }

    [Fact]
    public void Area_ShiftPageDown_ByTheDrawnHeight()
    {
        var state = new TextAreaState(string.Join('\n', Enumerable.Range(0, 10).Select(i => $"l{i}")));
        state.Cursor = P(0, 0);
        var buf = new CellBuffer(10, 3);
        Widget.TextArea().State(state).Focused().Draw(buf, buf.Area);

        Press(state, "shift+pgdn");

        Assert.Equal(P(3, 0), state.Cursor);
        Assert.Equal("l0\nl1\nl2\n", state.SelectedText);

        Press(state, "shift+pgup");
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void Area_LeftRightCollapse_OtherMovesClear()
    {
        var state = new TextAreaState("one\ntwo");
        state.Select(P(0, 1), P(1, 2));

        Press(state, "left");
        Assert.Equal(P(0, 1), state.Cursor);
        Assert.False(state.HasSelection);

        state.Select(P(1, 2), P(0, 1));
        Press(state, "right");
        Assert.Equal(P(1, 2), state.Cursor);

        state.Select(P(0, 1), P(1, 2));
        Press(state, "up");
        Assert.Equal(P(0, 2), state.Cursor);
        Assert.False(state.HasSelection);
    }

    [Fact]
    public void Area_SelectedText_OverAnEmptyLine()
    {
        var state = new TextAreaState("a\n\nb");

        state.SelectAll();
        Assert.Equal("a\n\nb", state.SelectedText);

        state.Select(P(0, 1), P(1, 0));
        Assert.Equal("\n", state.SelectedText);
    }

    [Fact]
    public void Area_CuttingAcrossLines_JoinsThem()
    {
        var state = new TextAreaState("one\ntwo\nthree");
        state.Select(P(0, 2), P(2, 2));

        Press(state, "ctrl+x");

        Assert.Equal("onree", state.Text);
        Assert.Equal(P(0, 2), state.Cursor);
        Assert.Equal("e\ntwo\nth", Clipboard.GetText());
    }

    [Fact]
    public void Area_TypingPasteAndEnter_ReplaceTheSelection()
    {
        var state = new TextAreaState("one\ntwo");
        state.Select(P(0, 1), P(1, 1));

        Press(state, "enter");
        Assert.Equal("o\nwo", state.Text);

        state.Select(P(0, 0), P(1, 2));
        state.Paste("x\ny");
        Assert.Equal("x\ny", state.Text);

        state.SelectAll();
        Type(state, "z");
        Assert.Equal("z", state.Text);
    }

    [Theory]
    [InlineData("backspace")]
    [InlineData("delete")]
    [InlineData("ctrl+w")]
    [InlineData("ctrl+u")]
    [InlineData("ctrl+k")]
    public void Area_DeleteKeys_DeleteOnlyTheSelection(string key)
    {
        var state = new TextAreaState("one\ntwo");
        state.Select(P(0, 2), P(1, 1));

        Press(state, key);

        Assert.Equal("onwo", state.Text);
        Assert.Equal(P(0, 2), state.Cursor);
    }

    [Fact]
    public void Area_CtrlA_AndCopy()
    {
        var state = new TextAreaState("a\nb");

        Assert.False(Handles(state, "ctrl+c"));
        Press(state, "ctrl+a", "ctrl+c");

        Assert.Equal("a\nb", Clipboard.GetText());
        Assert.Equal(P(1, 1), state.Cursor);
    }

    [Fact]
    public void Area_CtrlV_KeepsLineBreaks()
    {
        var state = new TextAreaState("ab");
        state.Cursor = P(0, 1);
        Clipboard.SetText("1\n2");

        Press(state, "ctrl+v");

        Assert.Equal("a1\n2b", state.Text);
    }

    [Fact]
    public void Area_Select_ClampsAndSnaps()
    {
        var state = new TextAreaState("a👍🏽\nb");

        state.Select(P(-1, 0), P(0, 2));

        Assert.Equal(P(0, 5), state.Cursor);
        Assert.Equal("a👍🏽", state.SelectedText);

        state.Select(P(9, 9), P(9, 9));
        Assert.False(state.HasSelection);
        Assert.Equal(P(1, 1), state.Cursor);
    }

    [Fact]
    public void Area_ClickClearsTheSelection()
    {
        var state = new TextAreaState("one\ntwo");
        var buf = new CellBuffer(10, 3);
        Widget.TextArea().State(state).Draw(buf, buf.Area);
        state.SelectAll();

        Assert.True(state.HandleMouse(new MouseEvent(MouseAction.Press, MouseButton.Left, 1, 0)));

        Assert.False(state.HasSelection);
        Assert.Equal(P(0, 1), state.Cursor);
    }

    // Text area: undo

    [Fact]
    public void Area_Undo_ByWord_AndAcrossLines()
    {
        var state = new TextAreaState();
        Type(state, "one two");
        Press(state, "enter");
        Type(state, "three");

        Press(state, "ctrl+z");
        Assert.Equal("one two\n", state.Text);
        Press(state, "ctrl+z");
        Assert.Equal("one two", state.Text);
        Press(state, "ctrl+z");
        Assert.Equal("one ", state.Text);

        Press(state, "ctrl+y", "ctrl+y");
        Assert.Equal("one two\n", state.Text);
        Assert.Equal(P(1, 0), state.Cursor);
    }

    [Fact]
    public void Area_Undo_RestoresJoinedLines_AndTheSelection()
    {
        var state = new TextAreaState("one\ntwo\nthree");
        state.Select(P(0, 2), P(2, 2));
        Press(state, "backspace");

        Press(state, "ctrl+z");

        Assert.Equal("one\ntwo\nthree", state.Text);
        Assert.Equal((P(0, 2), P(2, 2)), (state.SelectionStart, state.SelectionEnd));
    }

    [Fact]
    public void Area_SettingText_ClearsTheHistory()
    {
        var state = new TextAreaState();
        Type(state, "ab");

        state.Text = "x";

        Assert.False(state.CanUndo);
        Assert.True(Handles(state, "ctrl+z"));
        Assert.Equal("x", state.Text);
    }

    [Fact]
    public void Area_DeleteKinds_GroupSeparately()
    {
        var state = new TextAreaState("abcdef");
        state.Cursor = P(0, 3);
        Press(state, "backspace", "backspace", "delete", "delete");

        Assert.Equal("af", state.Text);
        Press(state, "ctrl+z");
        Assert.Equal("adef", state.Text);
        Press(state, "ctrl+z");
        Assert.Equal("abcdef", state.Text);
    }

    // Text area: drawing

    [Fact]
    public void Area_DrawsTheSelection_AcrossRows_WithALineBreakCell()
    {
        var state = new TextAreaState("ab\n\ncd");
        state.Select(P(0, 1), P(2, 1));
        var sel = Theme.Default.Selection.Bg;

        var buf = new CellBuffer(6, 3);
        Widget.TextArea().State(state).Focused().Draw(buf, buf.Area);

        Assert.NotEqual(sel, buf.Get(0, 0).Bg);
        Assert.Equal(sel, buf.Get(1, 0).Bg);
        Assert.Equal(sel, buf.Get(2, 0).Bg);   // the line break
        Assert.NotEqual(sel, buf.Get(3, 0).Bg);
        Assert.Equal(sel, buf.Get(0, 1).Bg);   // the empty line's break
        Assert.NotEqual(sel, buf.Get(1, 1).Bg);
        Assert.Equal(sel, buf.Get(0, 2).Bg);
        Assert.NotEqual(sel, buf.Get(1, 2).Bg); // the cursor's cell, after the selection
        Assert.Equal((1, 2), buf.CursorRequest);
    }

    [Fact]
    public void Area_DrawsTheSelection_OnWrappedRows_AndUnfocused()
    {
        var state = new TextAreaState("aaaa bbbb");
        state.Select(P(0, 2), P(0, 7));
        var buf = new CellBuffer(5, 2);

        Widget.TextArea().State(state).Draw(buf, buf.Area);

        var sel = Theme.Default.SelectionUnfocused.Bg;
        Assert.Equal(sel, buf.Get(2, 0).Bg);
        Assert.Equal(sel, buf.Get(4, 0).Bg);
        Assert.Equal(sel, buf.Get(0, 1).Bg);
        Assert.Equal(sel, buf.Get(1, 1).Bg);
        Assert.NotEqual(sel, buf.Get(2, 1).Bg);
        Assert.NotEqual(sel, buf.Get(1, 0).Bg);
    }

    [Fact]
    public void Area_Selection_Monochrome()
    {
        var state = new TextAreaState("abc");
        state.Select(P(0, 0), P(0, 2));
        var buf = new CellBuffer(5, 1);

        using (Theme.Use(Theme.Monochrome)) Widget.TextArea().State(state).Focused().Draw(buf, buf.Area);

        Assert.True(buf.Get(0, 0).Modifiers.HasFlag(Modifiers.Reversed));
        Assert.True(buf.Get(1, 0).Modifiers.HasFlag(Modifiers.Reversed));
    }

    [Fact]
    public void TextPosition_Compares()
    {
        Assert.True(P(0, 5) < P(1, 0));
        Assert.True(P(1, 2) > P(1, 1));
        Assert.True(P(1, 1) <= P(1, 1));
        Assert.True(P(2, 0) >= P(1, 9));
        Assert.Equal(0, P(3, 3).CompareTo(P(3, 3)));
    }

    // The clipboard

    [Fact]
    public void Clipboard_KeepsTheInAppText_AndUsesTheTarget()
    {
        var written = new List<string>();
        Clipboard.SetText("one");
        Assert.Equal("one", Clipboard.GetText());

        using (Clipboard.Use(new Clipboard.Target(written.Add, () => "system")))
        {
            Clipboard.SetText("two");
            Assert.Equal(["two"], written);
            Assert.Equal("system", Clipboard.GetText());
        }

        using (Clipboard.Use(new Clipboard.Target(null, () => null)))
            Assert.Equal("two", Clipboard.GetText());
    }

    [Fact]
    public void AnsiBackend_WritesOsc52_OnlyWhileEntered()
    {
        var host = new FakeConsoleHost();
        var backend = new AnsiBackend(ColorMode.TrueColor, host);

        backend.WriteClipboard("not yet");
        Assert.Equal(0, host.Output.Length);

        backend.Enter();
        var before = host.Output.Length;
        backend.WriteClipboard("hé\n");
        var written = Encoding.ASCII.GetString(host.Output.ToArray()[(int)before..]);

        Assert.Equal("\e]52;c;" + Convert.ToBase64String(Encoding.UTF8.GetBytes("hé\n")) + "\a", written);
        backend.Dispose();
    }

    [Fact]
    public void Clipboard_For_PicksTheSystemClipboard()
    {
        Assert.Null(Clipboard.For(new TestBackend(10, 2)));
        var target = Clipboard.For(new AnsiBackend(ColorMode.TrueColor, new FakeConsoleHost()));
        Assert.NotNull(target?.Write);
        if (OperatingSystem.IsWindows()) Assert.NotNull(target.Read);
        else Assert.Null(target.Read);
    }

    // Ctrl+C in the app

    private sealed class Field(bool capturesText = true) : Component
    {
        public TextFieldState State { get; } = new("hello");
        public int KeyCount { get; private set; }
        public override bool CapturesText => capturesText;

        public override bool HandleKey(ConsoleKeyInfo key)
        {
            KeyCount++;
            return !capturesText || State.HandleKey(key);
        }

        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
            Widget.TextField().State(State).Focused().Draw(buf, area);
    }

    private sealed class FieldScreen : Screen
    {
        public FieldScreen(bool capturesText = true)
        {
            Field = AddChild(new Field(capturesText));
            Focus.Add(Field);
            Keys.Add("ctrl+c", "Bound", () => Bound++);
        }

        public Field Field { get; }
        public int Bound { get; private set; }

        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) => ctx.Render(Field, buf, area);
    }

    [Fact]
    public void CtrlC_CopiesWithASelection_AndQuitsWithout()
    {
        var screen = new FieldScreen();
        using var h = new ComponentHarness(screen, 20, 3);

        h.Press("ctrl+a");
        h.Press("ctrl+c");
        Assert.False(h.App.Stopping.IsCancellationRequested);
        Assert.Equal("hello", Clipboard.GetText());

        h.Press("end");
        h.Press("ctrl+c");
        Assert.True(h.App.Stopping.IsCancellationRequested);
        Assert.Equal(0, screen.Bound);
    }

    [Fact]
    public void CtrlC_Quits_WhenTheFocusedComponentDoesNotCaptureText_EvenIfItTakesEveryKey()
    {
        var screen = new FieldScreen(capturesText: false);
        using var h = new ComponentHarness(screen, 20, 3);

        h.Press("ctrl+c");

        Assert.True(h.App.Stopping.IsCancellationRequested);
        Assert.Equal(0, screen.Field.KeyCount);
        Assert.Equal(0, screen.Bound);
    }

    [Fact]
    public void CtrlC_CopiesInAnOverlay()
    {
        var screen = new FieldScreen(capturesText: false);
        using var h = new ComponentHarness(screen, 30, 5);
        var line = new CommandLineOverlay(new CommandRegistry());
        h.App.Overlays.Show(line);
        h.Type(":quit");

        h.Press("ctrl+shift+left");
        h.Press("ctrl+c");

        Assert.False(h.App.Stopping.IsCancellationRequested);
        Assert.Equal(":quit", Clipboard.GetText());
    }

    [Fact]
    public void CtrlZ_InATextField_Undoes_AndDoesNotSuspend()
    {
        var screen = new FieldScreen();
        using var h = new ComponentHarness(screen, 20, 3);
        h.App.WithSuspendKey();
        h.Type("!");

        h.Press("ctrl+z");

        Assert.Equal("hello", screen.Field.State.Text);
    }

    [Fact]
    public void TheAppsClipboardTarget_IsUsedOnTheUiThread()
    {
        var written = new List<string>();
        var screen = new FieldScreen();
        var app = App.Create().WithBackend(new TestBackend(20, 3)).WithInput(new ScriptedInputSource())
            .WithTickRate(null).CaptureConsoleOutput(false);
        app.ClipboardTarget = new Clipboard.Target(written.Add, null);
        app.Screens.Push(screen);
        app.StartRun(CancellationToken.None);
        try
        {
            app.InjectKeys([KeyBinding.Parse("ctrl+a").ToKeyInfo(), KeyBinding.Parse("ctrl+c").ToKeyInfo()]);
            using var ui = app.EnterUi();
            app.Setup();
            app.Step(wait: false);
        }
        finally
        {
            app.EndRun();
        }

        Assert.Equal(["hello"], written);
    }
    [Fact]
    public void A_text_areas_Changed_is_raised_for_cut_and_not_for_copy()
    {
        var state = new TextAreaState("hello world");
        state.Select(new TextPosition(0, 0), new TextPosition(0, 5));
        var changes = 0;
        state.Changed += (_, _) => changes++;
        Assert.True(state.Copy());
        Assert.Equal(0, changes);
        Assert.True(state.Cut());
        Assert.Equal(1, changes);
    }
}
