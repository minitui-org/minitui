using System.Globalization;
using System.Text;

namespace MiniTui;

/// <summary>
/// The scroll position of scrollable text, such as a <see cref="ParagraphBuilder"/> or a modal.
/// The widget writes <see cref="ViewportHeight"/> and <see cref="ContentHeight"/> and clamps <see cref="Offset"/> while drawing.
/// </summary>
public sealed class ScrollState
{
    /// <summary>The first visible row. Clamped when drawn.</summary>
    public int Offset { get; set; }

    /// <summary>How many rows were visible at the last draw.</summary>
    public int ViewportHeight { get; private set; }

    /// <summary>How many rows the content had at the last draw.</summary>
    public int ContentHeight { get; private set; }

    /// <summary>
    /// Let ← → and the horizontal wheel (or Shift or Alt with the wheel) scroll unwrapped text sideways by 8 columns
    /// (1.x spec §23.3), for a <see cref="ParagraphBuilder"/> with <see cref="WrapMode.None"/>. Off by default, so
    /// ← → stay free for the screen.
    /// </summary>
    public bool ScrollsHorizontally { get; set; }

    /// <summary>The first visible column of unwrapped text, with <see cref="ScrollsHorizontally"/>. Clamped when drawn.</summary>
    public int HorizontalOffset { get; set; }

    /// <summary>How many columns were visible at the last draw, with <see cref="ScrollsHorizontally"/>.</summary>
    public int ViewportWidth { get; private set; }

    /// <summary>How wide the widest row was at the last draw, with <see cref="ScrollsHorizontally"/>.</summary>
    public int ContentWidth { get; private set; }

    /// <summary>
    /// Let the mouse select the text (1.x spec §20.5): a press and drag select, a double-click a word, a triple-click
    /// a row. Ctrl+C and Ctrl+Insert copy, Ctrl+A selects all and Esc clears. Off by default.
    /// </summary>
    public bool Selectable { get; set; }

    /// <summary>True while something is selected.</summary>
    public bool HasSelection => _anchor != _head;

    /// <summary>
    /// Show a caret in the text, as caret browsing does (1.x spec §27.4): ← → ↑ ↓ Home End PgUp PgDn and
    /// Ctrl+Home Ctrl+End move it, Shift with them selects, and Esc or F7 hides it. Only with <see cref="Selectable"/>;
    /// <see cref="HandleKey"/> toggles it on F7. It starts at the top of the view.
    /// </summary>
    public bool CaretVisible
    {
        get => _caret;
        set
        {
            if (value == _caret || (value && !Selectable)) return;
            _caret = value;
            if (value) PlaceCaret(Math.Max(0, Offset));
            else ClearSelection();
        }
    }

    /// <summary>Show the caret if it's hidden, otherwise hide it, as F7 does. For an app that lists F7 in its own keys.</summary>
    public void ToggleCaret() => CaretVisible = !CaretVisible;

    /// <summary>The selected part of each row as it was drawn last, with a line break between rows.</summary>
    public string SelectedText
    {
        get
        {
            if (!HasSelection) return "";
            var (start, end) = Ordered;
            var text = new StringBuilder();
            for (var row = start.Row; row <= end.Row && row < _rows.Count; row++)
            {
                if (row > start.Row) text.Append('\n');
                var from = row == start.Row ? start.Column : 0;
                var to = row == end.Row ? end.Column : int.MaxValue;
                foreach (var (cluster, column, width) in Clusters(_rows[row].Text))
                    if (column < to && column + width > from) text.Append(cluster);
            }
            return text.ToString();
        }
    }

    /// <summary>Select every row drawn last.</summary>
    public void SelectAll()
    {
        if (_rows.Count == 0) return;
        _anchor = new Mark(0, 0);
        _head = new Mark(_rows.Count - 1, _rows[^1].Width);
        Remember();
    }

    /// <summary>Clear the selection. The caret, if shown, stays where it is.</summary>
    public void ClearSelection()
    {
        if (_caret) (_anchor, _anchorAt) = (_head, _headAt);
        else _anchor = _head = _anchorAt = _headAt = default;
    }

    /// <summary>Copy the selection to the <see cref="Clipboard"/>.</summary>
    /// <returns>False, doing nothing, without a selection.</returns>
    public bool Copy()
    {
        if (!HasSelection) return false;
        Clipboard.SetText(SelectedText);
        return true;
    }

