using System.Text;

namespace MiniTui;

/// <summary>
/// Markdown to show formatted (1.x spec §27.3): its source, its blocks, where it's scrolled to, and with
/// <see cref="BlockCursor"/> a highlighted run of blocks. Draw it with <see cref="Widget.Markdown"/>.
/// </summary>
/// <remarks>
/// Headings, emphasis, inline code, links, images (as <c>[image: alt]</c>), lists with check boxes, quotes, fenced
/// and indented code, rules, tables and front matter are formatted; anything else is shown as written, so nothing
/// is lost.
/// </remarks>
public sealed class MarkdownState
{
    private string _source = "";
    private bool _plainText;
    private List<string> _lines = [];
    private List<MarkdownBlock> _blocks = [];
    private bool _blockCursor;
    private int _anchor;
    private int _active;
    private bool _follow;
    private MarkdownLayout? _layout;
    private (int Width, Theme? Theme) _laidFor;
    private Rect _text;

    /// <summary>Show <paramref name="source"/>.</summary>
    public MarkdownState(string source = "")
    {
        Source = source;
    }

    /// <summary>
    /// The Markdown. Setting it lays it out again, scrolls to the top and puts the block cursor on the first block.
    /// Tabs are read as spaces to the next multiple of 4, and <c>\r\n</c> as a line break.
    /// </summary>
    public string Source
    {
        get => _source;
        set
        {
            _source = value ?? "";
            Parse();
            Scroll.Offset = 0;
            Scroll.ClearSelection();
            SetHighlight(0, 0);
        }
    }

    /// <summary>
    /// Show <see cref="Source"/> as plain text instead: each run of lines between blank ones is a block, its lines
    /// as written and wrapped at words. Off by default.
    /// </summary>
    public bool PlainText
    {
        get => _plainText;
        set
        {
            if (value == _plainText) return;
            _plainText = value;
            Source = _source;
        }
    }

    /// <summary>The blocks of <see cref="Source"/>, top to bottom.</summary>
    public IReadOnlyList<MarkdownBlock> Blocks => _blocks;

    /// <summary>
    /// The scroll position. Its <see cref="ScrollState.Selectable"/> lets the mouse select the text as shown.
    /// </summary>
    public ScrollState Scroll { get; } = new();

