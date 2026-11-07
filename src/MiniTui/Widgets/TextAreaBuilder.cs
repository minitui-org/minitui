using System.Globalization;

namespace MiniTui;

/// <summary>A styled range on one line of a text area (1.x spec §25.6).</summary>
/// <param name="Start">The first UTF-16 code unit in the line.</param>
/// <param name="Length">How many UTF-16 code units.</param>
/// <param name="Style">Patched over the cells' style, so a foreground color alone keeps the background.</param>
public readonly record struct StyledRange(int Start, int Length, Style Style);

/// <summary>
/// A multi-line text input (1.x spec §6). Long lines wrap; the view scrolls vertically to keep the cursor visible,
/// and when focused shows the terminal cursor at the insertion point.
/// </summary>
public sealed class TextAreaBuilder
{
    private TextAreaState? _state;
    private string? _placeholder;
    private bool _focused;
    private BlockBuilder? _block;
    private Style? _style;
    private WrapMode _wrap = WrapMode.Word;
    private bool _lineNumbers;
    private Func<int, string, IEnumerable<StyledRange>>? _highlight;

    /// <summary>The text and cursor.</summary>
    public TextAreaBuilder State(TextAreaState state) { _state = state; return this; }

    /// <summary>Muted text shown on the first row while the text is empty.</summary>
    public TextAreaBuilder Placeholder(string? placeholder) { _placeholder = placeholder; return this; }

    /// <summary>Show the cursor and use the focused styles.</summary>
    public TextAreaBuilder Focused(bool focused = true) { _focused = focused; return this; }

    /// <summary>Draw a block around the text area. Its border shows focus.</summary>
    public TextAreaBuilder Block(BlockBuilder block) { _block = block; return this; }

    /// <summary>A style patched over the theme's input style.</summary>
    public TextAreaBuilder Style(Style style) { _style = style; return this; }

    /// <summary>
    /// How long lines break: <see cref="WrapMode.Word"/> (the default) at whitespace, <see cref="WrapMode.Char"/>
    /// anywhere, or <see cref="WrapMode.None"/>, which scrolls the view horizontally instead.
    /// </summary>
    public TextAreaBuilder Wrap(WrapMode wrap) { _wrap = wrap; return this; }

    /// <summary>
    /// Show line numbers in a gutter on the left (1.x spec §16.4). The text wraps in the width left over; the gutter
    /// isn't drawn when that would leave no room for text.
    /// </summary>
    public TextAreaBuilder LineNumbers(bool show = true) { _lineNumbers = show; return this; }

    /// <summary>
    /// Style parts of each line (1.x spec §25.6): <paramref name="highlight"/> gets a line's index and text and returns
    /// the ranges to style. It's called once per visible line at each draw, so it should be fast, caching per line if
    /// it needs to. Ranges are drawn under the search matches, the selection and the cursor; ranges past the line or
    /// empty are ignored, and their ends move out to the edges of the characters they cut.
    /// </summary>
    public TextAreaBuilder Highlight(Func<int, string, IEnumerable<StyledRange>>? highlight) { _highlight = highlight; return this; }

    /// <summary>
    /// Draw the text area inside its block (or area). With a block that has a right border, a scrollbar is drawn on it
    /// when the text doesn't fit.
    /// </summary>
    public void Draw(CellBuffer buf, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buf);
        var inner = _block?.Clone().Focused(_focused || _block.IsFocused).Draw(buf, area) ?? area;
        var state = _state ?? new TextAreaState();
        var theme = Theme.Current;
        var style = (_focused ? theme.InputFocused : theme.Input).Patch(_style ?? default);
        if (inner.IsEmpty)
        {
            state.Track(inner, _wrap, 0);
            return;
        }
        // 1.x spec §24.2: the area's style and its clicks reach across the block's padding
        var band = _block?.AcrossPadding(area, inner) ?? inner;
        buf.SetStyle(band, style);

        // 1.x spec §16.5: the find bar takes the last row
        var finding = state.IsFinding && inner.Height >= 2;
        if (finding)
        {
            DrawFindBar(buf, inner with { Y = inner.Bottom - 1, Height = 1 }, state, style, theme);
            inner = inner with { Height = inner.Height - 1 };
        }

        // 1.x spec §16.4: as wide as the last line's number, at least 2 digits, then a space
        var gutterWidth = _lineNumbers ? Math.Max(2, Digits(state.Lines.Count)) + 1 : 0;
        if (gutterWidth >= inner.Width) gutterWidth = 0;
        var gutter = inner with { Width = gutterWidth };
        inner = inner with { X = inner.X + gutterWidth, Width = inner.Width - gutterWidth };

