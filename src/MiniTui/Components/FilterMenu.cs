namespace MiniTui;

/// <summary>
/// The dropdown of a filterable <see cref="SelectState"/> (1.x spec §13.6): a filter line over the matching options,
/// placed as <see cref="PopupMenu"/> is. The palette's keys, except that Tab and Esc close it without a choice.
/// </summary>
internal sealed class FilterMenu : Overlay
{
    private const int MaxRows = 12;

    private readonly FilterListState _list;
    private readonly Rect _anchor;
    private readonly int _minWidth;
    private readonly int _optionCount;
    private int? _choice;

    public FilterMenu(IReadOnlyList<string> options, Rect anchor, int? selected, int minWidth, string text)
    {
        _list = new FilterListState(options.Select(o => new FilterRow(o ?? "")), text) { Wraps = true };
        if (text.Length == 0 && selected is { } i) _list.Select(i);
        _list.Filter.Cursor = text.Length;
        _anchor = anchor;
        _minWidth = minWidth;
        _optionCount = options.Count;
    }

    /// <summary>Called with the chosen option's index, or null, once the menu has closed, on the UI thread.</summary>
    internal Action<int?>? Closed { get; init; }

    /// <summary>The filter line and matches, for tests.</summary>
    internal FilterListState List => _list;

    public override bool Framed => false;

    public override bool CloseOnClickOutside => true;

    public override bool CapturesText => true;

    public override string KeysGroup => "Menu";

    // The side is picked for every option, so it doesn't flip as the matches shrink
    public override Rect Placement(Rect screen)
    {
        var width = Math.Min(Math.Max(Math.Max(_minWidth, _list.ContentWidth() + 2), 20), screen.Width);
        var wanted = Math.Clamp(_optionCount, 1, MaxRows) + 4;
        return PopupMenu.PlaceNear(_anchor, screen, width, wanted, _list.WantedRows(MaxRows) + 4);
    }

    public override bool HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape or ConsoleKey.Tab:
                Close();
                return true;
            case ConsoleKey.Enter when KeyInput.IsEnter(key):
                if (_list.Selected is { } item) Choose(item);
                return true;
        }
        _list.HandleKey(key);
        return true;
    }

    public override bool HandlePaste(string text) => _list.HandlePaste(text);

    public override bool HandleMouse(MouseEvent mouse)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        if (_list.Click(mouse) is { } item) Choose(item);
        return true;
    }

    public override void Render(CellBuffer buf, Rect area, RenderContext ctx)
    {
        ArgumentNullException.ThrowIfNull(buf);
        Draw(_list, buf, area, title: null, placeholder: "Type to filter");
    }

    /// <summary>The palette's and the menu's look: a modal block with the title, focused, over the whole area.</summary>
    internal static void Draw(FilterListState list, CellBuffer buf, Rect area, string? title, string? placeholder)
    {
        var theme = Theme.Current;
        var block = Widget.Block()
            .Title(title is { Length: > 0 } t ? new Line(t) : null)
            .BorderStyle(theme.ModalBorder)
            .TitleStyle(theme.ModalTitle);
        new FilterListBuilder(list) { FocusesBlock = false }.Block(block).Placeholder(placeholder).Focused().Draw(buf, area);
    }

    protected override void OnUnmount()
    {
        Closed?.Invoke(_choice);
    }

    private void Choose(int index)
    {
        _choice = index;
        Close();
    }
}
