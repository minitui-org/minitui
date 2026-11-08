# Screens and components

Structure a larger app as a stack of screens built from components, with focus, overlays and scoped key bindings.

A larger app is a stack of screens built from components:

```csharp
using MiniTui;

var app = App.Create();                            // screens get a status bar with key hints and a breadcrumb
app.Keys.Add("q", "Quit", app.Quit);               // global; "?" help and "ctrl+c" quit are built in
app.Screens.Push(new ServicesScreen(api));
app.Run();

sealed class ServicesScreen : Screen
{
    readonly ServicesClient api;
    readonly Loadable<IReadOnlyList<ServiceDto>> services = new();
    readonly SelectionState selection = new();
    public override string Title => "Services";

    public ServicesScreen(ServicesClient api)
    {
        this.api = api;
        // Bindings go in the constructor: OnMount runs again each time the screen is pushed, and Keys keeps them
        Keys.Add("r", "Refresh", () => Load(services, api.ListAsync))
            .Add("enter", "Open", () => App.Screens.Push(new DetailScreen(api, services.Value![selection.Selected])))
            .Add("d", "Delete", () => App.RunTask(_ => DeleteAsync()));
    }

    protected override void OnMount() => Load(services, api.ListAsync);   // cancelled when the screen is popped

    public override bool HandleKey(ConsoleKeyInfo key) => selection.HandleKey(key, services.Value?.Count ?? 0);

    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
        Widget.Table().Header("Name", "Status").Rows(services.Value ?? [], s => new Row(s.Name, s.Status))
            .State(selection).Focused(ctx.Focused).Draw(buf, area);

    async Task DeleteAsync()
    {
        var svc = services.Value![selection.Selected];
        if (!await App.Overlays.ConfirmAsync($"Delete {svc.Name}?")) return;
        await api.DeleteAsync(svc.Id, Lifetime);       // resumes on the UI thread
        App.Toasts.Push($"Deleted {svc.Name}", ToastLevel.Success);
        Load(services, api.ListAsync);
    }
}
```

Components are plain classes that hold state, handle keys and render every frame; there's no retained
widget tree. A parent renders its children explicitly with `ctx.Render(child, buf, rect)`, which also records
where the child is, so the mouse can click it.

```csharp
sealed class SearchBox : Component
{
    readonly TextFieldState text = new();
    public override bool CapturesText => true;           // typing "q" here doesn't trigger global "q"
    public override bool HandleKey(ConsoleKeyInfo key) => text.HandleKey(key);
    public override bool HandlePaste(string pasted) => text.Paste(pasted);
    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
        Widget.TextField().State(text).Focused(ctx.Focused).Block(Widget.Block().Title(" Search ")).Draw(buf, area);
}

sealed class FilesScreen : Screen
{
    readonly SearchBox search;
    readonly Component list;

    public FilesScreen()
    {
        search = AddChild(new SearchBox());
        list = AddChild(Component.From((buf, area, ctx) => { /* draw */ }, key => false));
        Focus.Add(list).Add(search);                       // Tab cycles; the first one starts focused
        Keys.Add("ctrl+r", "Reload", Reload);
    }

    public override void Render(CellBuffer buf, Rect area, RenderContext ctx)
    {
        var rows = Layout.Split(area, Direction.Vertical, Constraint.Length(3), Constraint.Fill());
        ctx.Render(search, buf, rows[0]);
        ctx.Render(list, buf, rows[1]);
    }

    void Reload() { }
}
```

- **Lifecycle:** `OnMount` runs when a component joins a mounted screen (parents before children);
  `OnUnmount` when it leaves (children first, after `Lifetime` is cancelled). `OnActivated`/`OnDeactivated`
  run when its screen becomes or stops being the top one. `Load(loadable, fetch)` ties a load to `Lifetime`,
  so popping a screen cancels its loads and ignores their results.