        var width = _wrap == WrapMode.None ? int.MaxValue : inner.Width;
        var rows = TextAreaLayout.Build(state.Lines, width, _wrap, state.Cursor);
        state.Track(inner, _wrap, rows.Count, gutter);
        state.HitArea = band with { Y = inner.Y, Height = inner.Height };
        var cursor = state.Cursor;
        var cursorRow = TextAreaLayout.RowOf(rows, cursor);
        var cursorColumn = TextAreaState.ColumnIn(state.Lines[cursor.Line], rows[cursorRow], cursor.Index);
        Scroll(state, rows.Count, cursorRow, cursorColumn, inner);
        if (_block?.HasRightBorder == true && rows.Count > inner.Height)
        {
            var bar = new Rect(area.Right - 1, inner.Y, 1, inner.Height);
            new ScrollbarBuilder().ContentLength(rows.Count).ViewportLength(inner.Height).Position(state.ScrollOffset).Draw(buf, bar);
            state.TrackScrollbar(bar);
        }
        if (gutterWidth > 0) DrawGutter(buf, gutter, rows, state, style, theme);

        var empty = state.Lines.Count == 1 && state.Lines[0].Length == 0;
        if (empty && !string.IsNullOrEmpty(_placeholder))
        {
            WidgetText.DrawString(buf, inner.X, inner.Y, _placeholder, inner.Width, style.Patch(theme.TextMuted));
        }
        else
        {
            var left = _wrap == WrapMode.None ? state.HorizontalOffset : 0;
            var selection = _focused ? theme.Selection : theme.SelectionUnfocused;
            var matches = finding ? state.Matches : [];
            var highlighted = new Dictionary<int, List<StyledRange>>();
            for (var y = 0; y < inner.Height && state.ScrollOffset + y < rows.Count; y++)
            {
                var visible = rows[state.ScrollOffset + y];
                DrawRow(buf, inner.X, inner.Y + y, state.Lines, visible, left, inner.Width, style);
                if (_highlight is not null)
                {
                    foreach (var range in HighlightsOf(visible.Line, state.Lines[visible.Line], highlighted))
                    {
                        DrawRange(buf, inner, inner.Y + y, state, visible, left, new TextPosition(visible.Line, range.Start),
                            new TextPosition(visible.Line, range.Start + range.Length), range.Style);
                    }
                }
                foreach (var match in matches)
                {
                    if (match.Start.Line == visible.Line)
                        DrawRange(buf, inner, inner.Y + y, state, visible, left, match.Start, match.End, theme.SearchMatch);
                }
                if (state.HasSelection) DrawSelection(buf, inner, inner.Y + y, state, visible, left, selection);
            }
        }

        if (!_focused || finding) return;
        var x = inner.X + cursorColumn - (_wrap == WrapMode.None ? state.HorizontalOffset : 0);
        var row = cursorRow - state.ScrollOffset;
        if (row < 0 || row >= inner.Height) return;

