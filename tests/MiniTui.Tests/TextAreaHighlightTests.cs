namespace MiniTui.Tests;

/// <summary>Styled ranges in a text area (1.x spec §25.6).</summary>
public sealed class TextAreaHighlightTests
{
    private static readonly Style Red = new(Color.Red);
    private static readonly Style Bold = Style.Default.Bold();

    private static CellBuffer Draw(TextAreaState state, Func<int, string, IEnumerable<StyledRange>> highlight, int width = 20, int height = 4,
        bool focused = false, WrapMode wrap = WrapMode.Word)
    {
        var buf = new CellBuffer(width, height);
        using var _ = Theme.Use(Theme.Default);
        Widget.TextArea().State(state).Highlight(highlight).Focused(focused).Wrap(wrap).Draw(buf, buf.Area);
        return buf;
    }

    private static bool IsRed(CellBuffer buf, int x, int y) => buf.Get(x, y).Fg == Color.Red;

    [Fact]
    public void A_range_styles_its_cells_and_nothing_else()
    {
        var buf = Draw(new TextAreaState("# Title\nbody"), (line, text) => line == 0 ? [new StyledRange(0, text.Length, Red)] : []);
        Assert.All(Enumerable.Range(0, 7), x => Assert.True(IsRed(buf, x, 0), $"column {x}"));
        Assert.False(IsRed(buf, 7, 0));
        Assert.All(Enumerable.Range(0, 4), x => Assert.False(IsRed(buf, x, 1)));
    }

    [Fact]
    public void Ranges_patch_each_other_and_the_style()
    {
        var buf = Draw(new TextAreaState("a *word*"), (_, _) => [new StyledRange(2, 6, Red), new StyledRange(3, 4, Bold)]);
        Assert.True(IsRed(buf, 2, 0));
        Assert.False(buf.Get(2, 0).Modifiers.HasFlag(Modifiers.Bold));
        Assert.True(IsRed(buf, 4, 0));
        Assert.True(buf.Get(4, 0).Modifiers.HasFlag(Modifiers.Bold));
    }

    [Fact]
    public void The_selection_and_search_matches_are_drawn_over_ranges()
    {
        var state = new TextAreaState("alpha beta");
        state.Select(new TextPosition(0, 0), new TextPosition(0, 2));
        var buf = Draw(state, (_, text) => [new StyledRange(0, text.Length, Red)], focused: true);
        Assert.Equal(Theme.Default.Selection.Fg, buf.Get(0, 0).Fg);
        Assert.Equal(Theme.Default.Selection.Bg, buf.Get(0, 0).Bg);
        Assert.True(IsRed(buf, 3, 0));

        var finding = new TextAreaState("alpha beta");
        finding.OpenFind();
        finding.FindText = "beta";
        var found = Draw(finding, (_, text) => [new StyledRange(0, text.Length, Red)], focused: true);
        Assert.False(IsRed(found, 6, 0));        // the current match (drawn as the selection) is over the range
        Assert.True(IsRed(found, 0, 0));
    }

    [Fact]
    public void Ranges_off_the_line_or_empty_are_ignored_and_others_are_clamped()
    {
        var buf = Draw(new TextAreaState("short"), (_, _) =>
            [new StyledRange(-3, 5, Red), new StyledRange(4, 50, Red), new StyledRange(9, 2, Bold), new StyledRange(2, 0, Bold), new StyledRange(1, -4, Bold)]);
        Assert.True(IsRed(buf, 0, 0));
        Assert.True(IsRed(buf, 1, 0));
        Assert.False(IsRed(buf, 2, 0));
        Assert.True(IsRed(buf, 4, 0));
        Assert.False(IsRed(buf, 5, 0));
        Assert.All(Enumerable.Range(0, 20), x => Assert.False(buf.Get(x, 0).Modifiers.HasFlag(Modifiers.Bold)));
    }

    [Fact]
    public void A_range_that_cuts_a_wide_character_covers_all_of_it()
    {
        // "a😀b": the emoji is two code units and two columns
        foreach (var cut in new[] { new StyledRange(2, 1, Red), new StyledRange(1, 1, Red) })     // its second half, its first
        {
            var buf = Draw(new TextAreaState("a😀b"), (_, _) => [cut]);
            Assert.False(IsRed(buf, 0, 0));
            Assert.True(IsRed(buf, 1, 0));
            Assert.True(IsRed(buf, 2, 0));
            Assert.False(IsRed(buf, 3, 0));
        }
    }

    [Fact]
    public void A_wrapped_line_is_asked_for_once_and_styled_on_every_row()
    {
        var asked = new List<int>();
        var buf = Draw(new TextAreaState("one two three four five six"), (line, text) =>
        {
            asked.Add(line);
            return [new StyledRange(0, text.Length, Red)];
        }, width: 10);
        Assert.Equal([0], asked);
        Assert.True(IsRed(buf, 0, 1));
        Assert.True(IsRed(buf, 0, 2));
    }

    [Fact]
    public void Only_visible_lines_are_asked_for_with_their_index()
    {
        var state = new TextAreaState(string.Join('\n', Enumerable.Range(0, 30).Select(i => $"line {i}")));
        var asked = new List<(int, string)>();
        Draw(state, (line, text) =>
        {
            asked.Add((line, text));
            return [];
        });
        Assert.Equal([(26, "line 26"), (27, "line 27"), (28, "line 28"), (29, "line 29")], asked);   // the cursor is at the end
    }

    [Fact]
    public void Ranges_scroll_with_the_text_without_wrapping()
    {
        var state = new TextAreaState(new string('x', 30) + "END");
        var buf = Draw(state, (_, text) => [new StyledRange(text.Length - 3, 3, Red)], width: 10, wrap: WrapMode.None, focused: true);
        var red = Enumerable.Range(0, 10).Where(x => IsRed(buf, x, 0)).ToArray();
        Assert.Equal(3, red.Length);
        Assert.Equal("END", string.Concat(red.Select(x => buf.Get(x, 0).Symbol)));
    }
}