- **Keys** go to the `App.OnKey` hook; then, if an overlay is open, only to it (Esc closes it); otherwise
  along the focus chain from the innermost focused component outwards (`HandleKey`, then `Keys`), then
  `App.Keys`; finally an unhandled Esc pops the screen. An unhandled Tab cycles the innermost focus scope.
  Popping the last screen quits.
- **Going back:** on a pushed screen the hints show `esc Back`, unless something in scope binds Esc. A popped
  screen can be pushed again and keeps its state; its `OnMount` runs again with a new `Lifetime`.
- **Hints and help** list the bindings in `Keys`. Keys a component handles itself in `HandleKey` (a list's
  arrows, say) don't appear; add a binding with `showInHints: false` if you want one in the help only. `?` and
  F1 open help; while a text field has focus, `?` is typed, so the hints show `f1 Help` and leave out other
  single-character bindings that would be typed too.
- **Chrome:** the bottom row shows hints for the bindings in scope (an inner `r` hides an outer `r`) and,
  in the room the hints leave, the screen titles. Turn it off with `WithChrome(statusBar: false)`.
- **Overlays:** `app.Overlays.ConfirmAsync(message)` (Yes is selected first, or No with `yesFirst: false` for a
  destructive action; `y` and `n` answer even when you pass other labels), `PromptAsync(title, text, validate)`
  (a line of text, or null for Esc; see below), `ShowMessageAsync(title, text)`,
  `ShowMenuAsync(items, anchor)`, `ShowHelp()`, `ShowPalette(registry)`, or `Show(new CommandLineOverlay(registry))` and your own
  [`Overlay`](xref:MiniTui.Overlay) subclasses. An overlay with `Framed => false` draws its own border; one with
  `CloseOnClickOutside => true` closes when the user clicks elsewhere.
- **Menus:** `ShowMenuAsync` opens a [`PopupMenu`](xref:MiniTui.PopupMenu) below the anchor (above it when there's more room there) and
  answers the chosen index, or null when dismissed. ↑ ↓ PgUp PgDn Home End and type-ahead move; Enter or a
  click chooses; Esc, Tab or a click outside closes. Items can also have:
  - a **mnemonic**, `&` before a letter in the label (`"&Save"`, `"&&"` for a literal `&`): drawn underlined,
    and typing it (or Alt+it) chooses the item at once;
  - a **check or radio mark**, `Checked = true/false`, with `Radio = true` for ● / ○;
  - a **submenu**, `Submenu = [...]`, opened beside the item with → Enter or a click, and closed with ← or Esc;
  - an action, `Run`, called after every open menu has closed. `PopupMenu.Chosen` gives the chosen item at any
    depth.

  A right-click makes a context menu:

  ```csharp
  public override bool HandleMouse(MouseEvent mouse)
  {
      if (mouse is not { Action: MouseAction.Press, Button: MouseButton.Right }) return false;
      MenuItem[] items = [new("Rename") { Hint = "F2" }, MenuItem.Separator, new("Delete") { Enabled = canDelete }];
      App.RunTask(async _ =>
      {
          if (await App.Overlays.ShowMenuAsync(items, new Rect(mouse.X, mouse.Y, 1, 1)) is { } choice) Run(choice);
      });
      return true;
  }
  ```
- **Menu bar:** `app.WithMenuBar(new MenuBar(menus))` puts a row of menus on the top line, above the
  screens. F10 opens the first menu, Alt and a title's mnemonic opens that one (after the focus chain and
  `App.Keys`, so your own Alt bindings win), and a click on a title opens or closes it. In an open menu, ← →
  move between menus and F10 or Esc closes it. A [`Menu`](xref:MiniTui.Menu) takes a fixed item list, or a factory run at each
  opening so marks follow your state; the palette lists every menu item that has a `Run`:

  ```csharp
  app.WithMenuBar(new MenuBar(
      new Menu("&File", [new("&Save") { Hint = "Ctrl+S", Run = Save }, MenuItem.Separator, new("&Quit") { Run = app.Quit }]),
      new Menu("&View", () => [new("&Wrap") { Checked = _wrap, Run = () => _wrap = !_wrap }])));
  ```
