using System.Globalization;

namespace MiniTui;

/// <summary>
/// How a <see cref="ParagraphBuilder"/> breaks lines that are wider than its area.
/// </summary>
public enum WrapMode
{
    /// <summary>
    /// Don't wrap; long lines are truncated with <c>…</c>, or scroll sideways with a <see cref="ScrollState"/> whose
    /// <see cref="ScrollState.ScrollsHorizontally"/> is on.
    /// </summary>
    None,
    /// <summary>Break at whitespace. Words wider than the area are broken anywhere.</summary>
    Word,
    /// <summary>Break at any character.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Name from the spec (§8.3), matching Ratatui's wrap modes.")]
    Char,
}

/// <summary>
/// Measuring text the way <see cref="ParagraphBuilder"/> lays it out.
/// </summary>
public static class Paragraph
{
    /// <summary>How many rows <paramref name="text"/> takes at <paramref name="width"/> columns.</summary>
    public static int MeasureLines(Text text, int width, WrapMode wrap)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TextWrap.Wrap(text, width, wrap).Count;
    }

    /// <summary>
    /// The rows <paramref name="text"/> wraps to at <paramref name="width"/> columns: the same rows, with the same styles
    /// and alignment, that <see cref="ParagraphBuilder"/> draws. With <see cref="WrapMode.None"/> or a width of 0 or
    /// less, the lines are returned as they are.
    /// </summary>
    public static IReadOnlyList<Line> Wrap(Text text, int width, WrapMode wrap)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TextWrap.Wrap(text, width, wrap);
    }
}

/// <summary>
/// Multi-line text with optional wrapping, alignment and scrolling.
/// </summary>
public sealed class ParagraphBuilder
{
    private Text _text = new([]);
    private WrapMode _wrap;
    private Alignment _alignment;
    private int _scroll;
    private ScrollState? _state;
    private BlockBuilder? _block;
    private Style? _style;

    /// <summary>The text. A string is split into lines at line breaks.</summary>
    public ParagraphBuilder Text(Text text) { _text = text ?? new Text([]); return this; }

    /// <summary>How long lines break. The default is <see cref="WrapMode.None"/>.</summary>
    public ParagraphBuilder Wrap(WrapMode wrap) { _wrap = wrap; return this; }

    /// <summary>The alignment of lines that don't set their own.</summary>
    public ParagraphBuilder Alignment(Alignment alignment) { _alignment = alignment; return this; }

    /// <summary>Skip this many rows from the top.</summary>
    public ParagraphBuilder Scroll(int rows) { _scroll = Math.Max(0, rows); return this; }

    /// <summary>Scroll with a state the paragraph keeps in range, and show a scrollbar when the text overflows.</summary>
    public ParagraphBuilder State(ScrollState state) { _state = state; return this; }

    /// <summary>Draw a block around the text.</summary>
    public ParagraphBuilder Block(BlockBuilder block) { _block = block; return this; }

    /// <summary>A style patched over the theme's text style.</summary>
    public ParagraphBuilder Style(Style style) { _style = style; return this; }

    /// <summary>Draw the paragraph.</summary>
    public void Draw(CellBuffer buf, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buf);
        var inner = _block?.Draw(buf, area) ?? area;
        _state?.Track(area);
        if (inner.IsEmpty)
        {
            _state?.Fit(0, 0);
            _state?.TrackRows(inner, [], [], _alignment);
            return;
        }

        var origins = new List<(int Line, int From)>();
        var rows = TextWrap.Wrap(_text, inner.Width, _wrap, origins);

        // Without a right border, the scrollbar takes the last column, so the text is laid out narrower. Scrolling
        // sideways (1.x spec §23.3), the horizontal bar goes on a free bottom border or takes the last row, which
        // can make the rows overflow, so the two are settled together.
        var scrollbarOnBorder = _block?.HasRightBorder == true;
        var columnBarOnBorder = _block?.HasFreeBottomBorder == true;
        var sideways = _state is { ScrollsHorizontally: true } && _wrap == WrapMode.None;
        var widest = sideways && rows.Count > 0 ? rows.Max(r => r.Width) : 0;
        var text = inner;
        bool vertical = false, horizontal = false;
        for (var pass = 0; pass < 2; pass++)
        {
            vertical = _state is not null && rows.Count > text.Height;
            text = text with { Width = vertical && !scrollbarOnBorder && inner.Width > 1 ? inner.Width - 1 : inner.Width };
            horizontal = sideways && widest > text.Width && (columnBarOnBorder || inner.Height > 1);
            text = text with { Height = horizontal && !columnBarOnBorder ? inner.Height - 1 : inner.Height };
        }
        if (text.Width < inner.Width && _wrap != WrapMode.None)
        {
            origins.Clear();
            rows = TextWrap.Wrap(_text, text.Width, _wrap, origins);
        }

