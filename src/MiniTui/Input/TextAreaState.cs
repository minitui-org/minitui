using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace MiniTui;

/// <summary>A position in a <see cref="TextAreaState"/>: a line, and a UTF-16 index into it on a grapheme cluster boundary.</summary>
public readonly record struct TextPosition(int Line, int Index) : IComparable<TextPosition>
{
    /// <summary>Compare by line, then by index.</summary>
    public int CompareTo(TextPosition other) => Line != other.Line ? Line.CompareTo(other.Line) : Index.CompareTo(other.Index);

    /// <summary>True if <paramref name="left"/> comes before <paramref name="right"/>.</summary>
    public static bool operator <(TextPosition left, TextPosition right) => left.CompareTo(right) < 0;

    /// <summary>True if <paramref name="left"/> comes after <paramref name="right"/>.</summary>
    public static bool operator >(TextPosition left, TextPosition right) => left.CompareTo(right) > 0;

    /// <summary>True if <paramref name="left"/> comes before <paramref name="right"/> or is the same.</summary>
    public static bool operator <=(TextPosition left, TextPosition right) => left.CompareTo(right) <= 0;

    /// <summary>True if <paramref name="left"/> comes after <paramref name="right"/> or is the same.</summary>
    public static bool operator >=(TextPosition left, TextPosition right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// The text and cursor of a multi-line text area (1.x spec §6). The text is a list of lines; the cursor is a line
/// and a UTF-16 index that is always on a grapheme cluster boundary, so editing never splits a character.
/// </summary>
public class TextAreaState
{
    private const int WheelRows = 3;

    private readonly List<string> _lines = [""];
    private readonly ReadOnlyCollection<string> _readOnlyLines;
    private readonly EditHistory<Snapshot> _history = new();
    private int _line;
    private int _index;
    private TextPosition? _anchor;
    private bool _dragging;
    private int? _goalColumn;
    private char? _pendingHighSurrogate;
    private int _indentSize = 4;
    private readonly ScrollbarDrag _bar = new();
    private readonly TextFieldState _find = new();
    private TextPosition _findStart;
    private int _version;
    private int _notifiedVersion;
    private (int Version, string Pattern, List<TextMatch> Matches)? _matches;

    /// <summary>Create a state with optional initial text. The cursor starts at the end.</summary>
    public TextAreaState(string text = "")
    {
        _readOnlyLines = _lines.AsReadOnly();
        Text = text;
    }

    /// <summary>
    /// The text, lines joined by <c>"\n"</c>. Setting it normalizes <c>"\r\n"</c> and <c>"\r"</c> to <c>"\n"</c>,
    /// turns tabs into spaces up to the next multiple of <see cref="IndentSize"/> columns, removes other control characters, and moves the
    /// cursor to the end. Setting it also clears the selection and the undo history.
    /// </summary>
    public string Text
    {
        get => string.Join('\n', _lines);
        set
        {
            var before = Changed is null ? null : Text;
            _lines.Clear();
            _lines.Add("");
            _line = 0;
            _index = 0;
            _anchor = null;
            _pendingHighSurrogate = null;
            _history.Clear();
            InsertNormalized(value ?? "");
            if (before is not null && string.Equals(before, Text, StringComparison.Ordinal)) _notifiedVersion = _version;
            Moved();
        }
    }

    /// <summary>
    /// Raised after the text changes (1.x spec §25.2): typing, deleting, pasting, cutting, indenting, undo and redo, or
    /// setting <see cref="Text"/> to something else. Moving the cursor or the selection doesn't raise it.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>The lines, without line breaks. Never empty: empty text is one empty line.</summary>
    public IReadOnlyList<string> Lines => _readOnlyLines;

    /// <summary>
    /// The cursor. Set values are clamped to the text and snap to the next cluster boundary, and clear the selection.
    /// </summary>
    public TextPosition Cursor
    {
        get => new(_line, _index);
        set => MoveTo(Clamp(value));
    }

    /// <summary>
    /// The other end of the selection, or null for none; <see cref="Cursor"/> is the end that moves (1.x spec §10).
    /// </summary>
    public TextPosition? SelectionAnchor => _anchor;

    /// <summary>The start of the selection (the earlier of the anchor and the cursor), or the cursor without one.</summary>
    public TextPosition SelectionStart => _anchor is { } a && a < Cursor ? a : Cursor;

    /// <summary>The end of the selection (the later of the anchor and the cursor), or the cursor without one.</summary>
    public TextPosition SelectionEnd => _anchor is { } a && a > Cursor ? a : Cursor;

    /// <summary>True if some text is selected.</summary>
    public bool HasSelection => _anchor is { } anchor && anchor != Cursor;

    /// <summary>The selected text, lines joined by <c>"\n"</c>, or <c>""</c>.</summary>
    public string SelectedText
    {
        get
        {
            var (start, end) = (SelectionStart, SelectionEnd);
            if (start.Line == end.Line) return _lines[start.Line][start.Index..end.Index];
            var text = new StringBuilder(_lines[start.Line], start.Index, _lines[start.Line].Length - start.Index, 64);
            for (var l = start.Line + 1; l < end.Line; l++) text.Append('\n').Append(_lines[l]);
            return text.Append('\n').Append(_lines[end.Line], 0, end.Index).ToString();
        }
    }

    /// <summary>True if <see cref="Undo"/> has something to take back.</summary>
    public bool CanUndo => _history.CanUndo;

    /// <summary>True if <see cref="Redo"/> has something to do again.</summary>
    public bool CanRedo => _history.CanRedo;

    /// <summary>
    /// Tab and Shift+Tab indent and outdent instead of moving focus, and Backspace in a line's indentation deletes back
    /// to the previous indent stop (1.x spec §16.3). Off by default.
    /// </summary>
    public bool TabIndents { get; set; }

    /// <summary>Enter starts the new line with the current line's indentation (1.x spec §16.3). Off by default.</summary>
    public bool AutoIndent { get; set; }

    /// <summary>
    /// The indent stop width: what Tab inserts with <see cref="TabIndents"/>, what Shift+Tab removes, and how far tabs
    /// in typed, set or pasted text expand. 4 by default, from 1 to 8. Changing it doesn't touch the text.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 1–8.</exception>
    public int IndentSize
    {
        get => _indentSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 8);
            _indentSize = value;
        }
    }

    /// <summary>
    /// Ctrl+F opens the find bar and F3 / Shift+F3 find again (1.x spec §16.5). On by default; turn it off to leave
    /// those keys to the app's bindings.
    /// </summary>
    public bool FindEnabled { get; set; } = true;

    /// <summary>True while the find bar is open: keys and pastes go to it (1.x spec §16.5).</summary>
    public bool IsFinding { get; private set; }

    /// <summary>The text being found. Setting it searches again from the selection's start (or the cursor).</summary>
    public string FindText
    {
        get => _find.Text;
        set
        {
            _find.Text = value ?? "";
            _findStart = SelectionStart;
            SearchFromStart();
        }
    }

    /// <summary>The number of matches of <see cref="FindText"/> in the text.</summary>
    public int MatchCount => Matches.Count;

    /// <summary>The index of the match that is the selection, or −1 when the selection isn't a match.</summary>
    public int MatchIndex
    {
        get
        {
            if (!HasSelection) return -1;
            var (start, end) = (SelectionStart, SelectionEnd);
            var matches = Matches;
            for (var i = 0; i < matches.Count; i++)
            {
                if (matches[i].Start == start && matches[i].End == end) return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// Open the find bar, as Ctrl+F does: it starts with the selection when that's within a line, or else the last
    /// find text, all selected so typing replaces it.
    /// </summary>
    public void OpenFind()
    {
        if (HasSelection && SelectionStart.Line == SelectionEnd.Line) _find.Text = SelectedText;
        _find.SelectAll();
        _findStart = SelectionStart;
        IsFinding = true;
        SearchFromStart();
    }

    /// <summary>Close the find bar, as Esc does. The current match stays selected, with the cursor at its end.</summary>
    public void CloseFind() => IsFinding = false;

    /// <summary>Select the next match after the cursor, wrapping around the text.</summary>
    /// <returns>False when there are no matches.</returns>
    public bool FindNext()
    {
        var matches = Matches;
        if (matches.Count == 0) return false;
        var cursor = Cursor;
        var next = matches.FindIndex(m => m.Start >= cursor);
        SelectMatch(matches[next < 0 ? 0 : next]);
        return true;
    }

    /// <summary>Select the previous match before the selection, wrapping around the text.</summary>
    /// <returns>False when there are no matches.</returns>
    public bool FindPrevious()
    {
        var matches = Matches;
        if (matches.Count == 0) return false;
        var start = SelectionStart;
        var previous = matches.FindLastIndex(m => m.Start < start);
        SelectMatch(matches[previous < 0 ? matches.Count - 1 : previous]);
        return true;
    }

    /// <summary>The first visible row (a wrapped row, not a line). Written by the widget while drawing, and by the wheel.</summary>
    public int ScrollOffset { get; internal set; }

    // What the widget last drew, for the keys and the mouse that depend on the layout
    internal int ViewWidth { get; private set; } = int.MaxValue;
    internal int ViewHeight { get; private set; } = 1;
    internal WrapMode ViewWrap { get; private set; } = WrapMode.None;
    internal Rect ViewArea { get; private set; }
    internal int ViewRowCount { get; private set; }
    internal Rect ViewGutter { get; private set; }

    /// <summary>The first visible display column with <see cref="WrapMode.None"/>.</summary>
    internal int HorizontalOffset { get; set; }

    /// <summary>True while the view should scroll to the cursor; the wheel turns it off until the cursor moves.</summary>
    internal bool FollowCursor { get; private set; } = true;

    private TextPosition End => new(_lines.Count - 1, _lines[^1].Length);

    /// <summary>Clear the text.</summary>
    public void Clear() => Text = "";

    /// <summary>
    /// Insert text at the cursor, replacing the selection, and move the cursor past it. Line breaks in the text start
    /// new lines.
    /// </summary>
    public void Insert(string text) => InsertText(text, EditKind.Other);

    /// <summary>
    /// Insert pasted text at the cursor, replacing the selection and keeping the text's line breaks. Tabs and control
    /// characters are handled as for <see cref="Text"/>.
    /// </summary>
    /// <returns>Always true, so a component can write <c>HandlePaste(text) =&gt; State.Paste(text)</c>.</returns>
    public bool Paste(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (IsFinding)
        {
            EditFind(() => _find.Paste(text));
            return true;
        }
        if (text.Length > 0) InsertText(text, EditKind.Other);
        else DeleteSelection();
        return true;
    }

    /// <summary>
    /// Select from <paramref name="anchor"/> to <paramref name="cursor"/>, both clamped and snapped as
    /// <see cref="Cursor"/> is. Equal values clear the selection.
    /// </summary>
    public void Select(TextPosition anchor, TextPosition cursor)
    {
        var a = Clamp(anchor);
        MoveTo(a);
        _anchor = a;
        MoveTo(Clamp(cursor), extend: true);
    }

    /// <summary>Select all the text, with the cursor at the end.</summary>
    public void SelectAll() => Select(default, End);

    /// <summary>Clear the selection, leaving the cursor where it is.</summary>
    public void ClearSelection() => MoveTo(Cursor);

    /// <summary>Copy the selection to the <see cref="MiniTui.Clipboard"/>.</summary>
    /// <returns>False, doing nothing, without a selection.</returns>
    public bool Copy()
    {
        if (!HasSelection) return false;
        MiniTui.Clipboard.SetText(SelectedText);
        return true;
    }

    /// <summary>Copy the selection to the <see cref="MiniTui.Clipboard"/> and delete it.</summary>
    /// <returns>False, doing nothing, without a selection.</returns>
    public bool Cut()
    {
        if (!Copy()) return false;
        DeleteSelection();
        return true;
    }

    /// <summary>
    /// Take back the last edit group (1.x spec §10.6): typed characters undo a word at a time. The text, cursor and
    /// selection return to how they were before it.
    /// </summary>
    /// <returns>False when there's nothing to undo.</returns>
    public bool Undo()
    {
        if (!_history.Undo(Current(), out var snapshot)) return false;
        Restore(snapshot);
        return true;
    }

    /// <summary>Do again the last edit group undone.</summary>
    /// <returns>False when there's nothing to redo.</returns>
    public bool Redo()
    {
        if (!_history.Redo(Current(), out var snapshot)) return false;
        Restore(snapshot);
        return true;
    }

    /// <summary>
    /// Handle an editing or movement key (1.x spec §6.3 and §10.3): text, Enter, ← → ↑ ↓, PgUp PgDn, Home End,
    /// Ctrl+Home End, Ctrl+← →, Shift with the movement keys to select, Backspace, Delete, Ctrl+Backspace / Ctrl+W,
    /// Ctrl+U, Ctrl+K, Ctrl+A (select all), Ctrl+C / Ctrl+X (copy and cut, only with a selection), Ctrl+V (paste the
    /// clipboard), Ctrl+Z (undo) and Ctrl+Y (redo), and with <see cref="TabIndents"/>, Tab and Shift+Tab (§16.3). Esc
    /// clears a selection; without one it isn't handled, nor are other Ctrl or Alt keys, nor Tab without
    /// <see cref="TabIndents"/>.
    /// </summary>
    /// <returns>True if the key was one of these.</returns>
    public virtual bool HandleKey(ConsoleKeyInfo key)
    {
        var ctrl = KeyInput.HasControl(key);
        var alt = (key.Modifiers & ConsoleModifiers.Alt) != 0;
        var shift = (key.Modifiers & ConsoleModifiers.Shift) != 0;
        if (IsFinding) return HandleFindKey(key, ctrl, alt, shift);
        // 1.x spec §22.3: Alt with the movement keys is left for the app (a split's Alt+Shift+arrows)
        if (alt && TextFieldState.IsMovementKey(key.Key)) return false;
        // 1.x spec §20.4: Ctrl+Insert copies (and never quits), Shift+Insert pastes, Shift+Delete cuts
        if (key.Key == ConsoleKey.Insert && !alt && ctrl != shift) return ctrl ? Copy() : MiniTui.Clipboard.PasteInto(Paste);
        if (key.Key == ConsoleKey.Delete && shift && !ctrl && Cut()) return true;
        // Esc clears a selection, and without one goes on (to close or go back)
        if (key.Key == ConsoleKey.Escape && HasSelection && !ctrl && !alt)
        {
            ClearSelection();
            return true;
        }

        if (FindEnabled && KeyInput.IsCtrl(key, 'F'))
        {
            OpenFind();
            return true;
        }
        if (FindEnabled && key.Key == ConsoleKey.F3 && !ctrl && !alt)
        {
            if (shift) FindPrevious();
            else FindNext();
            return true;
        }

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                if (ctrl || alt) return false;
                InsertText("\n" + (AutoIndent ? new string(' ', IndentBefore(SelectionStart)) : ""), EditKind.Other);
                return true;
            case ConsoleKey.Tab:
                if (!TabIndents || ctrl || alt) return false;
                if (shift) Outdent();
                else Indent();
                return true;
            case ConsoleKey.LeftArrow:
                if (HasSelection && !shift && !ctrl) return MoveTo(SelectionStart);
                return MoveTo(ctrl ? PreviousWord(Cursor) : PreviousCluster(Cursor), shift);
            case ConsoleKey.RightArrow:
                if (HasSelection && !shift && !ctrl) return MoveTo(SelectionEnd);
                return MoveTo(ctrl ? NextWord(Cursor) : NextCluster(Cursor), shift);
            case ConsoleKey.UpArrow:
                return MoveRows(-1, shift);
            case ConsoleKey.DownArrow:
                return MoveRows(1, shift);
            case ConsoleKey.PageUp:
                return MoveRows(-Math.Max(1, ViewHeight), shift);
            case ConsoleKey.PageDown:
                return MoveRows(Math.Max(1, ViewHeight), shift);
            case ConsoleKey.Home:
                return MoveTo(ctrl ? default : RowHome(), shift);
            case ConsoleKey.End:
                return MoveTo(ctrl ? End : RowEnd(), shift);
            case ConsoleKey.Backspace:
                // Unix terminals send Ctrl+Backspace as ^H (Backspace + Control); Windows reports the modifier directly
                if (DeleteSelection()) return true;
                if (ctrl) DeleteRange(PreviousWord(Cursor), Cursor, EditKind.Other);
                else if (TabIndents && _index > 0 && IndentBefore(Cursor) == _index)
                    DeleteRange(Cursor with { Index = (_index - 1) / IndentSize * IndentSize }, Cursor, EditKind.Other);
                else DeleteRange(PreviousCluster(Cursor), Cursor, EditKind.DeleteBackward);
                return true;
            case ConsoleKey.Delete:
                if (!DeleteSelection()) DeleteRange(Cursor, NextCluster(Cursor), EditKind.DeleteForward);
                return true;
        }

        if (KeyInput.IsCtrl(key, 'W'))
        {
            if (!DeleteSelection()) DeleteRange(PreviousWord(Cursor), Cursor, EditKind.Other);
            return true;
        }
        if (KeyInput.IsCtrl(key, 'U'))
        {
            if (!DeleteSelection())
                DeleteRange(_index == 0 ? PreviousCluster(Cursor) : new TextPosition(_line, 0), Cursor, EditKind.Other);
            return true;
        }
        if (KeyInput.IsCtrl(key, 'K'))
        {
            if (!DeleteSelection())
                DeleteRange(Cursor, _index == _lines[_line].Length ? NextCluster(Cursor) : new TextPosition(_line, _lines[_line].Length), EditKind.Other);
            return true;
        }
        if (KeyInput.IsCtrl(key, 'A'))
        {
            SelectAll();
            return true;
        }
        if (KeyInput.IsCtrlC(key)) return Copy();
        if (KeyInput.IsCtrl(key, 'X')) return Cut();
        if (KeyInput.IsCtrl(key, 'V')) return MiniTui.Clipboard.PasteInto(Paste);
        if (KeyInput.IsCtrlZ(key))
        {
            Undo();
            return true;
        }
        if (KeyInput.IsCtrl(key, 'Y'))
        {
            Redo();
            return true;
        }

        return TypeChar(key);
    }

    /// <summary>
    /// A left press inside the text moves the cursor there (Shift+press extends the selection to there), and a drag
    /// after it extends the selection, scrolling a row (or, without wrapping, a column) at a time past the edges; the
    /// wheel scrolls by three rows without moving the cursor (1.x spec §6.6 and §10.7). A double-click selects the word
    /// under the pointer and a triple-click its line (§17.4). Uses the area from the last draw.
    /// </summary>
    /// <returns>True if the event did any of these.</returns>
    public bool HandleMouse(MouseEvent mouse)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        if (IsFinding && _find.HandleMouse(mouse)) return true;
        var offset = ScrollOffset;
        if (_bar.Handle(mouse, ref offset))
        {
            // As the wheel does: the view stays where the bar left it until the cursor moves
            ScrollOffset = offset;
            FollowCursor = false;
            return true;
        }

        var area = ViewArea;
        switch (mouse.Action)
        {
            case MouseAction.ScrollUp or MouseAction.ScrollDown when (HitArea.IsEmpty ? area : HitArea).Contains(mouse.X, mouse.Y):
                var delta = mouse.Action == MouseAction.ScrollUp ? -WheelRows : WheelRows;
                ScrollOffset = Math.Clamp(ScrollOffset + delta, 0, MaxScroll);
                FollowCursor = false;
                return true;
            case MouseAction.Press when mouse.Button == MouseButton.Left:
                var inGutter = ViewGutter.Contains(mouse.X, mouse.Y);
                _dragging = inGutter || (HitArea.IsEmpty ? area : HitArea).Contains(mouse.X, mouse.Y);
                if (!_dragging) return false;
                // A press in the gutter (or the padding before it) goes to the start of the row
                inGutter |= mouse.X < area.X;
                var x = inGutter ? (ViewWrap == WrapMode.None ? -HorizontalOffset : 0) : mouse.X - area.X;
                var at = PositionAt(ScrollOffset + mouse.Y - area.Y, x);
                // 1.x spec §17.4: a double-click selects a word, a triple-click the line
                if (mouse.ClickCount == 2)
                {
                    var (start, end) = TextFieldState.WordAt(_lines[at.Line], at.Index);
                    Select(at with { Index = start }, at with { Index = end });
                }
                else if (mouse.ClickCount >= 3)
                {
                    Select(at with { Index = 0 }, at with { Index = _lines[at.Line].Length });
                }
                else
                {
                    MoveTo(at, (mouse.Modifiers & ConsoleModifiers.Shift) != 0);
                }
                return true;
            case MouseAction.Drag when mouse.Button == MouseButton.Left && _dragging:
                // Past the top or bottom, scroll a row and select to the first or last visible row
                int row;
                if (mouse.Y < area.Y) row = ScrollOffset = Math.Max(0, ScrollOffset - 1);
                else if (mouse.Y >= area.Bottom) row = (ScrollOffset = Math.Min(ScrollOffset + 1, MaxScroll)) + ViewHeight - 1;
                else row = ScrollOffset + mouse.Y - area.Y;
                var column = mouse.X < area.X ? -1 : mouse.X >= area.Right ? area.Width : mouse.X - area.X;
                MoveTo(PositionAt(row, column), extend: true);
                return true;
            case MouseAction.Release:
                _dragging = false;
                return false;
            default:
                return false;
        }
    }

    private int MaxScroll => Math.Max(0, ViewRowCount - ViewHeight);

    // The position at a visible row and a column from the view's left edge; the end of the text below the last row
    private TextPosition PositionAt(int row, int column)
    {
        var rows = Rows();
        if (row >= rows.Count) return End;
        var none = ViewWrap == WrapMode.None;
        return new TextPosition(rows[row].Line, IndexAtColumn(rows[row], Math.Max(0, column + (none ? HorizontalOffset : 0))));
    }

    /// <summary>Record what the widget drew, so the keys and the mouse can use the same layout.</summary>
    /// <summary>Where a press lands in the text: the view, and the block's side padding (1.x spec §24.2).</summary>
    internal Rect HitArea { get; set; }

    internal void Track(Rect area, WrapMode wrap, int rowCount, Rect gutter = default)
    {
        ViewArea = area;
        HitArea = default;
        ViewGutter = gutter;
        _bar.Track(default, 0, 0);
        ViewWidth = wrap == WrapMode.None ? int.MaxValue : Math.Max(1, area.Width);
        ViewHeight = Math.Max(1, area.Height);
        ViewWrap = wrap;
        ViewRowCount = rowCount;
    }

    /// <summary>The find field, which the widget draws in the find bar.</summary>
    internal TextFieldState FindField => _find;

    /// <summary>The matches of <see cref="FindText"/>, found again when it or the text changes.</summary>
    internal List<TextMatch> Matches
    {
        get
        {
            var pattern = _find.Text;
            if (_matches is { } cached && cached.Version == _version && cached.Pattern == pattern) return cached.Matches;
            var matches = FindMatches(pattern);
            _matches = (_version, pattern, matches);
            return matches;
        }
    }

    // 1.x spec §16.5: within lines, left to right, not overlapping; smart case; never splitting a cluster
    private List<TextMatch> FindMatches(string pattern)
    {
        var matches = new List<TextMatch>();
        if (pattern.Length == 0) return matches;
        var comparison = pattern.Any(char.IsUpper) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        for (var l = 0; l < _lines.Count; l++)
        {
            var line = _lines[l];
            for (var i = line.IndexOf(pattern, comparison); i >= 0 && i < line.Length;)
            {
                var end = i + pattern.Length;
                if (TextFieldState.SnapToBoundary(line, i) == i && TextFieldState.SnapToBoundary(line, end) == end)
                {
                    matches.Add(new TextMatch(new TextPosition(l, i), new TextPosition(l, end)));
                    i = end;
                }
                else
                {
                    i++;
                }
                i = i < line.Length ? line.IndexOf(pattern, i, comparison) : -1;
            }
        }
        return matches;
    }

    // Keys while the find bar is open: they edit the find text or move between matches
    private bool HandleFindKey(ConsoleKeyInfo key, bool ctrl, bool alt, bool shift)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape when !ctrl && !alt:
                CloseFind();
                return true;
            case ConsoleKey.Enter or ConsoleKey.F3 when !ctrl && !alt:
                if (shift) FindPrevious();
                else FindNext();
                return true;
            case ConsoleKey.DownArrow when !ctrl && !alt && !shift:
                FindNext();
                return true;
            case ConsoleKey.UpArrow when !ctrl && !alt && !shift:
                FindPrevious();
                return true;
        }
        if (KeyInput.IsCtrl(key, 'F'))
        {
            _find.SelectAll();
            return true;
        }
        var handled = false;
        EditFind(() => handled = _find.HandleKey(key));
        return handled;
    }

    // Edit the find text; when it changed, search again from where the search started
    private void EditFind(Func<bool> edit)
    {
        var before = _find.Text;
        edit();
        if (_find.Text != before) SearchFromStart();
    }

    private void SearchFromStart()
    {
        var matches = Matches;
        if (matches.Count == 0)
        {
            if (HasSelection) MoveTo(_findStart);
            return;
        }
        var start = _findStart;
        var first = matches.FindIndex(m => m.Start >= start);
        SelectMatch(matches[first < 0 ? 0 : first]);
    }

    private void SelectMatch(TextMatch match) => Select(match.Start, match.End);

    /// <summary>Called by the widget while drawing, after scrolling: where the scrollbar was drawn.</summary>
    internal void TrackScrollbar(Rect bar) => _bar.Track(bar, ViewRowCount, ViewHeight);

    /// <summary>The rows of the last drawn layout, for the current cursor.</summary>
    internal List<TextAreaLayout.Row> Rows() => TextAreaLayout.Build(_lines, ViewWidth, ViewWrap, Cursor);

    /// <summary>The display column of the cursor within its row.</summary>
    internal static int ColumnIn(string line, TextAreaLayout.Row row, int index) =>
        UnicodeWidth.Of(line.AsSpan(row.Start, Math.Max(0, index - row.Start)));

    // Move the cursor; Shift extends the selection from where the cursor was, anything else clears it
    private bool MoveTo(TextPosition position, bool extend = false)
    {
        if (extend) _anchor ??= Cursor;
        else _anchor = null;
        _line = position.Line;
        _index = position.Index;
        if (_anchor == Cursor) _anchor = null;
        _pendingHighSurrogate = null;
        _history.Break();
        Moved();
        return true;
    }

    private TextPosition Clamp(TextPosition position)
    {
        var line = Math.Clamp(position.Line, 0, _lines.Count - 1);
        return new TextPosition(line, TextFieldState.SnapToBoundary(_lines[line], Math.Clamp(position.Index, 0, _lines[line].Length)));
    }

    // A horizontal move, edit or click: the goal column resets and the view follows the cursor
    // Every edit ends here, after the state is consistent, so this is where Changed is raised
    private void Moved()
    {
        _goalColumn = null;
        FollowCursor = true;
        if (_version == _notifiedVersion) return;
        _notifiedVersion = _version;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ↑ ↓ PgUp PgDn: by wrapped row, keeping the goal column; past the first or last row, to the start or end
    private bool MoveRows(int delta, bool extend)
    {
        var rows = Rows();
        var current = TextAreaLayout.RowOf(rows, Cursor);
        var goal = _goalColumn ?? ColumnIn(_lines[_line], rows[current], _index);
        var target = current + delta;
        if (target < 0) MoveTo(default, extend);
        else if (target >= rows.Count) MoveTo(End, extend);
        else MoveTo(new TextPosition(rows[target].Line, IndexAtColumn(rows[target], goal)), extend);
        _goalColumn = goal;
        return true;
    }

    private TextPosition RowHome()
    {
        var rows = Rows();
        var row = rows[TextAreaLayout.RowOf(rows, Cursor)];
        return new TextPosition(_line, _index == row.Start ? 0 : row.Start);
    }

    private TextPosition RowEnd()
    {
        var rows = Rows();
        var row = rows[TextAreaLayout.RowOf(rows, Cursor)];
        var end = LastIndexIn(row);
        return new TextPosition(_line, _index == end ? _lines[_line].Length : end);
    }

    /// <summary>The boundary in <paramref name="row"/> nearest to <paramref name="column"/>, not past the row.</summary>
    private int IndexAtColumn(TextAreaLayout.Row row, int column)
    {
        var line = _lines[row.Line];
        var x = 0;
        for (var i = row.Start; i < row.End;)
        {
            var length = StringInfo.GetNextTextElementLength(line, i);
            var width = UnicodeWidth.OfGrapheme(line.AsSpan(i, length));
            if (x + width > column) return i;
            x += width;
            i += length;
        }
        return LastIndexIn(row);
    }

    // The last place the cursor can be and still show on this row: at a wrap point, End is the next row's start
    private int LastIndexIn(TextAreaLayout.Row row) =>
        row.LastOfLine || row.End == row.Start ? row.End : TextFieldState.PreviousBoundary(_lines[row.Line], row.End);

    private TextPosition PreviousCluster(TextPosition p)
    {
        if (p.Index > 0) return p with { Index = TextFieldState.PreviousBoundary(_lines[p.Line], p.Index) };
        return p.Line > 0 ? new TextPosition(p.Line - 1, _lines[p.Line - 1].Length) : p;
    }

    private TextPosition NextCluster(TextPosition p)
    {
        if (p.Index < _lines[p.Line].Length) return p with { Index = TextFieldState.NextBoundary(_lines[p.Line], p.Index) };
        return p.Line < _lines.Count - 1 ? new TextPosition(p.Line + 1, 0) : p;
    }

    // A line break counts as whitespace, so word moves and deletes cross it
    private bool IsSpaceBefore(TextPosition p) => p.Index == 0 ? p.Line > 0 : char.IsWhiteSpace(_lines[p.Line][p.Index - 1]);

    private bool IsSpaceAt(TextPosition p) =>
        p.Index == _lines[p.Line].Length ? p.Line < _lines.Count - 1 : char.IsWhiteSpace(_lines[p.Line][p.Index]);

    private TextPosition PreviousWord(TextPosition p)
    {
        while (p != default && IsSpaceBefore(p)) p = PreviousCluster(p);
        while (p != default && !IsSpaceBefore(p)) p = PreviousCluster(p);
        return p;
    }

    private TextPosition NextWord(TextPosition p)
    {
        var end = End;
        while (p != end && IsSpaceAt(p)) p = NextCluster(p);
        while (p != end && !IsSpaceAt(p)) p = NextCluster(p);
        return p;
    }

    // The leading spaces of the position's line, no more than there are before it
    private int IndentBefore(TextPosition p)
    {
        var line = _lines[p.Line];
        var n = 0;
        while (n < p.Index && line[n] == ' ') n++;
        return n;
    }

    // The lines Tab and Shift+Tab change: the selection's, leaving out a last line it ends at the start of
    private (int First, int Last) SelectedLines()
    {
        var (start, end) = (SelectionStart, SelectionEnd);
        return (start.Line, end.Line > start.Line && end.Index == 0 ? end.Line - 1 : end.Line);
    }

    // Tab (1.x spec §16.3): spaces to the next stop, or with a selection over several lines, indent each of them
    private void Indent()
    {
        var (first, last) = SelectedLines();
        if (!HasSelection || SelectionStart.Line == SelectionEnd.Line)
        {
            InsertText("\t", EditKind.Other);
            return;
        }
        ShiftLines(first, last, line => line.Length == 0 ? 0 : IndentSize);
    }

    // Shift+Tab: remove up to one level of leading spaces, back to the previous stop
    private void Outdent()
    {
        var (first, last) = SelectedLines();
        ShiftLines(first, last, line =>
        {
            var n = 0;
            while (n < line.Length && line[n] == ' ') n++;
            return n == 0 ? 0 : -(n - (n - 1) / IndentSize * IndentSize);
        });
    }

    // Add (positive) or remove (negative) spaces at the start of each line, as one undo group, moving the cursor and
    // the anchor with the text they're on
    private void ShiftLines(int first, int last, Func<string, int> change)
    {
        var changes = new int[last - first + 1];
        for (var l = first; l <= last; l++) changes[l - first] = change(_lines[l]);
        if (changes.All(c => c == 0)) return;

        _history.Record(EditKind.Other, Current);
        _history.Break();
        _version++;
        for (var l = first; l <= last; l++)
        {
            var c = changes[l - first];
            _lines[l] = c >= 0 ? new string(' ', c) + _lines[l] : _lines[l][-c..];
        }
        TextPosition Shift(TextPosition p)
        {
            if (p.Line < first || p.Line > last) return p;
            var c = changes[p.Line - first];
            return p with { Index = Math.Max(0, p.Index + c) };
        }
        (_line, _index) = Shift(Cursor);
        if (_anchor is { } anchor) _anchor = Shift(anchor);
        if (_anchor == Cursor) _anchor = null;
        _pendingHighSurrogate = null;
        Moved();
    }

    private void DeleteRange(TextPosition start, TextPosition end, EditKind kind)
    {
        if (end <= start) return;
        _history.Record(kind, Current);
        RemoveRange(start, end);
        Moved();
    }

    private void RemoveRange(TextPosition start, TextPosition end)
    {
        _anchor = null;
        if (end <= start) return;
        _version++;
        var joined = _lines[start.Line][..start.Index] + _lines[end.Line][end.Index..];
        _lines[start.Line] = joined;
        if (end.Line > start.Line) _lines.RemoveRange(start.Line + 1, end.Line - start.Line);
        _line = start.Line;
        _index = TextFieldState.SnapToBoundary(joined, start.Index);
    }

    private bool TypeChar(ConsoleKeyInfo key)
    {
        var c = key.KeyChar;
        if (c == '\0' || char.IsControl(c) || KeyInput.HasCommandModifier(key)) return false;

        if (char.IsHighSurrogate(c))
        {
            _pendingHighSurrogate = c;
            return true;
        }

        if (char.IsLowSurrogate(c))
        {
            var high = _pendingHighSurrogate;
            _pendingHighSurrogate = null;
            if (high is { } h) InsertText(new string([h, c]), EditKind.Type);
            return true;
        }

        _pendingHighSurrogate = null;
        InsertText(c.ToString(), EditKind.Type, char.IsWhiteSpace(c));
        return true;
    }

    private void InsertText(string text, EditKind kind, bool typedSpace = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        // Replacing a selection is a group of its own, which the typing after it joins
        if (HasSelection) _history.Break();
        _history.Record(kind, Current, typedSpace);
        RemoveRange(SelectionStart, SelectionEnd);
        InsertNormalized(text);
        Moved();
    }

    private bool DeleteSelection()
    {
        if (!HasSelection) return false;
        _history.Record(EditKind.Other, Current);
        RemoveRange(SelectionStart, SelectionEnd);
        Moved();
        return true;
    }

    private Snapshot Current() => new([.. _lines], Cursor, _anchor);

    private void Restore(Snapshot snapshot)
    {
        _version++;
        _lines.Clear();
        _lines.AddRange(snapshot.Lines);
        (_line, _index) = snapshot.Cursor;
        _anchor = snapshot.Anchor;
        _pendingHighSurrogate = null;
        Moved();
    }

    private sealed record Snapshot(string[] Lines, TextPosition Cursor, TextPosition? Anchor);

    // Insert at the cursor: line breaks split lines, tabs become spaces, other control characters are dropped
    private void InsertNormalized(string text)
    {
        _version++;
        var line = _lines[_line];
        var after = line[_index..];
        var current = new StringBuilder(line, 0, _index, _index + text.Length);
        var added = new List<string>();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                added.Add(current.ToString());
                current.Clear();
            }
            else if (c == '\t')
            {
                var column = UnicodeWidth.Of(current.ToString());
                current.Append(' ', IndentSize - column % IndentSize);
            }
            else if (!char.IsControl(c))
            {
                current.Append(c);
            }
        }

        var cursor = current.Length;
        current.Append(after);
        if (added.Count == 0)
        {
            _lines[_line] = current.ToString();
        }
        else
        {
            _lines[_line] = added[0];
            added.RemoveAt(0);
            added.Add(current.ToString());
            _lines.InsertRange(_line + 1, added);
            _line += added.Count;
        }
        _index = TextFieldState.SnapToBoundary(_lines[_line], cursor);
    }
}

