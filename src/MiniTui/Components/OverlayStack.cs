namespace MiniTui;

/// <summary>
/// Modal overlays drawn over the screens (§9.3). While any is open, the top one gets every key.
/// </summary>
/// <remarks>
/// Changes made from another thread while the app runs are posted to the UI thread.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Name from the spec (§9.3); it is a stack.")]
public sealed class OverlayStack
{
    private readonly App _app;
    private readonly List<Overlay> _overlays = [];

    internal OverlayStack(App app) => _app = app;

    /// <summary>The overlay on top, or null.</summary>
    public Overlay? Top => _overlays.Count == 0 ? null : _overlays[^1];

    /// <summary>True while any overlay is open.</summary>
    public bool Any => _overlays.Count > 0;

    /// <summary>The open overlays, bottom first.</summary>
    internal IReadOnlyList<Overlay> All => [.. _overlays];

    /// <summary>Open an overlay on top of the others.</summary>
    /// <exception cref="InvalidOperationException">The overlay is already mounted.</exception>
    public void Show(Overlay overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        if (overlay.IsMounted || overlay.Parent is not null || _overlays.Contains(overlay))
            throw new InvalidOperationException($"{overlay.GetType().Name} is already mounted.");
        _app.OnUiThread(() =>
        {
            _overlays.Add(overlay);
            try
            {
                overlay.Mount(_app);
            }
            catch
            {
                _overlays.Remove(overlay);
                overlay.Unmount();
                throw;
            }
            overlay.Activate();
            // 1.x spec §22.3: an open overlay waits for the user, so a task awaiting it doesn't keep ComponentHarness busy
            _app.BeginUserWait();
            _app.RequestRedraw();
        });
    }

    /// <summary>Close an overlay. Does nothing if it isn't open.</summary>
    public void Close(Overlay overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        _app.OnUiThread(() =>
        {
            if (!_overlays.Remove(overlay)) return;
            try
            {
                overlay.Deactivate();
            }
            finally
            {
                overlay.Unmount();
                _app.EndUserWait();
                _app.RequestRedraw();
            }
        });
    }

    /// <summary>
    /// Ask a yes/no question. Enter on Yes and <c>y</c> answer true; Enter on No, <c>n</c>, Esc and closing the
    /// dialog answer false; <c>y</c> and <c>n</c> answer whatever the labels are. Yes is selected when it opens, or No
    /// with <paramref name="yesFirst"/> false, for a destructive action. Awaited on the UI thread, the continuation
    /// runs there.
    /// </summary>
    public Task<bool> ConfirmAsync(string message, string yes = "Yes", string no = "No", bool yesFirst = true)
    {
        var dialog = new ConfirmDialog(message, yes, no, yesFirst);
        Show(dialog);
        return dialog.Result;
    }

    /// <summary>
    /// Show a <see cref="PopupMenu"/> next to <paramref name="anchor"/> (1.x spec §7). The task answers the chosen
    /// item's index, or null if the menu was dismissed. With no items it shows nothing and answers null. Awaited on
    /// the UI thread, the continuation runs there.
    /// </summary>
    public Task<int?> ShowMenuAsync(IReadOnlyList<MenuItem> items, Rect anchor, int selected = 0)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) return Task.FromResult<int?>(null);
        var menu = new PopupMenu(items, anchor, selected);
        Show(menu);
        return menu.Result;
    }

    /// <summary>
    /// Ask for a line of text (1.x spec §25.4). Enter answers the text; Esc and closing the dialog answer null.
    /// <paramref name="validate"/>, given the text on Enter, returns a message to show under the field (keeping the
    /// dialog open) or null to accept it. Awaited on the UI thread, the continuation runs there.
    /// </summary>
    public Task<string?> PromptAsync(string title, string text = "", Func<string, string?>? validate = null, string? placeholder = null)
    {
        var dialog = new PromptDialog(title, text, validate, placeholder);
        Show(dialog);
        return dialog.Result;
    }

    /// <summary>Show a scrollable message. The task completes when it's closed (Enter or Esc).</summary>
    public Task ShowMessageAsync(string title, Text body)
    {
        var dialog = new MessageDialog(title, body);
        Show(dialog);
        return dialog.Closed;
    }

    /// <summary>Show the key bindings in scope on the current focus chain, grouped by component (§9.5).</summary>
    public void ShowHelp() => _app.OnUiThread(() => Show(new HelpOverlay(KeyRouter.HelpLines(_app))));

    /// <summary>
    /// Show a <see cref="CommandPalette"/> over <see cref="CommandPalette.ItemsFor"/>: the key bindings in scope and
    /// the commands of <paramref name="commands"/> (1.x spec §13). Not bound by default:
    /// <c>app.Keys.Add("ctrl+p", "Commands", () => app.Overlays.ShowPalette())</c>.
    /// </summary>
    public void ShowPalette(CommandRegistry? commands = null) =>
        _app.OnUiThread(() => Show(new CommandPalette(CommandPalette.ItemsFor(_app, commands))));
}