        var offset = _scroll;
        int? columns = null;
        if (_state is { } state)
        {
            state.Fit(rows.Count, text.Height);
            state.FitColumns(widest, text.Width);
            if (sideways) columns = state.HorizontalOffset;
            state.TrackRows(text, rows, origins, _alignment, columns, (text.Width, _wrap));
            offset = state.Offset;
        }

        var style = Theme.Current.Text.Patch(_style ?? default);
        for (var i = 0; i < text.Height && offset + i < rows.Count; i++)
        {
            var line = rows[offset + i];
            if (columns is { } from && line.Width > text.Width) DrawScrolled(buf, text.X, text.Y + i, line, from, text.Width, style, marks: !horizontal);
            else
            {
                var alignment = line.Alignment != MiniTui.Alignment.Left ? line.Alignment : _alignment;
                WidgetText.DrawLine(buf, text.X, text.Y + i, line, text.Width, style, alignment);
            }
            DrawSelection(buf, text, i, offset + i);
        }

        if (vertical)
        {
            var bar = scrollbarOnBorder ? new Rect(area.Right - 1, text.Y, 1, text.Height) : new Rect(text.Right, text.Y, 1, text.Height);
            new ScrollbarBuilder().ContentLength(rows.Count).ViewportLength(text.Height).Position(offset).Draw(buf, bar);
            _state!.TrackScrollbar(bar);
        }
        if (horizontal)
        {
            // Across the padding too (1.x spec §24.2)
            var across = _block?.AcrossPadding(area, text) ?? text;
            var bar = across with { Y = columnBarOnBorder ? area.Bottom - 1 : text.Bottom, Height = 1 };
            new ScrollbarBuilder().Horizontal().ContentLength(widest).ViewportLength(text.Width).Position(columns ?? 0).Draw(buf, bar);
            _state!.TrackColumnScrollbar(bar);
        }
    }

    // 1.x spec §23.3: the columns of a row wider than the view from `from`; without a scrollbar, with … at an edge it
    // continues past
    private static void DrawScrolled(CellBuffer buf, int x, int y, Line line, int from, int width, Style style, bool marks)
    {
        var spans = new List<Span>();
        var column = 0;
        foreach (var span in line.Spans)
        {
            var content = Sanitizer.SingleLine(span.Content ?? "");
            var text = new System.Text.StringBuilder();
            for (var i = 0; i < content.Length;)
            {
                var length = StringInfo.GetNextTextElementLength(content, i);
                var w = UnicodeWidth.OfGrapheme(content.AsSpan(i, length));
                if (w > 0 && column >= from && column + w <= from + width) text.Append(content, i, length);
                else if (column < from && column + w > from) text.Append(' ', column + w - from); // the right half of a wide character
                column += w;
                i += length;
            }
            if (text.Length > 0) spans.Add(new Span(text.ToString(), span.Style));
        }
        WidgetText.DrawLine(buf, x, y, new Line([.. spans]), width, style, MiniTui.Alignment.Left);
        if (!marks) return;
        if (from > 0) buf.SetString(x, y, WidgetText.Ellipsis, style, 1);
        if (line.Width > from + width) buf.SetString(x + width - 1, y, WidgetText.Ellipsis, style, 1);
    }

    // 1.x spec §20.5: the selected cells of a row, focused unless the block says otherwise
    private void DrawSelection(CellBuffer buf, Rect inner, int y, int row)
    {
        if (_state is not { HasSelection: true } state) return;
        var (from, to) = state.SelectedColumns(row);
        from = Math.Max(from, 0);
        to = Math.Min(to, inner.Width);
        if (to <= from) return;
        var style = _block?.IsFocused == false ? Theme.Current.SelectionUnfocused : Theme.Current.Selection;
        buf.SetStyle(new Rect(inner.X + from, inner.Y + y, to - from, 1), style);
    }
}

/// <summary>The line breaking behind <see cref="ParagraphBuilder"/>.</summary>
internal static class TextWrap
{
    /// <summary>The rows of the text at a width; with <paramref name="origins"/>, each row's line and first column in it.</summary>
    public static List<Line> Wrap(Text text, int width, WrapMode mode, List<(int Line, int From)>? origins = null)
    {
        var rows = new List<Line>();
        for (var i = 0; i < text.Lines.Count; i++)
        {
            var line = text.Lines[i];
            if (mode == WrapMode.None || width <= 0)
            {
                rows.Add(line);
                origins?.Add((i, 0));
            }
            else WrapLine(line, width, mode, new RowBuilder(line.Alignment, rows, origins, i));
        }
        return rows;
    }