/// <summary>A match of a text area's find text: from <paramref name="Start"/> to <paramref name="End"/>, on one line.</summary>
internal readonly record struct TextMatch(TextPosition Start, TextPosition End);

/// <summary>Splits a text area's lines into rows of UTF-16 index ranges (1.x spec §6.5).</summary>
internal static class TextAreaLayout
{
    /// <summary>One row: <paramref name="Start"/> to <paramref name="End"/> of a line, <paramref name="Width"/> columns wide.</summary>
    public readonly record struct Row(int Line, int Start, int End, int Width, bool LastOfLine);

    /// <summary>
    /// The rows for <paramref name="width"/> columns. With word wrap, spaces at a wrap point stay at the end of their
    /// row (and may hang past the edge). When the cursor is at the end of a line whose last row is full, an empty
    /// row follows for it, so the cursor is always drawn inside the area.
    /// </summary>
    public static List<Row> Build(IReadOnlyList<string> lines, int width, WrapMode wrap, TextPosition cursor)
    {
        var rows = new List<Row>(lines.Count);
        for (var l = 0; l < lines.Count; l++)
        {
            var first = rows.Count;
            if (wrap == WrapMode.None || width == int.MaxValue) rows.Add(new Row(l, 0, lines[l].Length, UnicodeWidth.Of(lines[l]), true));
            else WrapLine(lines[l], l, Math.Max(1, width), wrap, rows);

            var last = rows[^1];
            if (wrap != WrapMode.None && width != int.MaxValue && cursor.Line == l && cursor.Index == last.End
                && last.Width >= width && last.End > last.Start && rows.Count > first)
            {
                rows[^1] = last with { LastOfLine = false };
                rows.Add(new Row(l, last.End, last.End, 0, true));
            }
        }
        return rows;
    }

