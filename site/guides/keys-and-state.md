# Keys and widget state

Key bindings, and the state objects behind interactive widgets: lists, text fields, trees, selection and the clipboard.

Widget state is a mutable class that handles its own keys; widgets write render-time facts (viewport
size, scroll offset) back into it while drawing.

| State | Keys | Used by |
|---|---|---|
| [`SelectionState`](xref:MiniTui.SelectionState) | ↑ ↓ j k PgUp PgDn Home End, plus `Next`/`Previous`/`PageDown`/`First`/`Last` | List, Table |
| [`ScrollState`](xref:MiniTui.ScrollState) | ↑ ↓ j k PgUp PgDn Home End; with `ScrollsHorizontally`, ← → (and Shift or Alt with the wheel) scroll unwrapped text sideways by 8 columns (unhandled at the edge) and Home End go to the first and last column too | Paragraph, MessageDialog |
| [`TextFieldState`](xref:MiniTui.TextFieldState) | Any text (including non-ASCII and emoji), ← → Home End, Ctrl+← → by word, Backspace/Delete by character, Ctrl+Backspace or Ctrl+W (word), Ctrl+U / Ctrl+K (to start / end); Shift with the moves selects, Ctrl+A selects all, Ctrl+C / Ctrl+X / Ctrl+V copy, cut and paste, Ctrl+Z / Ctrl+Y undo and redo (see [Selection and the clipboard](keys-and-state.md#selection-and-the-clipboard)) | TextField |
| [`CommandLineState`](xref:MiniTui.CommandLineState) | The above, plus Enter (submit), Esc (clear), ↑ ↓ (history: immutable, capped at `MaxHistory`) | CommandLine |
| [`TextAreaState`](xref:MiniTui.TextAreaState) | The `TextFieldState` keys across lines, plus Enter (new line), ↑ ↓ by wrapped row, PgUp PgDn, Home End by row then line, Ctrl+Home End, all of them with Shift to select; `Paste` keeps line breaks; `HandleMouse` places the cursor and scrolls; opt-in `TabIndents` (Tab and Shift+Tab indent and outdent the selected lines, Backspace deletes an indent level), `AutoIndent` (Enter keeps the indentation) and `IndentSize`; Ctrl+F opens a find bar on the last row (smart case, Enter/↓/F3 next, Shift+Enter/↑/Shift+F3 previous, Esc closes; `FindText`, `FindNext`, `MatchCount`, `FindEnabled = false` to leave Ctrl+F and F3 to the app) | TextArea |
| [`FilterListState`](xref:MiniTui.FilterListState) | Typing filters the rows by fuzzy match, best first (all of them in order while the filter is empty); ↑ ↓ PgUp PgDn and Ctrl+Home Ctrl+End move the selection (`Wraps` to wrap), Home End and the other text keys edit the filter line, Esc clears it; Enter and Tab are the caller's. With `TypeToFilter` off, `j` `k` move and `/` starts the filter (below). `SetRows` replaces the rows, keeping the selection by `Key`; `SelectionChanged`, and `Activated` on a double-click | FilterList |
| [`SelectState`](xref:MiniTui.SelectState) | Enter, Space, ↑ ↓, Alt+↓ or F4 opens the menu (pass the [`App`](xref:MiniTui.App): `HandleKey(key, App)`); `HandleMouse` opens it on a click; `Changed` fires when the user picks another option. With `Filterable`, a typed letter opens it too, and typing in it narrows the options | Select |
| [`TabBarState`](xref:MiniTui.TabBarState) | ← → h l | TabBar |
| [`CheckboxState`](xref:MiniTui.CheckboxState) | Space; `HandleMouse` toggles on a click on the box or label; `Changed` | Checkbox |
| [`ButtonState`](xref:MiniTui.ButtonState) | Enter, Space; `HandleMouse` presses on a release over it, cancels when dragged off; `Pressed`, `IsDown` | Button |
| `TreeState<T>` | ↑ ↓ PgUp PgDn Home End, → (expand, then first child), ← (collapse, then parent), Enter (toggle, or `Activated` on a leaf), letters jump by name; `HandleMouse` selects, toggles on the expander and scrolls | Tree |

[`KeyMap`](xref:MiniTui.KeyMap) binds keys to actions and produces hints and help text from the same list:

```csharp
var keys = new KeyMap()
    .Add("r", "Refresh", Refresh)
    .Add("j,down", "Down", () => selection.Next(count), group: "Navigation")
    .Add("ctrl+s", "Save", Save)
    .Add("q,esc", "Quit", app.Quit);

app.OnKey(keys.Handle);                                    // first match wins
Widget.StatusBar().Left(keys.Hints(40)).Draw(buf, status); // "r Refresh  j Down  ctrl+s Save  q Quit"
_ = app.Overlays.ShowMessageAsync("Keys", string.Join("\n", keys.HelpLines()));
```

A multi-line field is a component around a `TextAreaState`. Enter is a line break and Tab moves focus, so
submit with a binding such as Ctrl+S; tabs in pasted text become spaces:

```csharp
sealed class Notes : Component
{
    public TextAreaState State { get; } = new();
    public override bool CapturesText => true;
    public override bool HandleKey(ConsoleKeyInfo key) => State.HandleKey(key);
    public override bool HandlePaste(string text) => State.Paste(text);
    public override bool HandleMouse(MouseEvent mouse) => State.HandleMouse(mouse);
    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
        Widget.TextArea().State(State).Placeholder("Notes").Block(Widget.Block()).Focused(ctx.Focused).Draw(buf, area);
}
```

For editing a config file or a script, turn on the editor features. With `TabIndents`, Tab no longer moves focus
out of the text area, so give users another way out (the mouse, or a key of the app's):

```csharp
public TextAreaState State { get; } = new() { TabIndents = true, AutoIndent = true, IndentSize = 2 };
// ...
Widget.TextArea().State(State).LineNumbers().Block(Widget.Block().Title(" config.yaml ")).Focused(ctx.Focused).Draw(buf, area);
```

To color parts of the text (syntax, Markdown, errors), give the builder a `Highlight` callback. It gets a line's
index and text and returns [`StyledRange`](xref:MiniTui.StyledRange)s, each patched over the cells, so a range that
sets only a foreground color keeps the selection's background. It's called for the visible lines at every draw, so
keep it quick. A small Markdown highlighter:

```csharp
static readonly Regex Emphasis = new(@"\*\*[^*]+\*\*|`[^`]+`");

static IEnumerable<StyledRange> Markdown(int line, string text)
{
    if (text.StartsWith('#')) return [new StyledRange(0, text.Length, new Style(Color.Cyan).Bold())];
    return Emphasis.Matches(text).Select(m => new StyledRange(m.Index, m.Length,
        m.Value[0] == '`' ? new Style(Color.Yellow) : Style.Default.Bold()));
}

// ...
Widget.TextArea().State(State).Highlight(Markdown).Block(Widget.Block()).Focused(ctx.Focused).Draw(buf, area);
```

The callback sees one line at a time. For something spanning lines, such as a fenced code block, work out which
lines are inside one when the text changes (`TextAreaState.Changed`) and look the line up by its index.

A dropdown is a component around a `SelectState`. The state opens a menu overlay, so it takes the `App`:

```csharp
sealed class PlanField : Component
{
    public SelectState State { get; } = new(["Free", "Pro", "Team"]);
    public override bool HandleKey(ConsoleKeyInfo key) => State.HandleKey(key, App);
    public override bool HandleMouse(MouseEvent mouse) => State.HandleMouse(mouse, App);
    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
        Widget.Select().State(State).Placeholder("Choose a plan").Block(Widget.Block()).Focused(ctx.Focused).Draw(buf, area);
}
```

A list narrowed by typing, like the palette but on a screen, is a component around a `FilterListState`. Each
[`FilterRow`](xref:MiniTui.FilterRow) has a label (what's matched), optional muted detail and right-aligned hint, and
a key, so replacing the rows when the data changes keeps the selection on the same item:

```csharp
sealed class NoteList(Func<IEnumerable<Note>> notes) : Component
{
    public FilterListState State { get; } = new();
    public override bool CapturesText => true;
    public void Refresh() => State.SetRows(notes().Select(n => new FilterRow(n.Name, Hint: n.Changed.ToString("MMM d"), Key: n.Id)));
    protected override void OnMount()
    {
        Refresh();
        Keys.Add("enter", "Open", () => { if (State.Selected is { } row) Open(State.Rows[row]); });
    }
    public override bool HandleKey(ConsoleKeyInfo key) => State.HandleKey(key);
    public override bool HandlePaste(string text) => State.HandlePaste(text);
    public override bool HandleMouse(MouseEvent mouse) => State.HandleMouse(mouse);
    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
        Widget.FilterList(State).Placeholder("Filter").Block(Widget.Block().Title("Notes")).Focused(ctx.Focused).Draw(buf, area);
}
```

A row's `Style` and `HintStyle` are patched over its label and hint, so color can say what text otherwise would. The
selection and the matched characters are drawn over them:

```csharp
new FilterRow(task.Title, Hint: task.Due.ToString("MMM d"), Key: task.Id)
{
    Style = task.Done ? Theme.Current.TextMuted : null,
    HintStyle = task.Overdue ? Theme.Current.Error : null,
}
```

By default every letter typed filters, which suits a list opened to find one thing. On a screen people stay on,
turn `TypeToFilter` off and the list has the keys instead: `j` and `k` move as ↓ and ↑ do, Home and End go to the
first and last match, and other letters are left to you to bind. `/` or a click on the filter line starts
filtering; Enter then keeps the filter and Esc clears it, and either gives the keys back to the list. `Filtering`
says which it is, so `CapturesText` can follow it and `?` opens help while the list has the keys:

```csharp
public FilterListState State { get; } = new() { TypeToFilter = false };
public override bool CapturesText => State.Filtering;
// in Render: .Placeholder("/ to filter")
```

## Forms

A form is a screen with a component per field in its `Focus`, so Tab and Shift+Tab move between them. There's no
form widget or validation framework: check the values when the user submits, show the errors in `Render`, and
focus the first bad field with `Focus.Focus(field)`:

```csharp
void Submit()
{
    nameError = name.State.Text.Trim().Length == 0 ? "A name is required" : null;
    if (nameError is not null) { Focus.Focus(name); return; }
    App.Screens.Push(new NextStepScreen(profile));
}
```

`TextFieldState.Changed` fires after the text changes (typing, deleting, pasting, undo, or setting `Text`), to
validate or filter as the user types. `TextAreaState.Changed` does the same for a text area (and indenting), to
autosave or mark a document as modified; moving the cursor or the selection doesn't raise either. `SelectState` has
`Changed` too.

A checkbox keeps its value in a [`CheckboxState`](xref:MiniTui.CheckboxState) and a button its presses in a
[`ButtonState`](xref:MiniTui.ButtonState); the widget draws from the state and records where it was drawn, so the
state handles the keys and clicks. `CheckboxState.Changed` fires when the user toggles it (Space, or a click on
the box or label), not when code sets `Checked`. `ButtonState.Pressed` fires on Enter or Space, or when a left
click is released over it; a press dragged off it is cancelled, and the button draws pressed while held
(`IsDown`). Keys with modifiers are left alone, so a screen's Ctrl+Enter still reaches it.

```csharp
sealed class CheckboxField(string label) : Component
{
    public CheckboxState State { get; } = new();
    public override bool HandleKey(ConsoleKeyInfo key) => State.HandleKey(key);
    public override bool HandleMouse(MouseEvent mouse) => State.HandleMouse(mouse);
    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
        Widget.Checkbox().Label(label).State(State).Focused(ctx.Focused).Draw(buf, area);
}

sealed class ButtonField : Component
{
    private readonly ButtonState _state = new();
    private readonly string _label;

    public ButtonField(string label, Action pressed)
    {
        _label = label;
        _state.Pressed += (_, _) => pressed();
    }

    public override bool HandleKey(ConsoleKeyInfo key) => _state.HandleKey(key);
    public override bool HandleMouse(MouseEvent mouse) => _state.HandleMouse(mouse);
    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
        Widget.Button().Label(_label).State(_state).Focused(ctx.Focused).Draw(buf, area);
}
```

## Trees

`TreeState<T>` shows your own node objects: give it a function for a node's children and one for its label.
It reads children only for expanded nodes, recomputes the visible rows at every frame, and keeps the expanded
set and the selection by node (pass an `IEqualityComparer<T>` to match rebuilt nodes, say by path):

```csharp
sealed record Folder(string Name, List<Folder> Children);

var tree = new TreeState<Folder>(f => f.Children, f => f.Name) { Roots = [root] };
tree.Activated += (_, e) => Open(e.Node);              // Enter on a node without children
Widget.Tree(tree).Title(" Folders ").Guides().Focused(ctx.Focused).Draw(buf, area);
```

For a slow source, `TreeState.Async` takes a loader that returns a `Task`. A node's children load when it first
expands, with "Loading…" after its label meanwhile; they're kept until `Reload(node)`. A load that throws shows its
message on the row and collapses the node, and expanding it again retries. Loads run as app work
(`App.RunTask`), so `ComponentHarness.RunUntilIdle` waits for them:

```csharp
var files = TreeState.Async<Entry>(
    async (e, ct) => await Task.Run(() => Directory.EnumerateFileSystemEntries(e.Path).Select(p => new Entry(p)).ToList(), ct),
    e => e.Name);
files.HasChildren = e => e.IsDirectory;        // otherwise every unloaded node shows an expander
files.Roots = [new Entry(root)];
```

The synchronous constructor still works for lazy sources too: set `HasChildren` and load in `Expanded`.

`ExpandAll` reads the whole tree, which on a disk or an API can take a while; a node that is its own ancestor
is shown as a leaf, so cycles can't hang it. Letters typed while a tree is focused jump to the next row whose
label starts with them, so they don't reach plain-letter bindings such as `q`; bind tree commands to keys with
Alt or Ctrl (Esc still goes back).

`Reveal(path)` expands the nodes on a path from a root and selects the last one, scrolling to it; it returns
false if the path doesn't match. `Filter` shows only the nodes a predicate matches and their ancestors, drawn
open; it reads the children of collapsed nodes too, so keep filtered trees in memory (with a lazy source it
searches what's loaded). Drive it from a text field:

```csharp
var text = filterField.State.Text;
tree.Filter = text.Length == 0 ? null : f => f.Name.Contains(text, StringComparison.OrdinalIgnoreCase);
tree.Reveal([root, src, app, appCs]);
```

While filtering, ← and the expander close and open rows without touching the expanded nodes, and clearing the
filter selects the nearest node still shown. With async children, `await tree.RevealAsync(path)` loads the folders on the
way first; give the state a comparer by key (a path) so the path's nodes can be made before they load.

`Checkboxes = true` draws `[✓]`, `[ ]` or `[-]` on each row; Space or a click on the box toggles it. Checking a
parent checks everything under it, and a parent shows `[-]` when only some of its children are checked.
`CheckedNodes` lists the checked leaves and fully checked parents (not what's under them), and `CheckedChanged`
fires for each toggle. `IsChecked` and `SetChecked` work from code.

## Selection and the clipboard

Text fields, text areas and command lines select with Shift and the movement keys (or Ctrl+A for
everything); typing, pasting or a delete key replaces the selection, and ← / → collapse it. From code:
`Select`, `SelectAll`, `ClearSelection`, `SelectedText`, `SelectionStart` and `SelectionEnd`.

- **Ctrl+C** copies the selection in the focused field. Without a selection, it quits as always; a
  component that doesn't capture text or have a selection (`Component.HasSelection`), or a key binding, never
  gets Ctrl+C.
- **Ctrl+X** cuts, and **Ctrl+V** pastes what was copied. Copying goes to the system clipboard: on
  Windows through the clipboard API, and on Linux and macOS through the terminal (OSC 52; in tmux, with
  `set -s set-clipboard on`). It's also kept in the app, so copy and paste within it always work. In
  Windows Terminal and conhost, Ctrl+V is the terminal's paste, which pastes the system clipboard.
- **Reading the system clipboard on Linux and macOS:** `app.WithClipboardRead()` makes Ctrl+V ask the terminal
  for its clipboard (an OSC 52 query) and paste the answer. It works in xterm (with `allowWindowOps`), kitty
  (which may ask the user), WezTerm, foot, iTerm2 (with "Applications in terminal may access clipboard") and tmux
  3.3+ (`set -s set-clipboard on`); GNOME Terminal and Windows Terminal don't answer. A refusal pastes the
  in-app text; no answer within 500 ms does too, and the app stops asking. `Clipboard.GetTextAsync()` does the
  same read from code.
- **Ctrl+Insert** copies (and never quits), **Shift+Insert** pastes and **Shift+Delete** cuts, as the CUA keys
  do. Terminals that keep Shift+Insert for their own paste send it as a paste, which works the same.
- **Esc** clears the selection; without one it goes on as before (closing a dialog or going back).
- **Ctrl+Z** undoes and **Ctrl+Y** redoes, by word while typing. Setting [`Text`](xref:MiniTui.Text) clears the history. A
  text field takes Ctrl+Z even with nothing to undo, so with `WithSuspendKey`, Ctrl+Z suspends only
  while focus isn't on text.
- `Clipboard.SetText` and `Clipboard.GetText` use the same clipboard from your own code.
- **Selecting paragraph text:** a `ScrollState` with `Selectable = true` lets the mouse select the text of the
  paragraph drawn with it: press and drag (scrolling past the top or bottom), double-click a word, triple-click a
  row. Ctrl+C or Ctrl+Insert copies the rows as shown, Ctrl+A selects all and Esc clears. A component around it
  overrides `HasSelection => state.HasSelection` so Ctrl+C reaches it. The message and help dialogs are
  selectable, so an error message can be copied.

  Without a mouse, **F7** shows a caret at the top of the view, as caret browsing does: the arrows, Home, End,
  PgUp, PgDn and Ctrl+Home Ctrl+End move it instead of scrolling, Shift with them selects, and Esc (after clearing
  a selection) or F7 hides it. `CaretVisible` shows or hides it from code. The state can't add a hint, so to list
  the key, bind it: `Keys.Add("f7", "Caret", scroll.ToggleCaret)`.

  ```csharp
  sealed class LogView(ScrollState scroll, List<string> log) : Component
  {
      public override bool HasSelection => scroll.HasSelection;
      public override bool HandleKey(ConsoleKeyInfo key) => scroll.HandleKey(key);
      public override bool HandleMouse(MouseEvent mouse) => scroll.HandleMouse(mouse);
      public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
          Widget.Paragraph().Text(string.Join("\n", log)).State(scroll)
              .Block(Widget.Block().Title(" Log ").Focused(ctx.Focused)).Draw(buf, area);
  }
  ```

A binding is `(ctrl|alt|shift "+")* name`, where `name` is a printable character or one of `enter esc
tab backspace delete insert up down left right home end pgup pgdn space f1`–`f12`. A character binding
matches what was typed, so `"R"` and `"shift+r"` both mean Shift+R. `KeyBinding.Parse` throws
`FormatException` for anything else.
