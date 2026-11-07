namespace MiniTui;

/// <summary>An item in a <see cref="CommandPalette"/> (1.x spec §13.3).</summary>
/// <param name="Label">The text that's matched and shown, such as <c>"Refresh"</c>.</param>
/// <param name="Run">Called on the UI thread after the palette closes, when the item is chosen.</param>
public sealed record PaletteItem(string Label, Action Run)
{
    /// <summary>Muted text shown after the label, not matched: a group or a command's description.</summary>
    public string? Detail { get; init; }

    /// <summary>Display-only text drawn right-aligned and muted, such as a key: <c>"ctrl+s"</c>.</summary>
    public string? Hint { get; init; }
}

/// <summary>
/// A searchable list of things to run (1.x spec §13). Typing filters the items with <see cref="Fuzzy.Match"/>;
/// ↑ ↓ PgUp PgDn Ctrl+Home Ctrl+End move the selection; Enter or a click runs the selected item; Esc or a click
/// outside closes it. Show it with <see cref="OverlayStack.ShowPalette"/>, or with <see cref="OverlayStack.Show"/>
/// for items of your own.
/// </summary>
public sealed class CommandPalette : Overlay
{
    private readonly TaskCompletionSource<int?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PaletteItem[] _items;
    private readonly FilterListState _list;
    private int? _choice;

    /// <summary>Create a palette over <paramref name="items"/>. Show it with <see cref="OverlayStack.Show"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="items"/> contains null.</exception>
    public CommandPalette(IReadOnlyList<PaletteItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = [.. items];
        if (Array.IndexOf(_items, null) >= 0) throw new ArgumentException("The items can't contain null.", nameof(items));
        _list = new FilterListState(_items.Select(i => new FilterRow(i.Label, i.Detail, i.Hint))) { Wraps = true };
    }

    /// <summary>The chosen item's index in the items, or null when the palette closed without a choice.</summary>
    public Task<int?> Result => _result.Task;

    /// <summary>Shown muted in the empty filter line. Default <c>"Type to search"</c>.</summary>
    public string Placeholder { get; init; } = "Type to search";

    /// <summary>At most this many item rows are shown; more scroll. Default 12.</summary>
    public int MaxRows { get; init; } = 12;

    /// <summary>The filter line and matches, for tests.</summary>
    internal FilterListState List => _list;

    /// <inheritdoc/>
    public override bool Framed => false;

    /// <inheritdoc/>
    public override bool CloseOnClickOutside => true;

    /// <inheritdoc/>
    public override bool CapturesText => true;

    /// <inheritdoc/>
    public override string? Title => " Commands ";

    /// <summary>
    /// The items for the app's current state (1.x spec §13.4, §15.6): the key bindings in scope, innermost first, as
    /// the help overlay lists them (not the binding whose handler is running, which is the one opening the palette),
    /// then the menu bar's items with a <see cref="MenuItem.Run"/>, with their paths, then each command of <paramref name="commands"/> and its subcommands, in add order. A command with a usage
    /// opens a <see cref="CommandLineOverlay"/> with its name typed; one without runs at once. An action listed
    /// more than once (the same delegate: the same method and target, such as a binding and a menu item both given
    /// <c>Save</c>) appears only the first time.
    /// </summary>
    public static IReadOnlyList<PaletteItem> ItemsFor(App app, CommandRegistry? commands = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        var items = new List<PaletteItem>();
        var running = KeyMap.Running;
        foreach (var (group, entries) in KeyRouter.Visible(app))
        {
            foreach (var (entry, keys) in entries)
            {
                if (ReferenceEquals(entry, running)) continue;
                items.Add(new PaletteItem(entry.Description, entry.Handler) { Detail = group, Hint = keys.Split(", ")[0] });
            }
        }
        if (app.MenuBar is { } bar) items.AddRange(bar.PaletteItems());
        if (commands is not null) AddCommands(items, app, commands, commands, "");

        // 1.x spec §22.3: an action that's both a binding and a menu item (the same method on the same object) is
        // listed once, as the first one, which has its key
        var seen = new HashSet<Delegate>();
        return [.. items.Where(item => seen.Add(item.Run))];
    }

    /// <summary>
    /// Centered, near the top, <c>max(40, 60%)</c> of the width, and as tall as the filter line, a divider and the
    /// matches (up to <see cref="MaxRows"/>).
    /// </summary>
    public override Rect Placement(Rect screen)
    {
        var width = Math.Min(screen.Width, Math.Max(40, screen.Width * 60 / 100));
        var y = Math.Min(screen.Y + (screen.Height > 20 ? 2 : 1), screen.Bottom);
        var height = Math.Min(_list.WantedRows(MaxRows) + 4, screen.Bottom - y);
        if (height < 4)
        {
            y = screen.Y;
            height = Math.Min(_list.WantedRows(MaxRows) + 4, screen.Height);
        }
        return new Rect(screen.X + (screen.Width - width) / 2, y, Math.Max(0, width), Math.Max(0, height));
    }

    /// <inheritdoc/>
    public override bool HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape:
                return false; // closes the overlay
            case ConsoleKey.Enter when KeyInput.IsEnter(key):
                if (_list.Selected is { } item) Choose(item);
                return true;
            case ConsoleKey.Tab:
                return true;
        }
        _list.HandleKey(key);
        return true;
    }

    /// <inheritdoc/>
    public override bool HandlePaste(string text) => _list.HandlePaste(text);

    /// <inheritdoc/>
    public override bool HandleMouse(MouseEvent mouse)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        if (_list.Click(mouse) is { } item) Choose(item);
        return true;
    }

    /// <inheritdoc/>
    public override void Render(CellBuffer buf, Rect area, RenderContext ctx)
    {
        ArgumentNullException.ThrowIfNull(buf);
        FilterMenu.Draw(_list, buf, area, Title, Placeholder);
    }

    /// <inheritdoc/>
    protected override void OnUnmount()
    {
        _result.TrySetResult(_choice);
    }

    // 1.x spec §13.3: close first, so the item runs with the focus chain from before the palette opened
    private void Choose(int index)
    {
        _choice = index;
        Close();
        _items[index].Run();
    }

    private static void AddCommands(List<PaletteItem> items, App app, CommandRegistry root, CommandRegistry registry, string prefix)
    {
        foreach (var command in registry.Commands)
        {
            var name = prefix + command.Name;
            var label = ":" + name + (command.Usage is { Length: > 0 } usage ? " " + usage : "");
            Action run = command.Usage is { Length: > 0 }
                ? () => app.Overlays.Show(new CommandLineOverlay(root, name + " "))
                : () => CommandLineOverlay.Execute(app, root, name);
            items.Add(new PaletteItem(label, run) { Detail = command.Description });
            if (command.SubCommands is { } sub) AddCommands(items, app, root, sub, name + " ");
        }
    }
}