    /// <summary>Scroll by <paramref name="rows"/> (negative scrolls up).</summary>
    public void ScrollBy(int rows) => Offset = Clamp(Offset + rows);

    /// <summary>
    /// Handle ↑ ↓ j k PgUp PgDn Home End; when <see cref="ScrollsHorizontally"/>, also ← and → (unhandled at the
    /// edge), with Home and End going to the first and last column too; when <see cref="Selectable"/>, also Ctrl+C
    /// and Ctrl+Insert (copy, with a selection), Ctrl+A (select all), Esc (clear a selection, then hide the caret)
    /// and F7 (show or hide the caret). While the caret shows, the arrows, Home, End, PgUp and PgDn move it instead.
    /// </summary>
    /// <returns>True if the key was handled.</returns>
    public bool HandleKey(ConsoleKeyInfo key)
    {
        if (Selectable && HandleSelectionKey(key, out var handled)) return handled;
        if (_caret && HandleCaretKey(key)) return true;
        var page = Math.Max(1, ViewportHeight);
        switch (key.Key)
        {
            case ConsoleKey.UpArrow: ScrollBy(-1); return true;
            case ConsoleKey.DownArrow: ScrollBy(1); return true;
            case ConsoleKey.PageUp: ScrollBy(-page); return true;
            case ConsoleKey.PageDown: ScrollBy(page); return true;
            case ConsoleKey.Home:
                Offset = 0;
                if (ScrollsHorizontally) HorizontalOffset = 0;
                return true;
            case ConsoleKey.End:
                Offset = MaxOffset;
                if (ScrollsHorizontally) HorizontalOffset = MaxColumns;
                return true;
            case ConsoleKey.LeftArrow when ScrollsHorizontally: return ScrollSideways(-SideStep);
            case ConsoleKey.RightArrow when ScrollsHorizontally: return ScrollSideways(SideStep);
        }

        if (KeyInput.HasCommandModifier(key)) return false;
        switch (key.KeyChar)
        {
            case 'k': ScrollBy(-1); return true;
            case 'j': ScrollBy(1); return true;
            default: return false;
        }
    }