    /// <summary>
    /// Highlight a run of blocks with a bar beside them, moved by the keys and a click (1.x spec §27.3), for an app
    /// that does something with a block, such as quoting it. Off by default, when the keys scroll.
    /// </summary>
    public bool BlockCursor
    {
        get => _blockCursor;
        set
        {
            if (value == _blockCursor) return;
            _blockCursor = value;
            _follow = value;
            HighlightChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The highlighted blocks, first to last, or null without <see cref="BlockCursor"/> or blocks.</summary>
    public (int First, int Last)? Highlight =>
        _blockCursor && _blocks.Count > 0 ? (Math.Min(_anchor, _active), Math.Max(_anchor, _active)) : null;

    /// <summary>Raised when <see cref="Highlight"/> changes.</summary>
    public event EventHandler? HighlightChanged;

    /// <summary>
    /// The highlighted blocks' lines of <see cref="Source"/> as written, less the indentation they share, or "".
    /// </summary>
    public string HighlightedSource
    {
        get
        {
            if (Highlight is not { } h) return "";
            var from = _blocks[h.First].FirstLine;
            var to = Math.Min(_blocks[h.Last].LastLine, _lines.Count - 1);
            if (from > to) return "";
            var lines = _lines.Skip(from).Take(to - from + 1).Select(l => l.TrimEnd()).ToList();
            var shared = lines.Where(l => l.Length > 0).Select(MarkdownParser.Indentation).DefaultIfEmpty(0).Min();
            return string.Join('\n', lines.Select(l => l.Length >= shared ? l[shared..] : "")).Trim('\n');
        }
    }

    /// <summary>The highlighted blocks as they were drawn last, a line per row, or "" before they're drawn.</summary>
    public string HighlightedText => Highlight is { } h && _layout is { } layout ? layout.Shown(h.First, h.Last) : "";

    /// <summary>
    /// True with something to copy: a mouse selection, or a highlight. A <see cref="Component"/> showing the state
    /// returns it from <see cref="Component.HasSelection"/>, so Ctrl+C copies instead of quitting.
    /// </summary>
    public bool HasSelection => Scroll.HasSelection || Highlight is not null;

    /// <summary>Highlight block <paramref name="block"/> and scroll to it.</summary>
    public void SetHighlight(int block) => SetHighlight(block, block);

    /// <summary>
    /// Highlight blocks <paramref name="first"/> to <paramref name="last"/> and scroll to <paramref name="last"/>.
    /// Both are kept in range.
    /// </summary>
    public void SetHighlight(int first, int last)
    {
        var max = Math.Max(0, _blocks.Count - 1);
        var was = Highlight;
        _anchor = Math.Clamp(first, 0, max);
        _active = Math.Clamp(last, 0, max);
        _follow = true;
        if (Highlight != was) HighlightChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The block shown at <paramref name="row"/> of the rows drawn last, or null for a blank row or none.</summary>
    public int? BlockAt(int row) =>
        _layout is { } layout && row >= 0 && row < layout.RowBlock.Count && layout.RowBlock[row] >= 0 ? layout.RowBlock[row] : null;

    /// <summary>The first and last row block <paramref name="block"/> was drawn on last, or null before it's drawn.</summary>
    public (int First, int Last)? RowsOf(int block) =>
        _layout is { } layout && block >= 0 && block < layout.BlockRows.Count ? layout.BlockRows[block] : null;

    /// <summary>
    /// Handle a key. Without <see cref="BlockCursor"/>, the keys scroll (<see cref="ScrollState.HandleKey"/>). With
    /// it, ↑ ↓ (and k j) move the highlight a block, Shift+↑ ↓ stretch it, PgUp PgDn scroll a page and highlight the
    /// first block in view, and Home End go to the first and last block. Either way, with
    /// <see cref="ScrollState.Selectable"/> on, Ctrl+A, Esc and copying a mouse selection work as in a paragraph,
    /// and with a highlight and no mouse selection, Ctrl+C and Ctrl+Insert copy <see cref="HighlightedText"/>.
    /// </summary>
    /// <returns>True if the key was handled.</returns>
    public bool HandleKey(ConsoleKeyInfo key)
    {
        if (!_blockCursor || _blocks.Count == 0) return Scroll.HandleKey(key);

        var ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;
        var shift = (key.Modifiers & ConsoleModifiers.Shift) != 0;
        var alt = (key.Modifiers & ConsoleModifiers.Alt) != 0;
        if (KeyInput.IsCtrlC(key) || (key.Key == ConsoleKey.Insert && ctrl && !shift))
        {
            if (Scroll.HasSelection) return Scroll.Copy();
            Clipboard.SetText(HighlightedText);
            return true;
        }
        if (Scroll.Selectable && (KeyInput.IsCtrl(key, 'a') || (key.Key == ConsoleKey.Escape && Scroll.HasSelection)))
            return Scroll.HandleKey(key);
        if (ctrl || alt) return false;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow: Move(-1, shift); return true;
            case ConsoleKey.DownArrow: Move(1, shift); return true;
            case ConsoleKey.PageUp: Page(-1); return true;
            case ConsoleKey.PageDown: Page(1); return true;
            case ConsoleKey.Home:
                Scroll.ClearSelection();
                SetHighlight(0);
                return true;
            case ConsoleKey.End:
                Scroll.ClearSelection();
                SetHighlight(_blocks.Count - 1);
                return true;
        }
        switch (key.KeyChar)
        {
            case 'k': Move(-1, stretch: false); return true;
            case 'j': Move(1, stretch: false); return true;
            default: return false;
        }
    }

    /// <summary>
    /// Handle the mouse as <see cref="ScrollState.HandleMouse"/> does. With <see cref="BlockCursor"/>, a click
    /// highlights the block under it, and a highlight scrolled out of view moves to the nearest block in view.
    /// </summary>
    /// <returns>True if it was handled.</returns>
    public bool HandleMouse(MouseEvent mouse)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        if (_blockCursor && mouse.IsClick && _text.Contains(mouse.X, mouse.Y) && BlockAt(Scroll.Offset + mouse.Y - _text.Y) is { } block)
        {
            var was = Highlight;
            _anchor = _active = block;
            if (Highlight != was) HighlightChanged?.Invoke(this, EventArgs.Empty);
        }
        var handled = Scroll.HandleMouse(mouse);
        if (handled && _blockCursor) KeepInView();
        return handled;
    }

    // ---- Drawing ----

    /// <summary>Called by the builder: the layout at a width and theme, laid out again when either changes.</summary>
    internal MarkdownLayout Layout(int width, Theme theme)
    {
        if (_layout is null || _laidFor != (width, theme))
        {
            _layout = MarkdownLayout.Layout(_blocks, width, MarkdownStyles.From(theme));
            _laidFor = (width, theme);
            _follow |= _blockCursor;
        }
        return _layout;
    }

    /// <summary>Called by the builder before drawing the rows in <paramref name="text"/>: scroll to the highlight if it moved.</summary>
    internal void Arrange(Rect text)
    {
        _text = text;
        if (!_follow || !_blockCursor || _layout is null || _blocks.Count == 0) return;
        _follow = false;
        var (first, last) = _layout.BlockRows[_active];
        var height = Math.Max(1, text.Height);
        if (first < Scroll.Offset || last >= Scroll.Offset + height)
            Scroll.Offset = last >= Scroll.Offset + height && last - first < height ? last - height + 1 : first;
        if (_active == 0) Scroll.Offset = 0;
    }

    private void Parse()
    {
        _lines = Lines(_source);
        _blocks = _plainText ? MarkdownParser.PlainText(_lines) : MarkdownParser.Parse(_lines);
        _layout = null;
    }

    // Lines with tabs as spaces to the next multiple of 4
    private static List<string> Lines(string source)
    {
        var lines = new List<string>();
        foreach (var raw in source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (!raw.Contains('\t', StringComparison.Ordinal))
            {
                lines.Add(raw);
                continue;
            }
            var line = new StringBuilder();
            foreach (var c in raw)
            {
                if (c == '\t') line.Append(' ', 4 - line.Length % 4);
                else line.Append(c);
            }
            lines.Add(line.ToString());
        }
        return lines;
    }

    private void Move(int by, bool stretch)
    {
        Scroll.ClearSelection();
        var active = Math.Clamp(_active + by, 0, _blocks.Count - 1);
        SetHighlight(stretch ? _anchor : active, active);
    }

    private void Page(int by)
    {
        Scroll.ClearSelection();
        Scroll.ScrollBy(by * Math.Max(1, Scroll.ViewportHeight - 1));
        var block = FirstInView();
        var was = Highlight;
        _anchor = _active = block;
        if (Highlight != was) HighlightChanged?.Invoke(this, EventArgs.Empty);
    }

    private void KeepInView()
    {
        if (_layout is null || Highlight is not { } h) return;
        var (first, last) = (_layout.BlockRows[h.First].First, _layout.BlockRows[h.Last].Last);
        var top = Scroll.Offset;
        var bottom = top + Scroll.ViewportHeight - 1;
        int block;
        if (last < top) block = FirstInView();
        else if (first > bottom) block = LastInView();
        else return;
        _anchor = _active = block;
        HighlightChanged?.Invoke(this, EventArgs.Empty);
    }

    private int FirstInView()
    {
        if (_layout is null) return _active;
        var top = Scroll.Offset;
        for (var b = 0; b < _layout.BlockRows.Count; b++)
            if (_layout.BlockRows[b].First >= top) return b;
        return _layout.BlockRows.Count - 1;
    }

    private int LastInView()
    {
        if (_layout is null) return _active;
        var bottom = Scroll.Offset + Scroll.ViewportHeight - 1;
        for (var b = _layout.BlockRows.Count - 1; b >= 0; b--)
            if (_layout.BlockRows[b].Last <= bottom) return b;
        return 0;
    }
}