    /// <summary>The row the cursor shows on: at a wrap point, the next row.</summary>
    public static int RowOf(List<Row> rows, TextPosition cursor)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Line != cursor.Line) continue;
            if (cursor.Index < row.End || row.LastOfLine) return i;
        }
        return rows.Count - 1;
    }

    private static void WrapLine(string line, int lineNumber, int width, WrapMode wrap, List<Row> rows)
    {
        var rowStart = 0;
        var rowWidth = 0;
        var breakAt = -1;       // where the next row can start: just after whitespace in this row
        var widthAtBreak = 0;
        for (var i = 0; i < line.Length;)
        {
            var length = StringInfo.GetNextTextElementLength(line, i);
            var w = UnicodeWidth.OfGrapheme(line.AsSpan(i, length));
            var isSpace = char.IsWhiteSpace(line[i]);

            if (rowWidth + w > width && !(isSpace && wrap == WrapMode.Word))
            {
                if (wrap == WrapMode.Word && breakAt > rowStart)
                {
                    rows.Add(new Row(lineNumber, rowStart, breakAt, widthAtBreak, false));
                    rowStart = breakAt;
                    rowWidth -= widthAtBreak;
                    breakAt = -1;
                }
                if (rowWidth + w > width && rowWidth > 0)
                {
                    // No whitespace to break at (or Char wrap): break before this cluster
                    rows.Add(new Row(lineNumber, rowStart, i, rowWidth, false));
                    rowStart = i;
                    rowWidth = 0;
                    breakAt = -1;
                }
            }

            rowWidth += w;
            i += length;
            if (isSpace)
            {
                breakAt = i;
                widthAtBreak = rowWidth;
            }
        }
        rows.Add(new Row(lineNumber, rowStart, line.Length, rowWidth, true));
    }
}
