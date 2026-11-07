# Widgets

The built-in widgets, each a builder that draws into a [`CellBuffer`](xref:MiniTui.CellBuffer).

Factory for fluent widget builders. Every widget draws with `Theme.Current`, measures text by display
width (truncating with `…`), and never throws, whatever the area.

```csharp
Widget.Table()
    .Block(Widget.Block().Title(" Services "))
    .Header("Name", "Status", "Version")
    .Columns(Constraint.Fill(), Constraint.Length(10), Constraint.Length(12))  // layout constraints
    .Rows(services, s => new Row(s.Name, new Line(s.Status, Theme.Current.Success), s.Version))
    .State(selection)                       // SelectionState: scrolled to keep the selection visible
    .Focused()
    .Draw(buf, area);
```

| Builder | What it draws |
|---|---|
| `Block()` | Borders (`Borders.All`, `Top`, …), `BorderType(BoxStyle)`, `Title`/`TitleBottom`, `Padding`, `Focused`. `Draw` returns the inner rect. Content sits a column in from side borders by default; `Padding(...)` replaces that (`Padding(new Margin(0))` for none). Lists, tables and trees still highlight border to border. |
| `Paragraph()` | [`Text`](xref:MiniTui.Text) with `Wrap(WrapMode.None/Word/Char)`, [`Alignment`](xref:MiniTui.Alignment), `Scroll(n)` or `State(ScrollState)`. Unwrapped lines are cut with `…`, or scroll sideways with `ScrollState.ScrollsHorizontally` (see below), with a horizontal scrollbar. `Paragraph.MeasureLines` measures without drawing. |
| `List()` | `Items(list)` or `Items(list, format)` (only visible rows are formatted), or `Items(list, format, key)` to keep the selection on its item (see below), `State(SelectionState)`, `HighlightSymbol("> ")`, `Title`. |
| `Table()` | `Header`, `Columns(constraints)`, `ColumnSpacing`, `Rows` (or `Rows(items, format, key)`, below), `State` (▲/▼ on `SortColumn`), `HighlightSymbol`, `Sortable(columns)` (a header click calls `SelectionState.ToggleSort`, and `SortChanged` tells you to re-sort). 100,000 rows cost the same per frame as 100. |
| `Scrollbar()` | `Vertical`/`Horizontal`, `ContentLength`, `Position`, `ViewportLength`. Hidden when the content fits. |
| `Gauge()` | `Ratio(0..1)` with eighth-cell precision and a percentage (or custom) `Label`. |
| `Sparkline()` | `Data(values)` as `▁▂▃▄▅▆▇█`, newest at the right; optional `Max`. |
| `Canvas()` | Points, lines, rectangles, circles and `Print`ed text in your own coordinates (`XBounds`, `YBounds`; y grows up), drawn in `Paint(p => ...)` with a [`Marker`](xref:MiniTui.Marker): braille (2×4 dots per cell), half blocks, blocks or dots. |
| `Chart()` | A line or scatter plot of [`Dataset`](xref:MiniTui.Dataset)s with an `XAxis`/`YAxis` ([`Axis`](xref:MiniTui.Axis): bounds from the data unless `Min`/`Max` are set, `Labels` or `LabelFormat`, `Title`) and a `Legend` corner. Datasets without a style take `Theme.ChartSeries` in order. `GraphType.Area` fills under a line (draw the larger series first; later ones cover earlier ones). An `Axis` takes `LabelCount`, a `Formatter`, `Ticks` and a `Grid` behind the data; `Axis.Time("HH:mm")` and `Axis.Duration()` label seconds since the epoch (`ChartTime.ToValue(time)`) or seconds. |
| `BarChart()` | `Bars(new Bar(label, value), ...)`, vertical (`BarWidth`, `BarGap`) or `Direction(Direction.Horizontal)`, in eighths of a cell, with the value in or after each bar; optional `Max`, `ValueFormat`. `Groups(new BarGroup(label, bars), ...)` draws each group's bars side by side (`GroupGap` between groups), or as one bar with `Stacked()`; bar i of each group is series i, colored from `Theme.ChartSeries` and named in a `Legend`. |
| `Spinner()` | `Frames(SpinnerFrames.Braille/Line/Dots)` and a `Label`. Animates from the app's clock. |
| `Toasts()` | `Queue(toastQueue)`: up to 3 pop-ups in the top-right corner that expire on their own. |
| `StatusBar()` | `Left` and `Right` text; the right side wins when both don't fit. |
| `TextField()`, `CommandLine()` | Single-line input from [`TextFieldState`](xref:MiniTui.TextFieldState) / [`CommandLineState`](xref:MiniTui.CommandLineState), scrolled by display width, with the terminal cursor shown. |
| `TextArea()` | Multi-line input from [`TextAreaState`](xref:MiniTui.TextAreaState): `Wrap(WrapMode.Word/Char/None)` (word by default; `None` scrolls sideways), `Placeholder`, `Block`, `LineNumbers()` (a gutter). Scrolls to keep the cursor visible; with a block, a draggable scrollbar on its right border when the text doesn't fit. |
| `FilterList(state)` | A list narrowed by typing, from a [`FilterListState`](xref:MiniTui.FilterListState): the filter line (with a muted `Placeholder`), a divider joining the `Block`'s borders, and the matching rows with the matched characters in bold, each row's detail and hint muted, `Empty(text)` when nothing matches, and a scrollbar when they don't fit. The command palette is one. |
| `Select()` | A dropdown field from [`SelectState`](xref:MiniTui.SelectState): the chosen option (or a muted `Placeholder`) and a ▼. Opening it shows a [`PopupMenu`](xref:MiniTui.PopupMenu) of the options below the field; with `SelectState.Filterable`, a dropdown with a filter line, which typing on the field opens. |
| `TabBar()` | `Tabs(...)` with [`TabBarState`](xref:MiniTui.TabBarState), scrolled to keep the selected tab visible. |
| `Tree(state)` | The expanded part of a `TreeState<T>`'s tree, with ▸/▾ expanders, `Indent(n)` or `Guides()` (│ ├─ └─), `Format(node => line)`, `HighlightSymbol`, `Title`. See [Trees](keys-and-state.md#trees). |
| `Button()`, `Checkbox()` | Themed controls: `Label`, `Focused`, `Hover`, and `Pressed(bool)` / `Checked(bool)` or a `State(ButtonState)` / `State(CheckboxState)` that handles keys and clicks (see [Forms](keys-and-state.md#forms)). |

**Selection by key.** `SelectionState.Selected` is an index, so when the items change under it (a re-sort, a
filter, a refresh that adds rows above), it would point at another item. Give `Rows` or `Items` a key and the
selection follows its item instead, staying on the same screen row where it can. If the item is gone, the next one
takes its place, and a move by the user in the same frame wins. `SelectedKey` is the selected item's key, and
`SelectKey(key)` selects an item at the next draw, such as a row just added.

```csharp
Widget.Table()
    .Rows(Sorted(services), s => new Row(s.Name, s.Status), s => s.Id)    // re-sorting keeps the selected service
    .State(selection)
    .Draw(buf, area);

services.Add(created);
selection.SelectKey(created.Id);
```

**Long lines.** A paragraph with `WrapMode.None` cuts long lines with `…`. Give it a `ScrollState` with
`ScrollsHorizontally = true` and they scroll instead: ← and → move 8 columns (and go unhandled at the edge, so
the screen gets them), Home and End go to the first and last column, and the horizontal wheel or Shift or Alt
with the wheel scrolls too. A horizontal scrollbar shows while a row is wider than the view, on the bottom border (or in
the last row when that border has a title), and its thumb drags. Some terminals (Windows Terminal, WezTerm, foot) keep
Shift+wheel for themselves, to get past mouse mode; Alt+wheel works in Windows Terminal. Rows narrower than the view keep their alignment.
`HorizontalOffset` is the first visible column; `ContentWidth` and `ViewportWidth` are set when it's drawn.
It's off by default so that ← → stay free for screens that use them.

**Toggling wrapping.** The builder is made each frame, so a key that flips a flag can switch `Wrap(...)`. A
paragraph with a `ScrollState` keeps its place when its rows are laid out again: the line at the top stays at the
top (and a view at the end stays at the end), and a selection keeps its text. That holds for a resize too.

```csharp
bool _wrap;

public LogView() => Keys.Add("w", "Wrap", () => _wrap = !_wrap);

// In Render
Widget.Paragraph().Text(log).State(_log).Wrap(_wrap ? WrapMode.Word : WrapMode.None).Draw(buf, area);
```

```csharp
readonly ScrollState _log = new() { ScrollsHorizontally = true, Selectable = true };

public override bool HandleKey(ConsoleKeyInfo key) => _log.HandleKey(key);
public override bool HandleMouse(MouseEvent mouse) => _log.HandleMouse(mouse);

public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
    Widget.Paragraph().Text(string.Join("\n", _lines)).State(_log)
        .Block(Widget.Block().Title(" Log ").Focused(ctx.Focused)).Draw(buf, area);
```

```csharp
Widget.Chart()
    .Datasets(
        new Dataset("cpu", cpu),                                   // (double X, double Y) points
        new Dataset("p99", latency) { Type = GraphType.Scatter, Marker = Marker.Dot })
    .XAxis(new Axis { Title = "s", Min = now - 60, Max = now, LabelFormat = "0" })
    .YAxis(new Axis { Title = "%" })                               // bounds from the data
    .Block(Widget.Block().Title(" Load "))
    .Draw(buf, area);

Widget.Canvas()
    .XBounds(-1, 1).YBounds(-1, 1)
    .Paint(p =>
    {
        p.Circle(0, 0, 0.8, Theme.Current.Info);
        p.Line(0, 0, Math.Cos(angle), Math.Sin(angle));
        p.Print(-1, 1, "angle");
    })
    .Draw(buf, area);
```

In conhost (the classic Windows console), whose fonts lack eighth blocks, braille and rounded corners,
widgets switch to half and full blocks, the line spinner and single borders; canvases and charts draw braille
as half blocks. Windows Terminal is unaffected.

Spinners and visible toasts make the app tick (every 100 ms and 250 ms) even with `WithTickRate(null)`,
and only while they're drawn. `toasts.Push(text, ToastLevel.Error)` is thread-safe; each toast shows
for 4 seconds (or the `duration` you pass) from when it first appears. A long message wraps at words, up to 3
lines in a box up to 60 columns wide, and a line break in it starts a new line.

## Box styles

```csharp
Widget.Block().BorderType(BoxStyle.Single).Draw(buf, rect);   // ┌─┐
Widget.Block().BorderType(BoxStyle.Double).Draw(buf, rect);   // ╔═╗
Widget.Block().BorderType(BoxStyle.Rounded).Draw(buf, rect);  // ╭─╮
Widget.Block().BorderType(BoxStyle.Heavy).Draw(buf, rect);    // ┏━┓
```
