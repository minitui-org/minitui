namespace MiniTui.Tests;

/// <summary>The public filterable list (1.x spec §25.5). The palette's and the select's tests cover the rest.</summary>
public sealed class FilterListTests
{
    private static readonly FilterRow[] Notes =
    [
        new("Shopping", Hint: "10:00", Key: 1),
        new("Work plan", Hint: "Mar 3", Key: 2),
        new("Holiday", Hint: "2025-06-01", Key: 3),
    ];

    private static void Press(FilterListState state, params string[] keys)
    {
        foreach (var key in keys) state.HandleKey(KeyBinding.Parse(key).ToKeyInfo());
    }

    private static void Type(FilterListState state, string text)
    {
        foreach (var c in text) state.HandleKey(new ConsoleKeyInfo(c, 0, false, false, false));
    }

    private static string[] Draw(FilterListState state, int width = 30, int height = 7, BlockBuilder? block = null, bool focused = true,
        string? placeholder = null, CellBuffer? into = null)
    {
        var buf = into ?? new CellBuffer(width, height);
        Widget.FilterList(state).Block(block).Focused(focused).Placeholder(placeholder).Draw(buf, buf.Area);
        return [.. buf.ToLines().Select(l => l.TrimEnd())];
    }

    [Fact]
    public void An_empty_filter_shows_every_row_in_its_own_order_with_the_first_selected()
    {
        var state = new FilterListState(Notes);
        Assert.Equal([0, 1, 2], state.Matches);
        Assert.Equal(0, state.Selected);
    }

    [Fact]
    public void Typing_filters_best_first_and_selects_the_best_match()
    {
        var state = new FilterListState(Notes);
        Press(state, "down", "down");
        Type(state, "pl");
        Assert.Equal([1], state.Matches);
        Assert.Equal(1, state.Selected);
        Type(state, "zz");
        Assert.Empty(state.Matches);
        Assert.Null(state.Selected);
    }

    [Fact]
    public void Setting_the_filter_text_filters_too()
    {
        var state = new FilterListState(Notes);
        state.Filter.Text = "hol";
        Assert.Equal([2], state.Matches);
        Assert.Equal([2], new FilterListState(Notes, "hol").Matches);
    }

    [Fact]
    public void SetRows_keeps_the_selection_on_its_key()
    {
        var state = new FilterListState(Notes);
        Press(state, "down");                       // Work plan
        state.SetRows([new("Groceries", Key: 4), .. Notes.Reverse()]);
        Assert.Equal("Work plan", state.Rows[state.Selected!.Value].Label);

        state.SetRows([new("Work plan (renamed)", Key: 2), new("Holiday", Key: 3)]);
        Assert.Equal("Work plan (renamed)", state.Rows[state.Selected!.Value].Label);
    }

    [Fact]
    public void SetRows_falls_back_to_the_best_match_when_the_row_is_gone_or_filtered_out()
    {
        var state = new FilterListState(Notes);
        Press(state, "down");
        state.SetRows([Notes[0], Notes[2]]);
        Assert.Equal(0, state.Selected);

        var filtered = new FilterListState(Notes, "o");
        filtered.Select(2);
        filtered.SetRows([Notes[0], Notes[1], Notes[2] with { Label = "Trip" }]);
        Assert.Equal(filtered.Matches[0], filtered.Selected);
    }

    [Fact]
    public void Rows_without_keys_are_kept_by_label()
    {
        var state = new FilterListState([new("a"), new("b"), new("c")]);
        Press(state, "down");
        state.SetRows([new("c"), new("b"), new("a")]);
        Assert.Equal(1, state.Selected);
    }

    [Fact]
    public void Select_is_ignored_for_a_row_that_does_not_match()
    {
        var state = new FilterListState(Notes, "shop");
        state.Select(2);
        Assert.Equal(0, state.Selected);
        state.Select(0);
        Assert.Equal(0, state.Selected);
    }

    [Theory]
    [InlineData(false, 0, 2)]
    [InlineData(true, 2, 0)]
    public void Arrows_wrap_only_with_Wraps(bool wraps, int afterUp, int afterDownFromLast)
    {
        var state = new FilterListState(Notes) { Wraps = wraps };
        Press(state, "up");
        Assert.Equal(afterUp, state.Selected);
        Press(state, "ctrl+end", "down");
        Assert.Equal(afterDownFromLast, state.Selected);
    }

    [Fact]
    public void Ctrl_home_and_end_move_the_selection_while_home_and_end_move_the_cursor()
    {
        var state = new FilterListState(Notes, "o");
        Press(state, "ctrl+end");
        Assert.Equal(state.Matches[^1], state.Selected);
        Press(state, "ctrl+home");
        Assert.Equal(state.Matches[0], state.Selected);
        Press(state, "home");
        Assert.Equal(0, state.Filter.Cursor);
    }

    [Fact]
    public void Esc_clears_the_filter_and_otherwise_is_left_to_the_caller_as_are_enter_and_tab()
    {
        var state = new FilterListState(Notes, "pl");
        Assert.True(state.HandleKey(KeyBinding.Parse("esc").ToKeyInfo()));
        Assert.Equal("", state.Filter.Text);
        Assert.Equal(3, state.Matches.Count);
        Assert.False(state.HandleKey(KeyBinding.Parse("esc").ToKeyInfo()));
        Assert.False(state.HandleKey(KeyBinding.Parse("enter").ToKeyInfo()));
        Assert.False(state.HandleKey(KeyBinding.Parse("tab").ToKeyInfo()));
    }

