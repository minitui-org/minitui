namespace MiniTui;

/// <summary>
/// The key bindings in scope, grouped by component (§9.5). Shown by <see cref="OverlayStack.ShowHelp"/>
/// and the built-in <c>?</c> and <c>f1</c> bindings. <c>?</c>, F1, <c>q</c>, Enter or Esc closes it.
/// </summary>
public sealed class HelpOverlay : Overlay
{
    private readonly Text _text;
    private readonly ScrollState _scroll = new() { Selectable = true };

    internal HelpOverlay(IReadOnlyList<string> lines) =>
        _text = new Text([.. lines.Select(l => l.StartsWith(' ') || l.Length == 0 ? new Line(l) : new Line(l, Theme.Current.Title))]);

    /// <inheritdoc/>
    public override string? Title => " Keys ";

    /// <inheritdoc/>
    public override Rect Placement(Rect screen) => MessageDialog.ContentPlacement(screen, _text, 6);

    /// <inheritdoc/>
    public override bool HandleKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Enter || (key.Key == ConsoleKey.F1 && key.Modifiers == 0) || key.KeyChar is '?' or 'q')
        {
            Close();
            return true;
        }
        return _scroll.HandleKey(key);
    }

    /// <inheritdoc/>
    public override bool HasSelection => _scroll.HasSelection;

    /// <inheritdoc/>
    public override bool HandleMouse(MouseEvent mouse) => _scroll.HandleMouse(mouse);

    /// <inheritdoc/>
    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
        Widget.Paragraph().Text(_text).State(_scroll).Draw(buf, area);
}
