namespace MiniTui;

/// <summary>
/// A row in a <see cref="FilterListState"/> (1.x spec §25.5): the label the filter matches, muted text after it, a
/// right-aligned hint, and a key that identifies it when the rows are replaced.
/// </summary>
/// <param name="Label">The text that's matched and shown.</param>
/// <param name="Detail">Muted text shown after the label, not matched.</param>
/// <param name="Hint">Muted text drawn right-aligned, not matched, such as a key or a date.</param>
/// <param name="Key">What identifies the row, compared with <see cref="object.Equals(object?)"/>; without one, the label does.</param>
public readonly record struct FilterRow(string Label, string? Detail = null, string? Hint = null, object? Key = null);

/// <summary>
/// A list narrowed by typing (1.x spec §25.5): a filter line over the rows that fuzzy-match it (<see cref="Fuzzy.Match"/>),
/// best first, and the selection among them. Draw it with <see cref="Widget.FilterList"/>. The command palette and
/// the filterable select use it.
/// </summary>
public sealed class FilterListState
{
    private FilterRow[] _rows = [];
    private string[] _labels = [];
    private List<(int Row, FuzzyMatch Match)> _matches = [];
    private int[] _matchRows = [];
    private int _selected = -1;
    private int? _notifiedRow;
    private int _offset;
    private int _viewport;
    private Rect _rowsArea;

    /// <summary>Create a list over <paramref name="rows"/>, filtered by <paramref name="filter"/>, with its cursor at the end.</summary>
    public FilterListState(IEnumerable<FilterRow>? rows = null, string filter = "")
    {
        Filter = new TextFieldState(filter ?? "");
        Filter.Changed += (_, _) =>
        {
            Refilter();
            Notify();
        };
        SetRowsCore(rows ?? []);
        Refilter();
        _notifiedRow = Selected;
    }

    /// <summary>The filter line. A change to its text filters the rows again, selecting the best match.</summary>
    public TextFieldState Filter { get; }

    /// <summary>The rows, as last set.</summary>
    public IReadOnlyList<FilterRow> Rows => _rows;

    /// <summary>Indexes into <see cref="Rows"/> that match the filter, best first; with an empty filter, all of them in order.</summary>
    public IReadOnlyList<int> Matches => _matchRows;

    /// <summary>The selected row's index into <see cref="Rows"/>, or null when nothing matches.</summary>
    public int? Selected => _selected >= 0 && _selected < _matches.Count ? _matches[_selected].Row : null;

    /// <summary>↑ at the first match goes to the last, and ↓ at the last to the first. Off by default.</summary>
    public bool Wraps { get; set; }

    /// <summary>Raised when the selection moves to another row: by a key, the mouse, filtering, <see cref="Select"/> or <see cref="SetRows"/>.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised on a double-click on a row. Enter is left to the caller.</summary>
    public event EventHandler? Activated;

    /// <summary>
    /// Replace the rows and filter them again. The selection stays on the row with the same <see cref="FilterRow.Key"/>
    /// (the same label, for rows without keys) if it still matches; otherwise it goes to the best match.
    /// </summary>
    public void SetRows(IEnumerable<FilterRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var before = Selected is { } row ? _rows[row] : (FilterRow?)null;
        SetRowsCore(rows);
        Refilter();
        if (before is { } previous)
        {
            var index = _matches.FindIndex(m => SameRow(_rows[m.Row], previous));
            if (index >= 0) _selected = index;
        }
        Notify();
    }

    /// <summary>Select <paramref name="row"/>, an index into <see cref="Rows"/>, if it matches the filter.</summary>
    public void Select(int row)
    {
        var index = _matches.FindIndex(m => m.Row == row);
        if (index < 0) return;
        _selected = index;
        Notify();
    }

    /// <summary>
    /// ↑ ↓ PgUp PgDn and Ctrl+Home Ctrl+End move the selection; Esc clears a filter that isn't empty; any other key
    /// goes to the filter line (Home and End move its cursor). Enter, Tab, and Esc with an empty filter are left to the
    /// caller.
    /// </summary>
    /// <returns>True if the key was used.</returns>
    public bool HandleKey(ConsoleKeyInfo key)
    {
        var ctrl = KeyInput.HasControl(key);
        switch (key.Key)
        {
            case ConsoleKey.UpArrow: Move(-1, Wraps); return true;
            case ConsoleKey.DownArrow: Move(1, Wraps); return true;
            case ConsoleKey.PageUp: Page(-1); return true;
            case ConsoleKey.PageDown: Page(1); return true;
            case ConsoleKey.Home when ctrl: MoveTo(_matches.Count > 0 ? 0 : -1); return true;
            case ConsoleKey.End when ctrl: MoveTo(_matches.Count - 1); return true;
            case ConsoleKey.Enter or ConsoleKey.Tab: return false;
            case ConsoleKey.Escape when Filter.Text.Length > 0 && !Filter.HasSelection:
                Filter.Clear();
                return true;
            case ConsoleKey.Escape when !Filter.HasSelection: return false;
        }
        return Filter.HandleKey(key);
    }

    /// <summary>Paste into the filter line, filtering again.</summary>
    public bool HandlePaste(string text) => Filter.Paste(text);