- **Command palette:** `ShowPalette()` opens a [`CommandPalette`](xref:MiniTui.CommandPalette) listing every key binding in scope (the ones
  help shows, hidden ones included) with its group and key, plus the commands of an optional [`CommandRegistry`](xref:MiniTui.CommandRegistry).
  Typing filters them with fuzzy matching (matched letters in bold), ↑ ↓ PgUp PgDn move, and Enter or a click
  runs the item once the palette has closed. A command with a usage opens the command line with its name
  typed. An action that's both a binding and a menu item is listed once, with its key, when both were given the
  same method (`Run = Save` and `Keys.Add("ctrl+s", "Save", Save)`); two lambdas that call it are two actions. Nothing is bound by default; `Fuzzy.Match(pattern, text)` is public for filtering your own lists, and
  `new CommandPalette(items)` takes [`PaletteItem`](xref:MiniTui.PaletteItem)s of your own:

  ```csharp
  app.Keys.Add("ctrl+p", "Commands", () => app.Overlays.ShowPalette(commands));
  ```

## Asking for a line of text

`PromptAsync` asks for a name, a search term or a value. `validate` runs on Enter: a message keeps the dialog open
and shows under the field until the text changes, and null accepts it:

```csharp
Keys.Add("f2", "Rename", () => App.RunTask(async _ =>
{
    var name = await App.Overlays.PromptAsync("Rename", current,
        text => text.Trim().Length == 0 ? "A name can't be empty" : null);
    if (name is not null) Rename(current, name);
}));
```

## A dialog that returns a value

For anything more, a dialog is an [`Overlay`](xref:MiniTui.Overlay) subclass. Give it a task the caller awaits, completed when it
closes. Esc closes an overlay (unless `CloseOnEscape` is false), so complete the task in `OnUnmount` too:

```csharp
sealed class NameDialog : Overlay
{
    readonly TaskCompletionSource<string?> result = new();
    readonly TextFieldState name = new();

    public Task<string?> Result => result.Task;
    public override string? Title => "Your name";
    public override bool CapturesText => true;             // typing q types a q
    public override Rect Placement(Rect screen) => Centered(screen, 40, 3);

    public override bool HandleKey(ConsoleKeyInfo key)
    {
        if (key.Key != ConsoleKey.Enter) return name.HandleKey(key);
        result.TrySetResult(name.Text);
        Close();
        return true;
    }

    public override bool HandlePaste(string text) => name.Paste(text);

    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
        Widget.TextField().State(name).Focused().Draw(buf, area);

    protected override void OnUnmount() => result.TrySetResult(null);   // Esc closes it without a name
}
```

Show it and await the answer from `RunTask`, so the code after the `await` runs on the UI thread:

```csharp
Keys.Add("a", "Add", () => App.RunTask(async _ =>
{
    var dialog = new NameDialog();
    App.Overlays.Show(dialog);
    if (await dialog.Result is { Length: > 0 } name) App.Toasts.Push($"Hello, {name}");
}));
```

Inside a dialog, Tab moves between the components in its `Focus`, as on a screen.

## Updating on a timer

To update something every second, post from a timer. `App.Post` runs the update on the UI thread and redraws;
dispose of the timer when the screen's `Lifetime` ends:

```csharp
protected override void OnMount()
{
    var timer = new Timer(_ => App.Post(() => samples.Add(ReadCpu())), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    Lifetime.Register(timer.Dispose);
}
```

A loop in `RunTask` (`while (true) { await Task.Delay(...); ... }`) works in the app too, but
`ComponentHarness` waits for `RunTask` work to finish, so it would never be idle in a test.