    [Fact]
    public void A_paste_filters()
    {
        var state = new FilterListState(Notes);
        Assert.True(state.HandlePaste("work"));
        Assert.Equal([1], state.Matches);
    }

    [Fact]
    public void SelectionChanged_is_raised_when_the_selected_row_changes()
    {
        var state = new FilterListState(Notes);
        var changes = 0;
        state.SelectionChanged += (_, _) => changes++;

        Press(state, "up");                    // at the top already, no wrap
        Assert.Equal(0, changes);
        Press(state, "down");
        Assert.Equal(1, changes);
        Type(state, "w");                      // still Work plan, the best match
        Assert.Equal(1, changes);
        Type(state, "zz");                     // nothing
        Assert.Equal(2, changes);
        state.Filter.Clear();                  // the first
        state.Select(2);
        Assert.Equal(4, changes);
        state.SetRows(Notes);                  // the same row
        Assert.Equal(4, changes);
        state.SetRows(Notes[..1]);
        Assert.Equal(5, changes);
    }

    [Fact]
    public void A_click_selects_a_row_a_double_click_activates_it_and_the_wheel_moves()
    {
        var state = new FilterListState(Notes);
        var activated = 0;
        state.Activated += (_, _) => activated++;
        Draw(state);                           // rows from y = 2

        state.HandleMouse(new MouseEvent(MouseAction.Press, MouseButton.Left, 3, 4));
        Assert.Equal(2, state.Selected);
        Assert.Equal(0, activated);
        state.HandleMouse(new MouseEvent(MouseAction.Press, MouseButton.Left, 3, 3) { ClickCount = 2 });
        Assert.Equal(1, state.Selected);
        Assert.Equal(1, activated);
        state.HandleMouse(new MouseEvent(MouseAction.ScrollUp, MouseButton.None, 3, 3));
        Assert.Equal(0, state.Selected);
    }

    [Fact]
    public void Without_a_block_it_draws_the_filter_a_divider_and_the_rows_with_their_hints()
    {
        var lines = Draw(new FilterListState(Notes), placeholder: "Filter");
        Assert.Equal(" Filter", lines[0]);
        Assert.Equal(new string('─', 30), lines[1]);
        Assert.Equal(" Shopping               10:00", lines[2]);
        Assert.Equal(" Holiday           2025-06-01", lines[4]);
    }

    [Fact]
    public void A_block_is_joined_by_the_divider()
    {
        var lines = Draw(new FilterListState(Notes), block: Widget.Block().Title("Notes"));
        Assert.StartsWith("┌Notes", lines[0], StringComparison.Ordinal);
        Assert.Equal("├" + new string('─', 28) + "┤", lines[2]);
        Assert.Equal("│ Shopping             10:00 │", lines[3]);
    }

    [Fact]
    public void The_selection_is_drawn_focused_or_not_across_the_row()
    {
        var state = new FilterListState(Notes);
        var focused = new CellBuffer(30, 7);
        Draw(state, into: focused);
        var unfocused = new CellBuffer(30, 7);
        Draw(state, focused: false, into: unfocused);

        Assert.Equal(Theme.Current.Selection.Bg, focused.Get(0, 2).Bg);
        Assert.Equal(Theme.Current.Selection.Bg, focused.Get(29, 2).Bg);
        Assert.Equal(Theme.Current.SelectionUnfocused.Bg, unfocused.Get(0, 2).Bg);
    }

    [Fact]
    public void Nothing_matching_shows_the_empty_text()
    {
        var buf = new CellBuffer(30, 5);
        Widget.FilterList(new FilterListState(Notes, "zzz")).Empty("No notes match").Draw(buf, buf.Area);
        Assert.Equal(" No notes match", buf.ToLines()[2].TrimEnd());
        Assert.Equal(" No matches", Draw(new FilterListState(Notes, "zzz"))[2]);
    }

    [Fact]
    public void More_rows_than_fit_scroll_with_the_selection_and_show_a_scrollbar()
    {
        var state = new FilterListState(Enumerable.Range(1, 20).Select(i => new FilterRow($"note {i}")));
        Press(state, "ctrl+end");
        var lines = Draw(state);
        Assert.Contains("note 20", lines[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, l => l.Contains("note 1 ", StringComparison.Ordinal));
        Press(state, "pgup");
        Assert.Equal(14, state.Selected);       // a page of 5 up
    }

    [Fact]
    public void A_focused_list_draws_its_block_focused_but_the_palettes_border_stays_modal()
    {
        using var _ = Theme.Use(Theme.Monochrome);       // where a focused border is bold
        var own = new CellBuffer(30, 7);
        Draw(new FilterListState(Notes), block: Widget.Block(), into: own);
        Assert.True(own.Get(0, 0).Modifiers.HasFlag(Modifiers.Bold));

        var palette = new CellBuffer(30, 7);
        FilterMenu.Draw(new FilterListState(Notes), palette, palette.Area, " Commands ", null);
        Assert.False(palette.Get(0, 0).Modifiers.HasFlag(Modifiers.Bold));
    }
}