        // Spaces hanging past the edge at a wrap point put the cursor in the last column
        x = Math.Min(x, inner.Right - 1);
        buf.SetStyle(new Rect(x, inner.Y + row, 1, 1), theme.Cursor);
        buf.SetCursor(x, inner.Y + row);
    }

    // The line's ranges, asked for once per draw (a wrapped line has several rows), clamped and snapped to clusters
    private List<StyledRange> HighlightsOf(int index, string line, Dictionary<int, List<StyledRange>> cache)
    {
        if (cache.TryGetValue(index, out var ranges)) return ranges;
        ranges = [];
        foreach (var range in _highlight!(index, line) ?? [])
        {
            // Empty, before the line or past it; ClusterStart takes a negative start to 0
            var end = (int)Math.Min((long)range.Start + range.Length, line.Length);
            if (end <= Math.Max(0, range.Start)) continue;
            var start = ClusterStart(line, range.Start);
            end = TextFieldState.SnapToBoundary(line, end);
            ranges.Add(range with { Start = start, Length = end - start });
        }
        cache[index] = ranges;
        return ranges;
    }

    // The start of the cluster holding the code unit at index
    private static int ClusterStart(string line, int index)
    {
        var start = 0;
        while (start < line.Length)
        {
            var next = start + StringInfo.GetNextTextElementLength(line, start);
            if (next > index) return start;
            start = next;
        }
        return line.Length;
    }

    // "Find: " and the field, with "3 of 12", "12 matches" or "No matches" on the right
    private static void DrawFindBar(CellBuffer buf, Rect bar, TextAreaState state, Style style, Theme theme)
    {
        const string Label = "Find: ";
        var count = state.FindText.Length == 0 ? ""
            : state.MatchCount == 0 ? "No matches"
            : state.MatchIndex >= 0 ? $"{state.MatchIndex + 1} of {state.MatchCount}"
            : $"{state.MatchCount} matches";
        var countWidth = count.Length == 0 ? 0 : count.Length + 1;
        var x = bar.X;
        if (bar.Width > Label.Length + countWidth)
        {
            buf.SetString(x, bar.Y, Label, style.Patch(theme.Prompt));
            x += Label.Length;
        }
        else
        {
            countWidth = 0;
        }
        if (countWidth > 0) buf.SetString(bar.Right - count.Length, bar.Y, count, style.Patch(theme.TextMuted));
        var field = new Rect(x, bar.Y, Math.Max(0, bar.Right - x - countWidth), 1);
        TextFieldRenderer.Draw(buf, field, state.FindField, true, style, null);
    }

    private static int Digits(int n) => n < 10 ? 1 : 1 + Digits(n / 10);

    // A line's number on its first row, blank on the rest; the cursor's line in the text style, the others muted
    private static void DrawGutter(CellBuffer buf, Rect gutter, List<TextAreaLayout.Row> rows, TextAreaState state, Style style, Theme theme)
    {
        for (var y = 0; y < gutter.Height && state.ScrollOffset + y < rows.Count; y++)
        {
            var row = rows[state.ScrollOffset + y];
            if (row.Start != 0) continue;
            var number = (row.Line + 1).ToString(CultureInfo.InvariantCulture);
            var numberStyle = style.Patch(row.Line == state.Cursor.Line ? theme.Text : theme.TextMuted);
            buf.SetString(gutter.X + gutter.Width - 1 - number.Length, gutter.Y + y, number, numberStyle);
        }
    }

    // Follow the cursor unless the wheel moved the view, then keep the last row from rising above the bottom
    private void Scroll(TextAreaState state, int rowCount, int cursorRow, int cursorColumn, Rect inner)
    {
        var top = state.ScrollOffset;
        if (state.FollowCursor)
        {
            if (cursorRow < top) top = cursorRow;
            if (cursorRow >= top + inner.Height) top = cursorRow - inner.Height + 1;
        }
        state.ScrollOffset = Math.Clamp(top, 0, Math.Max(0, rowCount - inner.Height));

        if (_wrap != WrapMode.None)
        {
            state.HorizontalOffset = 0;
            return;
        }
        var left = state.HorizontalOffset;
        if (state.FollowCursor)
        {
            if (cursorColumn < left) left = cursorColumn;
            if (cursorColumn >= left + inner.Width) left = cursorColumn - inner.Width + 1;
        }
        state.HorizontalOffset = Math.Max(0, left);
    }

    // 1.x spec §10.8: the selected part of a row, and a cell for a selected line break when the row has room
    private static void DrawSelection(CellBuffer buf, Rect inner, int y, TextAreaState state, TextAreaLayout.Row row, int left, Style style) =>
        DrawRange(buf, inner, y, state, row, left, state.SelectionStart, state.SelectionEnd, style);

    // Patch the part of a row between two positions; a line break inside them shows as a cell after the line
    private static void DrawRange(CellBuffer buf, Rect inner, int y, TextAreaState state, TextAreaLayout.Row row, int left, TextPosition start, TextPosition end, Style style)
    {
        if (row.Line < start.Line || row.Line > end.Line) return;
        var line = state.Lines[row.Line];
        var from = Math.Max(row.Line == start.Line ? start.Index : 0, row.Start);
        var to = Math.Min(row.Line == end.Line ? end.Index : line.Length, row.End);
        if (from > to) return;
        var x0 = TextAreaState.ColumnIn(line, row, from) - left;
        var x1 = TextAreaState.ColumnIn(line, row, to) - left;
        if (row.LastOfLine && row.Line < end.Line) x1++;
        x0 = Math.Max(x0, 0);
        x1 = Math.Min(x1, inner.Width);
        if (x1 > x0) buf.SetStyle(new Rect(inner.X + x0, y, x1 - x0, 1), style);
    }

    private static void DrawRow(CellBuffer buf, int x, int y, IReadOnlyList<string> lines, TextAreaLayout.Row row, int left, int width, Style style)
    {
        var line = lines[row.Line];
        var column = 0;
        for (var i = row.Start; i < row.End;)
        {
            var length = StringInfo.GetNextTextElementLength(line, i);
            var grapheme = line.AsSpan(i, length);
            i += length;
            var w = UnicodeWidth.OfGrapheme(grapheme);
            var visibleStart = column - left;
            column += w;
            if (column <= left) continue;
            if (visibleStart < 0) continue; // a wide character cut by the left edge stays blank
            if (visibleStart + w > width) break;
            buf.SetString(x + visibleStart, y, grapheme, style, w);
        }
    }
}