    private static void WrapLine(Line line, int width, WrapMode mode, RowBuilder row)
    {
        if (mode == WrapMode.Char)
        {
            foreach (var g in Graphemes(line)) AddBreaking(g, width, row);
            row.Emit();
            return;
        }

        var firstRow = true;
        var pending = new List<Grapheme>();
        var pendingWidth = 0;
        foreach (var (word, isSpace) in Tokens(line))
        {
            if (isSpace)
            {
                // Leading whitespace is kept on a line's first row (indentation) and dropped at wrap points
                if (row.Width == 0 && !firstRow) continue;
                pending.AddRange(word);
                foreach (var g in word) pendingWidth += g.Width;
                continue;
            }

            var wordWidth = 0;
            foreach (var g in word) wordWidth += g.Width;

            if (row.Width + pendingWidth + wordWidth <= width)
            {
                row.AddRange(pending);
                row.AddRange(word);
            }
            else
            {
                if (row.Width > 0)
                {
                    row.Emit();
                    firstRow = false;
                }
                if (wordWidth <= width) row.AddRange(word);
                else
                {
                    foreach (var g in word)
                        if (AddBreaking(g, width, row)) firstRow = false;
                }
            }
            pending.Clear();
            pendingWidth = 0;
        }
        row.Emit();
    }

    // Add a grapheme, starting a new row first if it doesn't fit. Returns true if a row was emitted.
    private static bool AddBreaking(Grapheme g, int width, RowBuilder row)
    {
        if (row.Width + g.Width <= width)
        {
            row.Add(g);
            return false;
        }
        if (row.Width == 0) return false; // wider than the whole area: dropped
        row.Emit();
        if (g.Width <= width) row.Add(g);
        return true;
    }

    private static IEnumerable<(List<Grapheme> Graphemes, bool IsSpace)> Tokens(Line line)
    {
        List<Grapheme>? token = null;
        var tokenIsSpace = false;
        foreach (var g in Graphemes(line))
        {
            if (token is not null && g.IsSpace != tokenIsSpace)
            {
                yield return (token, tokenIsSpace);
                token = null;
            }
            token ??= [];
            tokenIsSpace = g.IsSpace;
            token.Add(g);
        }
        if (token is not null) yield return (token, tokenIsSpace);
    }

    private static IEnumerable<Grapheme> Graphemes(Line line)
    {
        var column = 0;
        foreach (var span in line.Spans)
        {
            var content = Sanitizer.SingleLine(span.Content ?? "");
            for (var i = 0; i < content.Length;)
            {
                var length = StringInfo.GetNextTextElementLength(content, i);
                var text = content.Substring(i, length);
                i += length;
                var w = UnicodeWidth.OfGrapheme(text);
                if (w == 0) continue;
                yield return new Grapheme(text, span.Style, w, text.Length == 1 && char.IsWhiteSpace(text[0]), column);
                column += w;
            }
        }
    }

    // Column: where it starts in its line
    private readonly record struct Grapheme(string Text, Style Style, int Width, bool IsSpace, int Column);

    // Builds the rows of one line, adding each one's origin
    private sealed class RowBuilder(Alignment alignment, List<Line> rows, List<(int Line, int From)>? origins, int line)
    {
        private readonly List<Span> _spans = [];
        private readonly System.Text.StringBuilder _current = new();
        private Style _currentStyle;
        private int? _from;
        private int _end;

        public int Width { get; private set; }

        public void Add(Grapheme g)
        {
            _from ??= g.Column;
            _end = g.Column + g.Width;
            if (_current.Length > 0 && g.Style != _currentStyle) Flush();
            _currentStyle = g.Style;
            _current.Append(g.Text);
            Width += g.Width;
        }

        public void AddRange(List<Grapheme> graphemes)
        {
            foreach (var g in graphemes) Add(g);
        }

        public void Emit()
        {
            Flush();
            rows.Add(new Line([.. _spans], alignment));
            origins?.Add((line, _from ?? _end));
            _spans.Clear();
            _from = null;
            Width = 0;
        }

        private void Flush()
        {
            if (_current.Length == 0) return;
            _spans.Add(new Span(_current.ToString(), _currentStyle));
            _current.Clear();
        }
    }
}
