namespace MiniTui;

/// <summary>
/// Which sides of a <see cref="BlockBuilder"/> have a border.
/// </summary>
[Flags]
public enum Borders
{
    /// <summary>No border.</summary>
    None = 0,
    /// <summary>The top border.</summary>
    Top = 1,
    /// <summary>The right border.</summary>
    Right = 2,
    /// <summary>The bottom border.</summary>
    Bottom = 4,
    /// <summary>The left border.</summary>
    Left = 8,
    /// <summary>All four borders.</summary>
    All = Top | Right | Bottom | Left,
}

/// <summary>
/// A border with optional titles and padding, drawn around other widgets.
/// </summary>
/// <example>
/// <code>
/// var inner = Widget.Block().Title(" Services ").Focused().Draw(buf, area);
/// </code>
/// </example>
public sealed class BlockBuilder
{
    private Borders _borders = MiniTui.Borders.All;
    private BoxStyle _borderType = BoxStyle.Single;
    private Line? _title;
    private Alignment _titleAlignment = Alignment.Left;
    private Line? _titleBottom;
    private Margin? _padding;
    private bool _focused;
    private Style? _style;
    private Style? _borderStyle;
    private Style? _titleStyle;

    // Extra columns between the corner and the title, for CellBuffer.DrawBoxWithTitle's 0.7 layout
    private int _titleInset;

    /// <summary>Which sides have a border. The default is <see cref="MiniTui.Borders.All"/>.</summary>
    public BlockBuilder Borders(Borders borders) { _borders = borders; return this; }

    /// <summary>The border glyphs. The default is <see cref="BoxStyle.Single"/>.</summary>
    public BlockBuilder BorderType(BoxStyle type) { _borderType = type; return this; }

    /// <summary>A title in the top border.</summary>
    public BlockBuilder Title(Line? title) { _title = title; return this; }

    /// <summary>Where the titles sit. The default is <see cref="Alignment.Left"/>.</summary>
    public BlockBuilder TitleAlignment(Alignment alignment) { _titleAlignment = alignment; return this; }

    /// <summary>A title in the bottom border.</summary>
    public BlockBuilder TitleBottom(Line? title) { _titleBottom = title; return this; }

    /// <summary>
    /// Space between the border and the inner area. By default, one column inside each side border (left, right)
    /// and none at the top and bottom, unless that would leave fewer than 3 columns (1.x spec §24.2); this replaces
    /// it, and <c>Padding(new Margin(0))</c> removes it.
    /// </summary>
    public BlockBuilder Padding(Margin padding) { _padding = padding; return this; }

    /// <summary>Draw with the focused border and title styles.</summary>
    public BlockBuilder Focused(bool focused = true) { _focused = focused; return this; }

    /// <summary>A style patched over the whole area, including the inside.</summary>
    public BlockBuilder Style(Style style) { _style = style; return this; }

    /// <summary>A style patched over the theme's border style.</summary>
    public BlockBuilder BorderStyle(Style style) { _borderStyle = style; return this; }

    /// <summary>A style patched over the theme's title style.</summary>
    public BlockBuilder TitleStyle(Style style) { _titleStyle = style; return this; }

    internal bool IsFocused => _focused;

    /// <summary>The style the borders are drawn in, for lines that join them.</summary>
    internal Style BorderStyleFor(Theme theme) => (_focused ? theme.BorderFocused : theme.Border).Patch(_borderStyle ?? default);

    internal bool HasRightBorder => (_borders & MiniTui.Borders.Right) != 0;

    /// <summary>A bottom border without a bottom title, where a horizontal scrollbar can go (1.x spec §23.3).</summary>
    internal bool HasFreeBottomBorder => (_borders & MiniTui.Borders.Bottom) != 0 && _titleBottom is null;

    internal BlockBuilder TitleInset(int columns) { _titleInset = columns; return this; }

    internal BlockBuilder Clone() => (BlockBuilder)MemberwiseClone();

    /// <summary>The area inside the borders, titles and padding, without drawing.</summary>
    public Rect Inner(Rect area)
    {
        var bordered = Bordered(area);
        return bordered.Inner(PaddingFor(bordered));
    }

    /// <summary>
    /// Rows laid out in the padded area, widened across the side padding, for a highlight drawn border to border
    /// (1.x spec §24.2).
    /// </summary>
    internal Rect AcrossPadding(Rect area, Rect rows)
    {
        if (rows.IsEmpty) return rows;
        var bordered = Bordered(area);
        var padding = PaddingFor(bordered);
        var x = Math.Max(bordered.X, rows.X - padding.Left);
        var right = Math.Min(bordered.Right, rows.Right + padding.Right);
        return rows with { X = x, Width = Math.Max(0, right - x) };
    }

