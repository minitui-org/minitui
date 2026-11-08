namespace MiniTui;

/// <summary>
/// Draws a <see cref="MarkdownState"/> (1.x spec §27.3): the Markdown formatted, wrapped to the area, scrolled by the
/// state, with a scrollbar when it overflows and, with <see cref="MarkdownState.BlockCursor"/>, a bar beside the
/// highlighted blocks.
/// </summary>
public sealed class MarkdownBuilder
{
    private const int Gutter = 2;

    private readonly MarkdownState _state;
    private BlockBuilder? _block;
    private string _empty = "";
    private bool _focused;

    internal MarkdownBuilder(MarkdownState state) => _state = state ?? throw new ArgumentNullException(nameof(state));

    /// <summary>Draw a block around the text.</summary>
    public MarkdownBuilder Block(BlockBuilder? block) { _block = block; return this; }

    /// <summary>Muted text shown when there's nothing to show. Empty by default.</summary>
    public MarkdownBuilder Empty(string text) { _empty = text ?? ""; return this; }

    /// <summary>Draw the block's focused border, and the highlight bar in the focused border color rather than the unfocused one.</summary>
    public MarkdownBuilder Focused(bool focused = true) { _focused = focused; return this; }

    /// <summary>Draw the Markdown over <paramref name="area"/>.</summary>
    public void Draw(CellBuffer buf, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buf);
        var theme = Theme.Current;
        var block = _block?.Clone();
        block?.Focused(_focused || block.IsFocused);
        var inner = block?.Draw(buf, area) ?? area;
        if (inner.IsEmpty)
        {
            _state.Arrange(inner);
            return;
        }
        if (_state.Blocks.Count == 0)
        {
            _state.Arrange(default);
            Widget.Paragraph().Text(new Text([new Line(_empty, theme.TextMuted)])).Wrap(WrapMode.Word).Draw(buf, inner);
            return;
        }

        // The bar's columns, then the text, less a column for the scrollbar
        var gutter = _state.BlockCursor ? Math.Min(Gutter, inner.Width - 1) : 0;
        var layout = _state.Layout(inner.Width - gutter - 1, theme);
        var text = inner with { X = inner.X + gutter, Width = inner.Width - gutter };
        _state.Arrange(text);
        Widget.Paragraph().Text(new Text(layout.Rows)).State(_state.Scroll).Draw(buf, text);

        if (_state.Highlight is { } h && gutter > 0)
        {
            var bar = Style.Default.WithFg((_focused ? theme.BorderFocused : theme.Border).Fg ?? Color.Reset);
            for (var row = layout.BlockRows[h.First].First; row <= layout.BlockRows[h.Last].Last; row++)
            {
                var y = inner.Y + row - _state.Scroll.Offset;
                if (y >= inner.Y && y < inner.Bottom) buf.SetString(inner.X, y, "▌", bar, 1);
            }
        }
    }
}
