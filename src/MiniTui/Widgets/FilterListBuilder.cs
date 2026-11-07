namespace MiniTui;

/// <summary>
/// Draws a <see cref="FilterListState"/> (1.x spec §25.5): the filter line in the first row, a divider, and the
/// matching rows under it, the matched characters in bold, details and hints muted, and a scrollbar when they don't
/// all fit.
/// </summary>
public sealed class FilterListBuilder
{
    private readonly FilterListState _state;
    private BlockBuilder? _block;
    private string? _placeholder;
    private string _empty = "No matches";
    private bool _focused;

    internal FilterListBuilder(FilterListState state) => _state = state ?? throw new ArgumentNullException(nameof(state));

    /// <summary>
    /// Draw a block around the list. The divider under the filter line joins its side borders, and its padding is
    /// replaced: the rows and the filter line keep a column each side, and the selection spans the width.
    /// </summary>
    public FilterListBuilder Block(BlockBuilder? block) { _block = block; return this; }

    /// <summary>Muted text in the empty filter line.</summary>
    public FilterListBuilder Placeholder(string? text) { _placeholder = text; return this; }

    /// <summary>Shown when nothing matches. <c>"No matches"</c> by default.</summary>
    public FilterListBuilder Empty(string text) { _empty = text ?? ""; return this; }

    /// <summary>Show the filter line's cursor, the focused selection style, and the block's focused border.</summary>
    public FilterListBuilder Focused(bool focused = true) { _focused = focused; return this; }

    /// <summary>For the palette and the select's menu: focused, but in a block that isn't drawn focused.</summary>
    internal bool FocusesBlock { get; set; } = true;

    /// <summary>Draw the list over <paramref name="area"/>.</summary>
    public void Draw(CellBuffer buf, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buf);
        var theme = Theme.Current;
        var block = _block?.Clone().Padding(new Margin(0));
        if (block is not null && FocusesBlock) block.Focused(_focused || block.IsFocused);
        var inner = block?.Draw(buf, area) ?? area;
        if (inner.IsEmpty)
        {
            _state.ClearLayout();
            return;
        }

        var field = new Rect(inner.X + 1, inner.Y, Math.Max(0, inner.Width - 2), 1);
        TextFieldRenderer.Draw(buf, field, _state.Filter, _focused, theme.Text, _placeholder);
        if (inner.Height < 2)
        {
            _state.ClearLayout();
            return;
        }

        var border = block?.BorderStyleFor(theme) ?? theme.Border;
        WidgetText.HorizontalLine(buf, inner.X, inner.Y + 1, inner.Width, border);
        if (inner.X > area.X) buf.SetString(area.X, inner.Y + 1, "├", border, 1);
        if (inner.Right < area.Right) buf.SetString(area.Right - 1, inner.Y + 1, "┤", border, 1);

        var rows = new Rect(inner.X, inner.Y + 2, inner.Width, Math.Max(0, inner.Height - 2));
        if (_state.MatchCount == 0)
        {
            _state.Layout(rows);
            _state.ClearLayout();
            if (rows.Height > 0) WidgetText.DrawString(buf, rows.X + 1, rows.Y, _empty, Math.Max(0, rows.Width - 2), theme.Text.Patch(theme.TextMuted));
            return;
        }

        var offset = _state.Layout(rows);
        var visible = Math.Clamp(_state.MatchCount - offset, 0, rows.Height);
        var selection = _focused ? theme.Selection : theme.SelectionUnfocused;
        for (var i = 0; i < visible; i++)
            DrawRow(buf, rows.X, rows.Y + i, rows.Width, offset + i, theme, selection);

        if (_state.MatchCount > rows.Height)
        {
            // On the right border, or in the last column without one
            var x = inner.Right < area.Right ? area.Right - 1 : inner.Right - 1;
            new ScrollbarBuilder().ContentLength(_state.MatchCount).ViewportLength(rows.Height).Position(offset)
                .Draw(buf, new Rect(x, rows.Y, 1, rows.Height));
        }
    }

    private void DrawRow(CellBuffer buf, int x, int y, int width, int index, Theme theme, Style selection)
    {
        var (item, match) = _state.MatchAt(index);
        var row = _state.Rows[item];
        var label = _state.LabelOf(item);
        var muted = theme.Text.Patch(theme.TextMuted);

        var hint = row.Hint is { Length: > 0 } h ? Sanitizer.SingleLine(h) : "";
        var hintWidth = Math.Min(UnicodeWidth.Of(hint), Math.Max(0, width - 2));
        var available = width - 2 - (hintWidth > 0 ? hintWidth + 2 : 0);
        var labelWidth = WidgetText.DrawString(buf, x + 1, y, label, Math.Max(0, available), theme.Text);

        var detail = row.Detail is { Length: > 0 } d ? Sanitizer.SingleLine(d) : "";
        var detailWidth = available - labelWidth - 2;
        if (detail.Length > 0 && detailWidth > 0) WidgetText.DrawString(buf, x + 1 + labelWidth + 2, y, detail, detailWidth, muted);
        if (hintWidth > 0) WidgetText.DrawString(buf, x + width - 1 - hintWidth, y, hint, hintWidth, muted);

        var selected = index == _state.SelectedIndex;
        if (selected) buf.SetStyle(new Rect(x, y, width, 1), selection);

        // Matched characters, inside the part of the label that was drawn
        var highlight = selected ? new Style(Add: Modifiers.Bold) : theme.Title.Bold();
        foreach (var position in match.Positions)
        {
            if (position >= label.Length) continue;
            var column = UnicodeWidth.Of(label.AsSpan(0, position));
            var cellWidth = Math.Max(1, UnicodeWidth.Of(label.AsSpan(position, char.IsHighSurrogate(label[position]) && position + 1 < label.Length ? 2 : 1)));
            if (column + cellWidth > labelWidth) continue;
            buf.SetStyle(new Rect(x + 1 + column, y, cellWidth, 1), highlight);
        }
    }
}