    /// <summary>
    /// Handle the mouse over the text drawn last (1.x spec §4.8, §14.4): the wheel scrolls by three rows, the
    /// scrollbar's thumb drags and its track pages. When <see cref="ScrollsHorizontally"/>, the horizontal wheel and
    /// Shift or Alt with the wheel scroll sideways by 8 columns, and so does the horizontal scrollbar (§23.3). When <see cref="Selectable"/>, the left button
    /// selects (§20.5).
    /// </summary>
    /// <returns>True if it scrolled, tried to, or selected.</returns>
    public bool HandleMouse(MouseEvent mouse)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        var offset = Offset;
        if (_bar.Handle(mouse, ref offset))
        {
            Offset = Clamp(offset);
            return true;
        }
        var columns = HorizontalOffset;
        if (_columnBar.Handle(mouse, ref columns))
        {
            HorizontalOffset = ClampColumns(columns);
            return true;
        }
        if (Selectable && HandleSelectionMouse(mouse)) return true;
        if (!_mouseArea.Contains(mouse.X, mouse.Y)) return false;
        if (ScrollsHorizontally)
        {
            // Alt as well as Shift: terminals that keep Shift for bypassing mouse mode still send Alt
            var sideways = (mouse.Modifiers & (ConsoleModifiers.Shift | ConsoleModifiers.Alt)) != 0;
            switch (mouse.Action)
            {
                case MouseAction.ScrollLeft:
                case MouseAction.ScrollUp when sideways:
                    ScrollColumnsBy(-SideStep);
                    return true;
                case MouseAction.ScrollRight:
                case MouseAction.ScrollDown when sideways:
                    ScrollColumnsBy(SideStep);
                    return true;
            }
        }
        switch (mouse.Action)
        {
            case MouseAction.ScrollUp: ScrollBy(-WheelRows); return true;
            case MouseAction.ScrollDown: ScrollBy(WheelRows); return true;
            default: return false;
        }
    }

    /// <summary>Called by widgets while drawing: the area the text was drawn in.</summary>
    internal void Track(Rect area)
    {
        _mouseArea = area;
        _bar.Track(default, 0, 0);
        _columnBar.Track(default, 0, 0);
    }

    /// <summary>
    /// Called by widgets while drawing, after <see cref="Fit"/>: the rows laid out and where they were drawn, for
    /// selecting. A selection is kept by its place in the text, so it survives the rows being laid out again; one on
    /// lines that are gone is cleared. When the layout changes and the offset wasn't moved, the line at the top
    /// stays there (1.x spec §23.3).
    /// </summary>
    /// <param name="text">Where the rows were drawn.</param>
    /// <param name="rows">The rows laid out.</param>
    /// <param name="origins">Where each row starts in the text: its line and the column in that line.</param>
    /// <param name="alignment">The paragraph's alignment.</param>
    /// <param name="columns">When scrolling sideways, the first visible column: rows wider than the view start there.</param>
    /// <param name="layout">What the rows were laid out for; the top line is kept when it changes.</param>
    internal void TrackRows(Rect text, IReadOnlyList<Line> rows, IReadOnlyList<(int Line, int From)> origins, Alignment alignment,
        int? columns = null, (int Width, WrapMode Wrap) layout = default)
    {
        _textArea = text;
        KeepTop(origins, layout);
        _rows.Clear();
        if (!Selectable) return;
        for (var i = 0; i < rows.Count; i++)
        {
            var line = rows[i];
            var (source, from) = origins[i];
            var content = new StringBuilder();
            foreach (var span in line.Spans) content.Append(Sanitizer.SingleLine(span.Content ?? ""));
            var row = content.ToString();
            var width = UnicodeWidth.Of(row);
            if (columns is not null && width > text.Width)
            {
                _rows.Add(new Row(row, width, 0, source, from, Scrolls: true));
                continue;
            }
            var shown = Math.Min(width, text.Width);
            var start = (line.Alignment != Alignment.Left ? line.Alignment : alignment) switch
            {
                Alignment.Center => (text.Width - shown) / 2,
                Alignment.Right => text.Width - shown,
                _ => 0,
            };
            _rows.Add(new Row(row, width, start, source, from));
        }
        if (_caret && _caretPending && _rows.Count > 0)
        {
            PlaceCaret(Offset);
            return;
        }
        if (_anchorAt == _headAt && !_caret) return;
        if (_rows.Count == 0 || Math.Max(_anchorAt.Row, _headAt.Row) > _rows[^1].Line)
        {
            if (_caret && _rows.Count > 0) PlaceCaret(Offset);
            else ClearSelection();
        }
        else (_anchor, _head) = (ToRow(_anchorAt), ToRow(_headAt));
    }

    // The same line at the top after the rows are laid out again, unless the offset was moved; at the end, the end
    private void KeepTop(IReadOnlyList<(int Line, int From)> origins, (int Width, WrapMode Wrap) layout)
    {
        if (_layout is { } last && last != layout && _requested == _drawnOffset && origins.Count > 0)
        {
            if (_atEnd) Offset = MaxOffset;
            else
            {
                var row = 0;
                while (row + 1 < origins.Count && origins[row + 1].CompareTo(_top) <= 0) row++;
                Offset = Clamp(row);
            }
        }
        _layout = layout;
        _drawnOffset = Offset;
        _top = Offset < origins.Count ? origins[Offset] : default;
        _atEnd = MaxOffset > 0 && Offset == MaxOffset;
    }

    /// <summary>
    /// Called by widgets while drawing: the selected columns of row <paramref name="row"/>, from the left of the text
    /// area. Empty without a selection on it.
    /// </summary>
    internal (int From, int To) SelectedColumns(int row)
    {
        if (!HasSelection || row >= _rows.Count) return default;
        var (start, end) = Ordered;
        if (row < start.Row || row > end.Row) return default;
        var from = row == start.Row ? start.Column : 0;
        var to = row == end.Row ? end.Column : int.MaxValue;
        int x0 = int.MaxValue, x1 = 0;
        foreach (var (_, column, width) in Clusters(_rows[row].Text))
        {
            if (column >= to) break;
            if (column + width <= from) continue;
            x0 = Math.Min(x0, column);
            x1 = column + width;
        }
        var shift = StartOf(_rows[row]);
        return x1 > x0 ? (x0 + shift, x1 + shift) : default;
    }

    /// <summary>Called by widgets while drawing: the caret's column in row <paramref name="row"/>, from the left of the text area, or null.</summary>
    internal int? CaretColumn(int row) =>
        _caret && !_caretPending && row == _head.Row && row < _rows.Count ? _head.Column + StartOf(_rows[row]) : null;

    /// <summary>Show the caret at the start of row <paramref name="row"/> as drawn, for a widget that knows better than the top of the view.</summary>
    internal void ShowCaretAt(int row)
    {
        if (!Selectable) return;
        _caret = true;
        PlaceCaret(row);
    }

    /// <summary>Called by widgets while drawing, after <see cref="Fit"/>: where the scrollbar was drawn.</summary>
    internal void TrackScrollbar(Rect bar) => _bar.Track(bar, ContentHeight, ViewportHeight);

    /// <summary>Called by widgets while drawing, after <see cref="FitColumns"/>: where the horizontal scrollbar was drawn.</summary>
    internal void TrackColumnScrollbar(Rect bar) => _columnBar.Track(bar, ContentWidth, ViewportWidth);

    /// <summary>Called by widgets while drawing.</summary>
    internal void Fit(int contentHeight, int viewportHeight)
    {
        ContentHeight = Math.Max(0, contentHeight);
        ViewportHeight = Math.Max(0, viewportHeight);
        _requested = Offset;
        Offset = Clamp(Offset);
    }

    /// <summary>Called by widgets while drawing: the widest row and the visible columns, for scrolling sideways.</summary>
    internal void FitColumns(int contentWidth, int viewportWidth)
    {
        ContentWidth = Math.Max(0, contentWidth);
        ViewportWidth = Math.Max(0, viewportWidth);
        HorizontalOffset = ClampColumns(HorizontalOffset);
    }

    private const int WheelRows = 3;
    private const int SideStep = 8;

    private Rect _mouseArea;
    private Rect _textArea;
    private readonly ScrollbarDrag _bar = new();
    private readonly ScrollbarDrag _columnBar = new(horizontal: true);
    private readonly List<Row> _rows = [];
    // The selection in rows as drawn, and in the text (a line and a column of it), which it's kept by
    private Mark _anchor;
    private Mark _head;
    private Mark _anchorAt;
    private Mark _headAt;
    private bool _selecting;
    private bool _caret;
    private bool _caretPending; // shown before any rows were drawn: placed at the next draw
    private int? _caretColumn;  // the column ↑ ↓ keep to

    // The layout drawn last, the offset asked for and drawn, and the top row's place in the text
    private (int Width, WrapMode Wrap)? _layout;
    private int _requested;
    private int _drawnOffset;
    private (int Line, int From) _top;
    private bool _atEnd;

    // A row as drawn: its text, its width, the column it starts at (by its alignment) or that it scrolls sideways,
    // and its line and first column in the text
    private readonly record struct Row(string Text, int Width, int Start, int Line, int From, bool Scrolls = false);

    // The selection's rows as places in the text
    private void Remember() => (_anchorAt, _headAt) = (ToText(_anchor), ToText(_head));

    private Mark ToText(Mark mark) => _rows.Count == 0 ? default : new Mark(_rows[mark.Row].Line, _rows[mark.Row].From + mark.Column);

    // The row and column of a place in the text: in the last row starting at or before it, at most at its end
    private Mark ToRow(Mark at)
    {
        int low = 0, high = _rows.Count - 1;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var row = _rows[mid];
            if (row.Line < at.Row || (row.Line == at.Row && row.From <= at.Column)) low = mid;
            else high = mid - 1;
        }
        var found = _rows[low];
        var column = found.Line < at.Row ? found.Width : Math.Clamp(at.Column - found.From, 0, found.Width);
        return new Mark(low, column);
    }

    private int StartOf(Row row) => row.Scrolls ? -HorizontalOffset : row.Start;

    // A row and a column of that row's text: the boundary before that cell
    private readonly record struct Mark(int Row, int Column) : IComparable<Mark>
    {
        public int CompareTo(Mark other) => Row != other.Row ? Row.CompareTo(other.Row) : Column.CompareTo(other.Column);
    }

    private (Mark Start, Mark End) Ordered => _anchor.CompareTo(_head) <= 0 ? (_anchor, _head) : (_head, _anchor);

    private bool HandleSelectionKey(ConsoleKeyInfo key, out bool handled)
    {
        var ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;
        var shift = (key.Modifiers & ConsoleModifiers.Shift) != 0;
        if (KeyInput.IsCtrlC(key) || (key.Key == ConsoleKey.Insert && ctrl && !shift)) handled = Copy();
        else if (KeyInput.IsCtrl(key, 'a')) { SelectAll(); handled = true; }
        else if (key.Key == ConsoleKey.Escape && HasSelection) { ClearSelection(); handled = true; }
        else if (key.Key == ConsoleKey.Escape && _caret) { CaretVisible = false; handled = true; }
        else if (key.Key == ConsoleKey.F7 && key.Modifiers == 0) { ToggleCaret(); handled = true; }
        else return handled = false;
        return true;
    }

    private bool HandleSelectionMouse(MouseEvent mouse)
    {
        var area = _textArea;
        switch (mouse.Action)
        {
            case MouseAction.Press when mouse.Button == MouseButton.Left:
                _selecting = area.Contains(mouse.X, mouse.Y) && _rows.Count > 0;
                if (!_selecting) return false;
                var at = MarkAt(Offset + mouse.Y - area.Y, mouse.X - area.X);
                var row = _rows[at.Row];
                // 1.x spec §17.4's word rule: a double-click selects a word, a triple-click the row
                if (mouse.ClickCount == 2 && row.Text.Length > 0)
                {
                    var (start, end) = TextFieldState.WordAt(row.Text, IndexAt(row.Text, at.Column));
                    _anchor = at with { Column = ColumnOf(row.Text, start) };
                    _head = at with { Column = ColumnOf(row.Text, end) };
                }
                else if (mouse.ClickCount >= 3)
                {
                    _anchor = at with { Column = 0 };
                    _head = at with { Column = row.Width };
                }
                else _anchor = _head = at;
                Remember();
                return true;
            case MouseAction.Drag when mouse.Button == MouseButton.Left && _selecting:
                // Past the left or right edge, scroll a column (1.x spec §23.3)
                if (ScrollsHorizontally && mouse.X < area.X) ScrollColumnsBy(-1);
                else if (ScrollsHorizontally && mouse.X >= area.Right) ScrollColumnsBy(1);
                // Past the top or bottom, scroll a row and select to the first or last visible row
                int visible;
                if (mouse.Y < area.Y) visible = Offset = Clamp(Offset - 1);
                else if (mouse.Y >= area.Bottom) visible = (Offset = Clamp(Offset + 1)) + area.Height - 1;
                else visible = Offset + mouse.Y - area.Y;
                _head = MarkAt(visible, ScrollsHorizontally ? Math.Clamp(mouse.X - area.X, 0, area.Width) : mouse.X - area.X);
                Remember();
                return true;
            case MouseAction.Release when _selecting:
                _selecting = false;
                return false;
            default:
                return false;
        }
    }

    // The boundary nearest a visible row and a column from the text area's left; below the last row, its end
    private Mark MarkAt(int row, int x)
    {
        if (row >= _rows.Count) return new Mark(_rows.Count - 1, _rows[^1].Width);
        row = Math.Max(0, row);
        var text = _rows[row];
        var column = Math.Clamp(x - StartOf(text), 0, text.Width);
        // Inside a wide character, the boundary before it
        var at = 0;
        foreach (var (_, start, width) in Clusters(text.Text))
        {
            if (start + width > column) break;
            at = start + width;
        }
        return new Mark(row, at);
    }

    // The string index of the cluster at a column, or the end
    private static int IndexAt(string text, int column)
    {
        var at = 0;
        for (var i = 0; i < text.Length;)
        {
            var length = StringInfo.GetNextTextElementLength(text, i);
            at += UnicodeWidth.OfGrapheme(text.AsSpan(i, length));
            if (at > column) return i;
            i += length;
        }
        return text.Length;
    }

    private static int ColumnOf(string text, int index) => UnicodeWidth.Of(text.AsSpan(0, index));

    // The visible clusters of a row, each with its column and width
    private static IEnumerable<(string Cluster, int Column, int Width)> Clusters(string text)
    {
        var column = 0;
        for (var i = 0; i < text.Length;)
        {
            var length = StringInfo.GetNextTextElementLength(text, i);
            var cluster = text.Substring(i, length);
            i += length;
            var width = UnicodeWidth.OfGrapheme(cluster);
            if (width == 0) continue;
            yield return (cluster, column, width);
            column += width;
        }
    }

    // The caret at the start of a row, nothing selected; before any rows are drawn, at the next draw
    private void PlaceCaret(int row)
    {
        _caretColumn = null;
        if (_rows.Count == 0)
        {
            _caretPending = true;
            return;
        }
        _caretPending = false;
        _anchor = _head = new Mark(Math.Clamp(row, 0, _rows.Count - 1), 0);
        Remember();
    }

    private bool HandleCaretKey(ConsoleKeyInfo key)
    {
        if (_caretPending || _rows.Count == 0) return key.Key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow or ConsoleKey.UpArrow
            or ConsoleKey.DownArrow or ConsoleKey.Home or ConsoleKey.End or ConsoleKey.PageUp or ConsoleKey.PageDown;
        var ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;
        var shift = (key.Modifiers & ConsoleModifiers.Shift) != 0;
        if ((key.Modifiers & ConsoleModifiers.Alt) != 0) return false;
        var head = _head;
        var page = Math.Max(1, ViewportHeight - 1);
        var keepColumn = false;
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow when !ctrl: head = Step(head, -1); break;
            case ConsoleKey.RightArrow when !ctrl: head = Step(head, 1); break;
            case ConsoleKey.UpArrow when !ctrl: head = Vertical(head, -1); keepColumn = true; break;
            case ConsoleKey.DownArrow when !ctrl: head = Vertical(head, 1); keepColumn = true; break;
            case ConsoleKey.PageUp when !ctrl: head = Vertical(head, -page); keepColumn = true; break;
            case ConsoleKey.PageDown when !ctrl: head = Vertical(head, page); keepColumn = true; break;
            case ConsoleKey.Home: head = ctrl ? new Mark(0, 0) : head with { Column = 0 }; break;
            case ConsoleKey.End: head = ctrl ? new Mark(_rows.Count - 1, _rows[^1].Width) : head with { Column = _rows[head.Row].Width }; break;
            default: return false;
        }
        if (!keepColumn) _caretColumn = null;
        _head = head;
        if (!shift) _anchor = _head;
        Remember();
        // Keep the caret's row in view
        if (_head.Row < Offset) Offset = _head.Row;
        else if (ViewportHeight > 0 && _head.Row >= Offset + ViewportHeight) Offset = _head.Row - ViewportHeight + 1;
        return true;
    }

    // A character left or right, onto the row before or after at an edge
    private Mark Step(Mark at, int by)
    {
        var clusters = Clusters(_rows[at.Row].Text).ToList();
        if (by < 0)
        {
            if (at.Column > 0) return at with { Column = clusters.Last(c => c.Column < at.Column).Column };
            return at.Row > 0 ? new Mark(at.Row - 1, _rows[at.Row - 1].Width) : at;
        }
        if (clusters.FirstOrDefault(c => c.Column >= at.Column) is { Cluster: not null } next) return at with { Column = next.Column + next.Width };
        return at.Row + 1 < _rows.Count ? new Mark(at.Row + 1, 0) : at;
    }

    // Rows up or down, at the column it was in before moving up or down, or the nearest boundary before it
    private Mark Vertical(Mark at, int rows)
    {
        _caretColumn ??= at.Column;
        var row = Math.Clamp(at.Row + rows, 0, _rows.Count - 1);
        var column = 0;
        foreach (var (_, start, width) in Clusters(_rows[row].Text))
        {
            if (start + width > _caretColumn) break;
            column = start + width;
        }
        return new Mark(row, column);
    }

    private int MaxOffset => Math.Max(0, ContentHeight - ViewportHeight);

    private void ScrollColumnsBy(int columns) => HorizontalOffset = ClampColumns(HorizontalOffset + columns);

    private int MaxColumns => Math.Max(0, ContentWidth - ViewportWidth);

    // Scroll sideways; false, doing nothing, at that edge (and before anything wider than the view was drawn)
    private bool ScrollSideways(int columns)
    {
        var to = Math.Clamp(HorizontalOffset + columns, 0, MaxColumns);
        if (to == HorizontalOffset) return false;
        HorizontalOffset = to;
        return true;
    }

    private int ClampColumns(int offset) => ContentWidth == 0 && ViewportWidth == 0
        ? Math.Max(0, offset)
        : Math.Clamp(offset, 0, MaxColumns);

    // Before the first draw the content size is unknown, so only negative offsets are clamped
    private int Clamp(int offset) => ContentHeight == 0 && ViewportHeight == 0
        ? Math.Max(0, offset)
        : Math.Clamp(offset, 0, MaxOffset);
}