    /// <summary>
    /// The wheel moves the selection, the pointer moving over a row selects it, a click on a row selects it (a
    /// double-click raises <see cref="Activated"/>), and the filter line takes its clicks and drags. Uses the areas from
    /// the last draw.
    /// </summary>
    /// <returns>True if the mouse was over the list.</returns>
    public bool HandleMouse(MouseEvent mouse)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        if (Click(mouse) is { } row)
        {
            Select(row);
            if (mouse.ClickCount >= 2) Activated?.Invoke(this, EventArgs.Empty);
        }
        return true;
    }

    /// <summary>As <see cref="HandleMouse"/>, but answers the row clicked, for the menus, where a click chooses.</summary>
    internal int? Click(MouseEvent mouse)
    {
        switch (mouse.Action)
        {
            case MouseAction.ScrollUp: Move(-1, wrap: false); return null;
            case MouseAction.ScrollDown: Move(1, wrap: false); return null;
            case MouseAction.Move:
                // 1.x spec §17.7: the selection follows the pointer
                if (_rowsArea.Contains(mouse.X, mouse.Y) && _offset + (mouse.Y - _rowsArea.Y) is var under && under < _matches.Count) MoveTo(under);
                return null;
        }
        if (mouse.IsClick && _rowsArea.Contains(mouse.X, mouse.Y))
        {
            var index = _offset + (mouse.Y - _rowsArea.Y);
            return index < _matches.Count ? _matches[index].Row : null;
        }
        Filter.HandleMouse(mouse);
        return null;
    }

    /// <summary>The rows the list wants: the matches, at least 1 (for the empty message), at most <paramref name="maxRows"/>.</summary>
    internal int WantedRows(int maxRows) => Math.Clamp(_matches.Count, 1, Math.Max(1, maxRows));

    /// <summary>The widest row: label, detail and hint with their gaps and padding.</summary>
    internal int ContentWidth()
    {
        var width = 0;
        for (var i = 0; i < _rows.Length; i++)
        {
            var row = _rows[i];
            var w = UnicodeWidth.Of(_labels[i]);
            if (row.Detail is { Length: > 0 } detail) w += 2 + UnicodeWidth.Of(Sanitizer.SingleLine(detail));
            if (row.Hint is { Length: > 0 } hint) w += 2 + UnicodeWidth.Of(Sanitizer.SingleLine(hint));
            width = Math.Max(width, w);
        }
        return width + 2;
    }

    // ---- For the builder ----

    internal int MatchCount => _matches.Count;
    internal int SelectedIndex => _selected;
    internal (int Row, FuzzyMatch Match) MatchAt(int index) => _matches[index];
    internal string LabelOf(int row) => _labels[row];

    /// <summary>Fit the offset to the selection and a viewport of <paramref name="rows"/> rows; answers the first shown.</summary>
    internal int Layout(Rect rows)
    {
        _viewport = rows.Height;
        _offset = SelectionState.FitOffset(_offset, Math.Max(0, _selected), _matches.Count, _viewport);
        _rowsArea = rows with { Height = Math.Clamp(_matches.Count - _offset, 0, _viewport) };
        return _offset;
    }

    internal void ClearLayout() => _rowsArea = default;

    private void SetRowsCore(IEnumerable<FilterRow> rows)
    {
        _rows = [.. rows];
        _labels = Array.ConvertAll(_rows, r => Sanitizer.SingleLine(r.Label ?? ""));
    }

    // Filter again; the selection goes to the best match
    private void Refilter()
    {
        var text = Filter.Text;
        var matches = new List<(int Row, FuzzyMatch Match)>();
        for (var i = 0; i < _rows.Length; i++)
            if (Fuzzy.Match(text, _labels[i]) is { } match) matches.Add((i, match));

        // Best score first; then the shorter label, then the rows' order (a stable sort). An empty filter keeps the order.
        _matches = [.. matches
            .OrderByDescending(m => m.Match.Score)
            .ThenBy(m => text.Trim().Length == 0 ? 0 : _labels[m.Row].Length)
            .ThenBy(m => m.Row)];
        _matchRows = [.. _matches.Select(m => m.Row)];
        _selected = _matches.Count > 0 ? 0 : -1;
        _offset = 0;
    }

    private static bool SameRow(FilterRow row, FilterRow previous) =>
        previous.Key is { } key ? Equals(row.Key, key) : row.Key is null && string.Equals(row.Label, previous.Label, StringComparison.Ordinal);

    private void MoveTo(int index)
    {
        _selected = index;
        Notify();
    }

    private void Move(int direction, bool wrap)
    {
        if (_matches.Count == 0) return;
        var target = _selected + direction;
        MoveTo(wrap ? ((target % _matches.Count) + _matches.Count) % _matches.Count : Math.Clamp(target, 0, _matches.Count - 1));
    }

    private void Page(int direction)
    {
        if (_matches.Count == 0) return;
        MoveTo(Math.Clamp(_selected + direction * Math.Max(1, _viewport), 0, _matches.Count - 1));
    }

    private void Notify()
    {
        var row = Selected;
        if (row == _notifiedRow) return;
        _notifiedRow = row;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