    // The area inside the borders and titles
    private Rect Bordered(Rect area)
    {
        var top = Has(MiniTui.Borders.Top) || _title is not null ? 1 : 0;
        var bottom = Has(MiniTui.Borders.Bottom) || _titleBottom is not null ? 1 : 0;
        var left = Has(MiniTui.Borders.Left) ? 1 : 0;
        var right = Has(MiniTui.Borders.Right) ? 1 : 0;
        return area.Inner(new Margin(left, top, right, bottom));
    }

    private Margin PaddingFor(Rect bordered)
    {
        if (_padding is { } padding) return padding;
        var left = Has(MiniTui.Borders.Left) ? 1 : 0;
        var right = Has(MiniTui.Borders.Right) ? 1 : 0;
        return bordered.Width - left - right < 3 ? default : new Margin(left, 0, right, 0);
    }

    /// <summary>Draw the block.</summary>
    /// <returns>The inner area, the same as <see cref="Inner"/>.</returns>
    public Rect Draw(CellBuffer buf, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buf);
        var inner = Inner(area);
        if (area.IsEmpty) return inner;

        var theme = Theme.Current;
        if (_style is { } style) buf.SetStyle(area, style);

        var borderStyle = (_focused ? theme.BorderFocused : theme.Border).Patch(_borderStyle ?? default);
        DrawBorders(buf, area, borderStyle);

        var titleStyle = (_focused ? theme.TitleFocused : theme.Title).Patch(_titleStyle ?? default);
        if (_title is { } title) DrawTitle(buf, area, area.Y, title, titleStyle);
        if (_titleBottom is { } bottom) DrawTitle(buf, area, area.Bottom - 1, bottom, titleStyle);
        return inner;
    }

    private bool Has(Borders side) => (_borders & side) != 0;

    private void DrawBorders(CellBuffer buf, Rect area, Style style)
    {
        // Conhost fonts have no rounded corners
        var type = _borderType == BoxStyle.Rounded && Glyphs.Basic ? BoxStyle.Single : _borderType;
        var (tl, tr, bl, br, h, v) = type switch
        {
            BoxStyle.Double => ("╔", "╗", "╚", "╝", "═", "║"),
            BoxStyle.Rounded => ("╭", "╮", "╰", "╯", "─", "│"),
            BoxStyle.Heavy => ("┏", "┓", "┗", "┛", "━", "┃"),
            _ => ("┌", "┐", "└", "┘", "─", "│"),
        };

        var clip = area.Intersect(buf.Area);
        if (clip.IsEmpty) return;

        if (Has(MiniTui.Borders.Left) && area.X >= clip.X)
            for (var y = clip.Y; y < clip.Bottom; y++) Patch(buf, area.X, y, v, style);
        if (Has(MiniTui.Borders.Right) && area.Right - 1 < clip.Right)
            for (var y = clip.Y; y < clip.Bottom; y++) Patch(buf, area.Right - 1, y, v, style);
        if (Has(MiniTui.Borders.Top) && area.Y >= clip.Y)
            for (var x = clip.X; x < clip.Right; x++) Patch(buf, x, area.Y, h, style);
        if (Has(MiniTui.Borders.Bottom) && area.Bottom - 1 < clip.Bottom)
            for (var x = clip.X; x < clip.Right; x++) Patch(buf, x, area.Bottom - 1, h, style);

        if (Has(MiniTui.Borders.Top | MiniTui.Borders.Left, all: true)) Patch(buf, area.X, area.Y, tl, style);
        if (Has(MiniTui.Borders.Top | MiniTui.Borders.Right, all: true)) Patch(buf, area.Right - 1, area.Y, tr, style);
        if (Has(MiniTui.Borders.Bottom | MiniTui.Borders.Left, all: true)) Patch(buf, area.X, area.Bottom - 1, bl, style);
        if (Has(MiniTui.Borders.Bottom | MiniTui.Borders.Right, all: true)) Patch(buf, area.Right - 1, area.Bottom - 1, br, style);
    }

    private bool Has(Borders sides, bool all) => all ? (_borders & sides) == sides : (_borders & sides) != 0;

    private static void Patch(CellBuffer buf, int x, int y, string symbol, Style style) => buf.SetString(x, y, symbol, style, 1);

    private void DrawTitle(CellBuffer buf, Rect area, int y, Line title, Style style)
    {
        var left = (Has(MiniTui.Borders.Left) ? 1 : 0) + _titleInset;
        var right = (Has(MiniTui.Borders.Right) ? 1 : 0) + _titleInset;
        var width = area.Width - left - right;
        WidgetText.DrawLine(buf, area.X + left, y, title, width, style, _titleAlignment);
    }
}
