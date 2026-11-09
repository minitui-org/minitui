# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.5.0] - 2026-11-09

MiniTui 2.5: a filter list can give the list the keys, so `j` and `k` move and `/` starts the filter.

### Added
- `FilterListState.TypeToFilter`, on by default. Turned off, the list has the keys: `j` and `k` move as ↓ and ↑ do, Home and End go to the first and last match, and other letters are left to the caller, so a screen can bind them. `/` or a press on the filter line starts filtering (`Filtering`); Enter then keeps the filter and Esc clears it, and either gives the keys back to the list. The filter line shows its cursor only while filtering.

## [2.4.0] - 2026-11-08

MiniTui 2.4: Markdown. `Widget.Markdown` shows Markdown formatted, F7 shows a caret for selecting text without a mouse, `Paragraph.Wrap` returns a paragraph's rows, and filter list rows can be styled.

### Added
- `Widget.Markdown(state)` and `MarkdownState` show Markdown formatted: headings, emphasis, inline code, links, images as `[image: alt]`, lists with check boxes, nested quotes, fenced and indented code, rules, tables in aligned columns, and front matter hidden; anything else is shown as written. The keys scroll, or with `BlockCursor` move a highlighted block (`Highlight`, `HighlightChanged`, `HighlightedSource`, `HighlightedText`, Ctrl+C copies it), and `PlainText` shows the source as written. `Blocks`, `BlockAt(row)` and `RowsOf(block)` map blocks to rows (#89).
- `Theme.Heading`, `Theme.Code` and `Theme.Link`, set in every built-in theme.
- A caret in selectable text, for selecting without a mouse: in a paragraph or Markdown whose `ScrollState.Selectable` is on, F7 shows it, the arrows, Home, End, PgUp, PgDn and Ctrl+Home Ctrl+End move it, Shift with them selects, and Esc or F7 hides it. `ScrollState.CaretVisible` and `ToggleCaret()`. The message and help dialogs have it too. In Markdown with the block cursor, it starts at the top of the highlight (#90).
- `FilterRow.Style` and `FilterRow.HintStyle` style a filter list row's label and hint, patched over the muted and text styles, with the selection and the matched characters drawn over them (#88).
- `Paragraph.Wrap(text, width, wrap)` returns the rows a paragraph draws, with their styles and alignment, without drawing them, beside `Paragraph.MeasureLines` (#91).

## [2.3.0] - 2026-11-07

MiniTui 2.3: help while typing. F1 opens help as well as `?`, so it works while a text field has focus, and hints show keys that can fire.

### Changed
- F1 opens help as well as `?`, so it works while a text field or text area has focus, where `?` is typed. An app that binds F1 itself keeps it. Help lists `?, f1`, and F1 closes it (#85).
- While a text field has focus, hints show a key that can fire: `f1 Help` instead of `? Help`, a binding's second key when its first is a plain character, and nothing for a binding whose only key would be typed.

## [2.2.0] - 2026-11-07

MiniTui 2.2: gaps found building MiniTui Desk's apps. Apps can save before quitting, on Ctrl+C, a hangup or SIGTERM; text areas report changes and can color parts of their text; a text prompt dialog; the palette's filterable list is public; and long toasts wrap.

### Added
- `TextAreaBuilder.Highlight((line, text) => ranges)` colors parts of a text area's text, for syntax, Markdown or errors. Each `StyledRange(Start, Length, Style)` is patched over its cells, under the search matches, the selection and the cursor. Ranges past the line or empty are ignored, and a range that cuts a wide character or an emoji covers all of it. The callback is called once per visible line at each draw.
- `FilterListState` and `Widget.FilterList(state)`: the command palette's list narrowed by typing, public, for use on a screen as well as in an overlay. `FilterRow` has a label (matched), a muted detail, a right-aligned hint and a key. Typing filters by fuzzy match, best first, and selects the best match; an empty filter shows every row in its own order. ↑ ↓ PgUp PgDn and Ctrl+Home Ctrl+End move the selection (`Wraps` to wrap), Esc clears the filter, and Enter and Tab are the caller's. `SetRows` replaces the rows and keeps the selection on the row with the same key (or label). `SelectionChanged`, and `Activated` on a double-click. The builder takes a `Block`, which the divider joins, a `Placeholder`, `Empty` text and `Focused`. The palette and the filterable select are built on it and look and work as before.
- `OverlayStack.PromptAsync(title, text, validate, placeholder)` and `PromptDialog`, which ask for a line of text. Enter answers the text and Esc answers null. `validate` runs on Enter, and the message it returns shows under the field in the theme's error style, keeping the dialog open until the text changes. The dialog is 44 columns wide, or the screen less 4.
- `App.OnQuitting(reason => ...)`, run on the UI thread before the app quits, so an app with unsaved work can save it or ask first. Ctrl+C and the new `App.RequestQuit()` call it with `QuitReason.Requested`, and returning false keeps the app running. A hangup (SIGHUP, or the console window closing on Windows) and SIGTERM call it with `QuitReason.Hangup` or `QuitReason.Terminated`; the process then ends as before, whatever it returns, and the signal waits up to 2 seconds for it. Nothing is drawn after it, so nothing lands on the shell's screen. Nested apps all get the signal, innermost first. `App.Quit()` doesn't call it. `ComponentHarness.Signal(reason)` delivers a hangup or SIGTERM in tests.
- `TextAreaState.Changed`, raised after the text changes (typing, deleting, pasting, cutting, indenting, undo and redo, or setting `Text` to something else), but not for cursor or selection moves, as `TextFieldState.Changed` is.

### Changed
- A long toast wraps at words, up to 3 lines in its 60-column box, instead of being cut to one line; only a message longer than that is cut, with `…` at the end of the third line. A line break in a toast starts a new line, where it used to become a space.
- Ctrl+C and the built-in `ctrl+c` binding call `RequestQuit()` instead of `Quit()`. Without an `OnQuitting` handler that's the same thing.

### Fixed
- Closing the terminal no longer crashes the app with an `IOException`. The hangup's SIGCONT redrew the screen, and the write to the closed terminal threw before the SIGHUP was handled. An I/O error once the console has hung up is now treated as the hangup.

## [2.1.1] - 2026-11-06

MiniTui 2.1.1: Ctrl+J works on terminals without a keyboard protocol.

### Fixed
- Ctrl+J arrives as Ctrl+J on terminals without a keyboard protocol, so `ctrl+j` bindings work there. The line feed it sends used to decode as Enter. Enter sends a carriage return in raw mode, so it isn't affected, but a key a terminal sends as a line feed (some send one for Ctrl+Enter, or for a Shift+Enter set up by hand) is now Ctrl+J too. So is an Enter pressed before the app has switched the terminal to raw mode, since the kernel has already turned it into a line feed, so that press does nothing.

## [2.1.0] - 2026-11-04

MiniTui 2.1: horizontal scrolling in paragraphs, checkbox and button states, selection by key for lists and tables, padding inside block borders, and clearer bar chart values.

### Added
- Horizontal scrolling in paragraphs: `ScrollState.ScrollsHorizontally` lets an unwrapped paragraph's long lines scroll sideways instead of being cut. ← → and the horizontal wheel (or Shift or Alt with the wheel; Windows Terminal keeps Shift+wheel) scroll 8 columns, Home and End go to the first and last column, and a horizontal scrollbar (on the bottom border, or in the last row) shows while a row is wider than the view, with a draggable thumb. `HorizontalOffset`, `ContentWidth` and `ViewportWidth` give the position. Selection works while scrolled, and dragging past a side scrolls. Off by default. The example's Dashboard log uses it, and `w` toggles wrapping there.
- A paragraph with a `ScrollState` keeps its place when its rows are laid out again (wrapping toggled, or a resize): the line at the top stays at the top, a view at the end stays at the end, and a selection keeps its text instead of pointing at whatever is now in its rows.
- `CheckboxState` and `ButtonState`, so checkboxes and buttons handle their own keys and clicks: `Widget.Checkbox().State(...)` and `Widget.Button().State(...)`. A checkbox toggles on Space or a click on its box or label and raises `Changed` for user toggles only. A button raises `Pressed` on Enter, Space, or a left click released over it, cancels when the press is dragged off, and draws pressed while held (`IsDown`). The example's Form and Widgets screens use them, and the Form has a Submit button.
- Selection by key for lists and tables: `Items(items, format, key)` and `Rows(items, format, key)` keep the selection on the same item when the items are re-sorted, filtered or refreshed, on the same screen row where it can; when the item is gone the next one takes its place, and a move in the same frame wins. `SelectionState.SelectedKey` and `SelectKey(key)`. The example's Services table is keyed by id.
- The package has an icon on nuget.org: the terminal-window logo from minitui.org.

### Changed
- Blocks pad their content one column inside each side border by default, so nothing sits against a border; the top and bottom, sides without a border, and blocks narrower than 3 columns inside are unpadded. `Padding(...)` replaces the default, and `Padding(new Margin(0))` restores the old look. List, table and tree highlights, text input backgrounds and clicks, and a paragraph's horizontal scrollbar span the padding. Built-in dialogs, menus, toasts and chart legends look the same.
- Bar charts: a vertical bar shorter than one cell has its value on the row above it, in the bar's color, instead of over its partial glyph, which cut the bar in two. An empty bar's value takes the bar's color too. A value wider than its bar goes on the row above it, spreading over the gaps and neighbours' empty cells, instead of being dropped. A value is drawn whole or not at all, and never next to another value on its row: the later one is left out, so with `BarGap(0)` neighbouring values no longer run together.

## [2.0.0] - 2026-11-03

MiniTui 2.0: `Buffer` is renamed `CellBuffer`, so samples compile in a default console project, plus fixes found by building four apps from the docs alone. See [Migrating from 1.x to 2.0](https://minitui.org/docs/MIGRATION-1-to-2.html).

### Added
- `TextFieldState.Changed`, raised after the text changes (typing, deleting, pasting, undo and redo, or setting `Text`), but not for cursor or selection moves.
- `ConfirmAsync(message, yes, no, yesFirst)` and `ConfirmDialog(..., yesFirst)`: with `yesFirst: false`, No is selected when the dialog opens, for destructive actions. The example's Services demo asks that way before deleting.
- On a pushed screen, the status bar hints `esc Back` (after the screen's own hints) unless something in scope binds Esc.

### Changed
- **Breaking:** `Buffer` is now `CellBuffer`, so it no longer clashes with `System.Buffer` in projects with implicit usings (every `Render(Buffer buf, ...)` override failed to compile without an alias). Every `Render`, `Draw` and render callback takes a `CellBuffer`. [Migrating from 1.x to 2.0](https://minitui.org/docs/MIGRATION-1-to-2.html) has a one-line `sed`.
- **Breaking (binary):** `ConfirmAsync` and `ConfirmDialog`'s constructor have the new `yesFirst` parameter; source compiles unchanged.
- A tree's type-ahead handles a key only when it matches a row, so `?` and other app keys work while a tree is focused.
- Every click on a tree's expander toggles it; two quick clicks used to open it and leave it open.
- A gauge's label over the empty part is drawn in the theme's text color, not the terminal's default, so it reads with `Theme.Light` on a dark terminal.
- Text fields, text areas and the command line leave Alt with the arrows, Home and End unhandled, so a split's Alt+Shift+arrows work while one is focused.
- The command palette lists an action once when a key binding and a menu item have the same delegate.
- Toasts start below the menu bar instead of covering it.
- The status bar gives the key hints room first, and the breadcrumb what's left.
- Overlay titles are drawn with a space on each side whether or not the title has them.
- Every open overlay counts as waiting for the user, so `ComponentHarness` doesn't time out while a task awaits a custom dialog.

## [1.15.1] - 2026-11-03

MiniTui 1.15.1: the documentation moves to minitui.org, and the repository to github.com/minitui-org/minitui. No code changes.

### Changed
- The documentation moved to [minitui.org](https://minitui.org): guides, an API reference and the changelog. The README is now a summary that links to it, the package's page on nuget.org is a shorter one of its own, and the package's project URL is the site.
- The repository moved to [github.com/minitui-org/minitui](https://github.com/minitui-org/minitui); the old address redirects. New versions publish to GitHub Packages under `minitui-org`.

## [1.15.0] - 2026-11-03

MiniTui 1.15: reading the system clipboard through the terminal, the Insert keys, and selecting paragraph text with the mouse.

### Added
- `App.WithClipboardRead()`: on Linux and macOS, Ctrl+V (and Shift+Insert) in text fields, text areas and command lines asks the terminal for its clipboard with an OSC 52 query and pastes the reply when it arrives. A refusal (an empty reply) pastes the in-app text; no reply within 500 ms does too, and the app stops asking for the rest of the run. The decoder reads OSC 52 replies of any length, never as keys. `Clipboard.GetTextAsync()` reads the same way from code (the Win32 clipboard on Windows).
- Ctrl+Insert copies the selection (and never quits), Shift+Insert pastes, and Shift+Delete cuts (or deletes, without a selection) in text fields, text areas and command lines.
- Selecting paragraph text: `ScrollState.Selectable` lets the mouse select the text of the paragraph drawn with the state (press and drag, scrolling past the edges; a double-click selects a word, a triple-click a row), drawn in `Theme.Selection` (or `SelectionUnfocused` when the paragraph's block isn't focused). `SelectedText` is the rows as shown, a line break between them. `HandleKey` copies with Ctrl+C or Ctrl+Insert, selects all with Ctrl+A and clears with Esc; `SelectAll`, `ClearSelection` and `Copy` do the same from code. When the text changes, the selection stays on its rows, and is cleared when they're gone.
- `Component.HasSelection`: while it's true, Ctrl+C goes to the focused component's `HandleKey` instead of quitting, as it does for one that captures text.
- The message and help dialogs' text can be selected and copied.
- Example: the example app reads the clipboard through the terminal, the Form demo's footer lists the Insert keys, and the Dashboard's log can be selected and copied (and follows new lines while scrolled to the end).
- `docs/SPEC-1.x.md` §20: the clipboard and selection extras spec.

### Changed
- Ctrl+Insert and Shift+Insert used to be unhandled in the text states, and Shift+Delete deleted a cluster even with a selection.
- Esc clears a selection in text fields, text areas, command lines and the command line overlay; without one it goes on as before (closing or going back, or clearing a command line). It used to go on with the selection left in place.
- The message and help dialogs handle a press and drag (selecting) where they ignored them, and Esc clears a selection before it closes them.

## [1.14.0] - 2026-11-03

MiniTui 1.14: grouped and stacked bar charts, time and duration axes, tick marks and grid lines, and area charts.

### Added
- Grouped and stacked bar charts: `BarChartBuilder.Groups(new BarGroup(label, bars), ...)` draws each group's bars side by side, with `GroupGap` between groups and the group's label under them, or, with `Stacked()`, as one bar of segments from the bottom up (left to right), whole cells between segments and eighths at the top, with the group's total in its top cell. Bar i of each group is series i, colored from `Theme.ChartSeries` unless it has its own style. `Max` defaults to the largest bar, or the largest total when stacked.
- `BarChartBuilder.Legend(position)`: a legend of the groups' series (the first group's bar labels), in a corner as `Chart`'s.
- `Axis.LabelCount` (default 3) and `Axis.Formatter`, which formats the default labels instead of `LabelFormat`.
- Time axes: `Axis.Time(format)` labels values as local times (seconds since the Unix epoch) and `Axis.Duration(format)` as durations in seconds, with `-` before negatives. `ChartTime.ToValue` and `FromValue` convert times and durations to and from chart values.
- `Axis.Ticks`: `┬` on the X axis and `┤` on the Y axis at each label. `Axis.Grid`: dotted lines (`┈ ┊ ┼`) across the plot at each label, behind the data, except beside the other axis.
- `GraphType.Area`: a line with the dots under it filled down to the bottom of the plot.
- Example: `g` in the Charts demo cycles the requests chart between single, grouped and stacked bars (GET, POST and DELETE), the line chart's X axis is the last 10 s as a duration axis with ticks and a Y grid, and `a` toggles areas.
- `docs/SPEC-1.x.md` §19: the chart extras spec.

## [1.13.0] - 2026-11-03

MiniTui 1.13: trees you can filter, reveal a node in, check nodes in, and load in the background.

### Added
- `TreeState.Reveal(path)`: expands the nodes on a path from a root (raising `Expanded` for each one that opens), selects the last and scrolls it into view. Returns false, changing nothing, when the path doesn't match the tree or the filter hides its node.
- `TreeState.Filter`: shows only the nodes a predicate matches and their ancestors, drawn open, searching collapsed nodes too. ← and the expander still close and open rows while filtering, without changing the expanded nodes; clearing the filter selects the nearest node still shown. Type-ahead, the keys and the title's count work on the filtered rows.
- `TreeState.Checkboxes`: a `[✓]`, `[ ]` or `[-]` box on each row, toggled with Space or a click on it. Checking a node checks everything under it; a parent is checked when all its children are and partly checked when some are. `IsChecked`, `SetChecked`, `CheckedNodes` (checked leaves and fully checked parents, in tree order) and `CheckedChanged`.
- `TreeState.Async(loadChildren, text, comparer)`: children loaded by a `Task` when a node first expands, with "Loading…" after its label meanwhile, cached until `Reload(node)`. A failed load shows its message on the row and collapses the node; expanding again retries. Loads run through `App.RunTask`. `RevealAsync(path)` loads the nodes on a path, then reveals it.
- Example: the Tree demo has a filter field above the tree, reveals `src/MiniTui/App/App.cs` with Ctrl+G, loads folders in the background, and shows check boxes with Alt+X.
- `docs/SPEC-1.x.md` §18: the tree extras spec.

## [1.12.0] - 2026-11-03

MiniTui 1.12: double- and triple-click, and hover for rows, buttons, tabs and menus.

### Added
- Double- and triple-click: the app counts quick presses of the same button in the same cell and sets `MouseEvent.ClickCount` (1, 2 or 3) on each press, before `App.OnMouse` and the components see it. `App.WithDoubleClickTime(interval)` sets the time between presses (500 ms by default, 100 ms to 2 s). Windows' own `DOUBLE_CLICK` records are counted the same way. `ComponentHarness.DoubleClick(x, y)`.
- A double-click selects the word under the pointer in text fields, text areas and command lines (a run of letters, digits and `_`, of other symbols, or of spaces), and a triple-click selects the line (all of a text field).
- `SelectionState.Activated`: raised when a list or table row is double-clicked. A double-click on a tree row does what Enter does: a leaf raises `TreeState.Activated`, a parent expands or collapses. A double-click on a tree's expander toggles it once.
- Example: double-clicking a log row in the Panes demo moves focus to its details.
- Hover: `App.WithHover()` turns on all-motion mouse reports (`?1003`; conhost's moves without a button) and delivers them as `MouseAction.Move`, routed to `App.OnEvent`, `App.OnMouse` and the component under the pointer without focusing it. Queued moves are coalesced, and a move draws a frame only when the hovered component changes, something handled it, or the hovered components drew with the pointer. `App.PointerPosition`.
- `RenderContext.Hover`: the pointer, for the component under it and its ancestors (null under an overlay or without hover). `Hover(position)` on the list, table, tree, button, checkbox and tab bar builders patches the new `Theme.Hover` over the row, widget or tab under it (never over the selection). `Theme.Hover` is a subtle background in the dark and light themes and an underline in the default and monochrome themes.
- With hover, popup menus, the command palette and the filterable select's list select the item under the pointer, and while a menu bar menu is open, moving over another title switches to it; with none open, the title under the pointer shows hover.
- Example: hover is on, in the demo list, the menus and the demos' lists, tables, trees, buttons, checkboxes and tabs.
- `docs/SPEC-1.x.md` §17: the double-click and hover spec.

### Changed
- The SGR and X10 decoders and the Windows input translator report motion without a button as `MouseAction.Move` instead of dropping it; the app drops moves unless `WithHover` is on.
- The second press of a double-click in a text field or text area selects a word instead of placing the cursor again.

## [1.11.0] - 2026-11-03

MiniTui 1.11: the text area as an editor, with Tab to indent, auto-indent, line numbers, a scrollbar and find.

### Added
- Text area editor features: `TextAreaState.TabIndents` (Tab inserts spaces to the next indent stop, or indents every selected line; Shift+Tab outdents; Backspace in the indentation deletes back to the previous stop; Tab no longer moves focus out of the text area), `AutoIndent` (Enter starts the new line with the current line's indentation) and `IndentSize` (the indent stop width, 1–8, also used to expand tabs; 4 by default). Each indent or outdent is one undo step.
- `TextAreaBuilder.LineNumbers()`: a gutter of line numbers, the cursor's line highlighted. Clicking in it moves the cursor to the start of that row.
- Find in text areas: Ctrl+F opens a find bar on the text area's last row, starting with the selection (within a line) or the last find text. Matches are found as you type, from where the search started; Enter, ↓ and F3 go to the next, Shift+Enter, ↑ and Shift+F3 to the previous, wrapping; Esc closes the bar and keeps the match selected. Smart case: a find text with an uppercase letter matches exactly, otherwise case is ignored. With the bar closed, F3 and Shift+F3 find again. `TextAreaState.FindEnabled`, `IsFinding`, `FindText`, `MatchCount`, `MatchIndex`, `OpenFind`, `CloseFind`, `FindNext` and `FindPrevious`.
- `Theme.SearchMatch`: the other find matches, highlighted while the bar is open; set in every built-in theme.
- Example: an Editor demo, a YAML file in a text area with indentation, auto-indent, line numbers, a scrollbar and find.
- `docs/SPEC-1.x.md` §16: the text area as an editor spec.

### Changed
- Ctrl+F and F3 (with or without Shift) in a focused text area open find and find again, instead of reaching the bindings. Set `FindEnabled = false` to keep them for the app.
- A text area in a block with a right border shows a scrollbar on that border when its text doesn't fit. Dragging the thumb or pressing the track scrolls without moving the cursor.

## [1.10.0] - 2026-11-02

MiniTui 1.10: a menu bar, and menus with mnemonics, check and radio items, and submenus.

### Added
- Richer menu items in `PopupMenu` (and so in context menus): mnemonics (`&` before a letter in `MenuItem.Label`, drawn underlined; typing it, or Alt+it, chooses the item, and items sharing one are cycled through), check and radio marks (`Checked`, `Radio`), submenus (`Submenu`, opened beside the item with → Enter or a click, to the left when there's no room, closed with ← or Esc; Tab or a click outside every menu closes them all), and `Run`, called after every open menu has closed. `MenuItem.Mnemonic` and `PopupMenu.Chosen` (the chosen item at any depth; `Result` is the top-level index).
- `MenuBar`, `Menu` and `App.WithMenuBar(bar)` (`App.MenuBar`): a row of menus on the top line, above the screens and overlays. F10 opens the first menu (an `f10` "Menu" entry in `App.Keys`, listed in help), Alt and a title's mnemonic opens that one after the focus chain and `App.Keys`, and a click on a title opens, switches or closes. In an open menu, ← → move between menus (→ on an item without a submenu too), Alt and another title switches, and F10 or Esc closes. A `Menu` takes a fixed list or a factory run at each opening. `MenuBar.Open(index)` and `IsOpen`.
- The command palette lists the menu bar's items that have a `Run`, with their paths (`File › Export`) as the detail.
- Example: the Command Registry demo's right-click menu has mnemonics and a Sort submenu of radio items, and the example app has a menu bar: File (open any demo, Quit), View (the theme as radio items, the debug overlay as a check item) and Help.
- `docs/SPEC-1.x.md` §15: the menu bar and richer menus spec.

### Changed
- A `&` in a `MenuItem` label now marks a mnemonic and isn't drawn; write `&&` for a literal `&` (a `&` before a space or at the end stays as it is).
- A click outside a menu that was opened from another menu closes both, unless it lands on the other menu.

## [1.9.0] - 2026-11-02

MiniTui 1.9: split panes with draggable dividers, draggable scrollbars, and tables sorted by clicking their headers.

### Added
- `SplitState`, `SplitPane`, `SplitBuilder` and `Widget.Split(state)`: two panes side by side or stacked, with a one-cell divider the user drags with the mouse or moves with Alt+Shift+arrows (`HandleKey`, `MoveBy`). Sized by `Ratio`, or with one pane at a fixed size in cells (`SplitState.Fixed`, `FixedSize`, `FixedPane`) for sidebars; `MinFirst` and `MinSecond` limit how small the user can make each pane; `Changed` fires when they move it. Splits nest.
- Scrollbar dragging: in lists, tables, trees and paragraphs, dragging the scrollbar's thumb scrolls, and a press on its track pages towards it. The selection stays where it is.
- Sortable tables: `TableBuilder.Sortable(columns)` makes a header click call `SelectionState.ToggleSort(column)` (ascending on a new column, then flipping), and `SelectionState.SortChanged` tells the app to re-sort its rows.
- Example: a Panes demo with a fixed sidebar of log sources, a sortable log table and a details pane in nested splits.
- `docs/SPEC-1.x.md` §14: the split panes, scrollbar dragging and sortable tables spec.

### Changed
- A mouse release goes to the component the press hit, as drags do, instead of the one under the pointer.
- A press on a list, table, tree or paragraph scrollbar scrolls instead of doing nothing.
- The wheel over a list, table or tree scrolls the view by a row instead of moving the selection, and scrolling (the wheel or the scrollbar) keeps the selection. Lists, tables and trees scroll the selection back into view when it moves: a key, a click, or setting `Selected`.

## [1.8.0] - 2026-11-02

MiniTui 1.8: a command palette that finds and runs any key binding or command, fuzzy matching, and a select you can type into.

### Added
- `CommandPalette` and `OverlayStack.ShowPalette(commands)`: a searchable list of everything the app can do. By default (`CommandPalette.ItemsFor`) it lists the key bindings in scope, as help does but including hidden ones, with their groups and keys, then the commands and subcommands of an optional `CommandRegistry`. Typing filters them with fuzzy matching and bolds the matched letters; ↑ ↓ PgUp PgDn Ctrl+Home Ctrl+End move; Enter or a click runs the item after the palette closes; Esc or a click outside closes it. A command with a usage opens the command line with its name typed. The binding that opened the palette isn't listed. `PaletteItem` (`Label`, `Run`, `Detail`, `Hint`) for lists of your own. Not bound by default.
- `Fuzzy.Match(pattern, text)` and `FuzzyMatch`: the palette's matcher, ignoring case and spaces in the pattern, favouring prefixes, word starts and runs, with the matched positions for highlighting.
- `SelectState.Filterable`: the dropdown gets a filter line that narrows the options as you type, and a letter typed on the closed field opens it with that letter in the filter.
- Example: Ctrl+P opens the palette everywhere (on the Command Registry screen with its commands too), and the Form demo has a filterable Country select.
- `docs/SPEC-1.x.md` §13: the command palette and filterable select spec.

## [1.7.0] - 2026-11-02

MiniTui 1.7: the app asks the terminal about itself, picks a light or dark theme to match, and turns on keyboard protocols for keys like Ctrl+Enter.

### Added
- Terminal queries: at startup and after a resume, the app asks the terminal for its default colors (OSC 10 and 11), its name (XTVERSION), its keyboard protocol support and its Primary Device Attributes. The input decoder reads the replies and never turns them into keys. `App.TerminalInfo` (`Answered`, `Background`, `Foreground`, `IsDark`, `Keyboard`, `Name`) holds what the terminal said, and `TerminalInfoEvent` reports each change. `WithTerminalQueries(false)` or `MINITUI_QUERIES=0` turns them off.
- Keyboard protocols: where the terminal supports one, the app turns on the kitty keyboard protocol, else xterm's modifyOtherKeys (each only after the terminal's reply says it's supported), or win32-input-mode on Windows (Windows Terminal). Ctrl+Enter, Shift+Enter, Ctrl+Tab, Ctrl+I, Ctrl+M, Ctrl+[ and Ctrl with digits and punctuation then arrive as themselves, and bindings such as `ctrl+enter` match them; every key the classic encoding can express decodes exactly as before, and Esc no longer waits for a timeout under the kitty protocol. The protocol is turned off on every path that restores the terminal. `KeyboardProtocol` and `TerminalInfo.Keyboard` say which is in use; `App.WithKeyboardProtocol(false)` or `MINITUI_KEYBOARD=legacy` turns them off.
- Example: the Form demo also submits with Ctrl+Enter, and the Input demo suggests keys to try.
- `Theme.Dark` and `Theme.Light`: themes with their own RGB colors, the same in every terminal whatever its palette. They set the text and background colors, and the app fills the screen with the background (`Theme.Default` still uses the terminal's palette and background). `App.WithTheme(dark, light)` picks one from the terminal's background (or `COLORFGBG`); the first frame waits up to 150 ms for the reply.
- The debug overlay shows light or dark and the terminal's name. The Input demo shows what the terminal answered, the example app uses `Theme.Dark` or `Theme.Light` by the terminal's background, and the Widgets demo's `t` cycles auto, default, dark, light and monochrome.
- `docs/SPEC-1.x.md` §12: the terminal queries and keyboard protocols spec.

### Changed
- In a terminal with a keyboard protocol, Ctrl+Enter, Alt+Enter, Ctrl+Tab and Ctrl+I arrive as themselves, so `enter` and `tab` bindings no longer match them. The command line, dialogs, menus and trees don't treat Ctrl+Enter or Alt+Enter as Enter, and Ctrl+Tab and Alt+Tab don't move focus. A `ctrl+i` or `ctrl+m` binding no longer matches the Tab or Enter key.
- Widgets that clear an area before drawing (dialogs, toasts, buttons, the status bar) clear it with the theme's text style, so a theme with a background keeps it there. With `Theme.Default` nothing changes.
- In a real terminal, the app now writes its queries at startup and after a resume. Apps with their own `OnEvent` also see `TerminalInfoEvent`.

## [1.6.0] - 2026-11-01

MiniTui 1.6: a tree view over your own nodes, with lazily read children.

### Added
- `Widget.Tree(state)`, `TreeBuilder<T>`, `TreeState<T>`, `TreeRow<T>` and `TreeNodeEventArgs<T>`: a scrollable tree over the app's own nodes. The state asks for children only for expanded nodes and recomputes the visible rows at each frame; `HasChildren` shows an expander before children are loaded, and `Expanded` is where lazy loaders start. Keys: ↑ ↓ PgUp PgDn Home End, → expands or moves to the first child, ← collapses or moves to the parent, Enter toggles a parent or raises `Activated` on a leaf, and letters jump by name (type-ahead). The mouse selects rows, toggles on the ▸/▾ expander and scrolls. `Expand`, `Collapse`, `Toggle`, `ExpandAll`, `CollapseAll`, `IsExpanded`, `Rows`, `SelectionChanged` and `Collapsed`; cycles are shown as leaves. `Guides()` draws │ ├─ └─ lines; in conhost the expanders are `+` and `-`.
- Example: a Tree demo browsing the file system from the current directory, a directory at a time.
- `docs/SPEC-1.x.md` §11: the tree view spec.

## [1.5.0] - 2026-11-01

MiniTui 1.5: text selection, copy and paste, undo and redo, and selecting with the mouse.

### Added
- Text selection in `TextFieldState` and `TextAreaState` (so in `TextField`, `TextArea` and `CommandLine`): Shift with the movement keys, Ctrl+A to select all; typing, pasting and the delete keys replace the selection, and ← / → collapse it. `SelectionAnchor`, `SelectionStart`, `SelectionEnd`, `HasSelection`, `SelectedText`, `Select`, `SelectAll` and `ClearSelection` for code. Selected text is drawn with `Theme.Selection`, or `Theme.SelectionUnfocused` when the widget isn't focused.
- Copy, cut and paste: Ctrl+C (with a selection), Ctrl+X and Ctrl+V, and `Copy()` / `Cut()` on both states. `Clipboard.SetText` and `Clipboard.GetText`: the system clipboard on Windows, OSC 52 on Unix terminals, and an in-app clipboard that always works.
- Undo and redo: Ctrl+Z and Ctrl+Y, `Undo()`, `Redo()`, `CanUndo` and `CanRedo` on both states. Typing undoes a word at a time; up to 100 steps are kept.
- `TextPosition` compares (`IComparable<TextPosition>`, `<`, `>`, `<=`, `>=`).
- `MouseAction.Drag`: with `WithMouse`, the terminal also reports the pointer moving while a button is held (`?1002` instead of `?1000`; `MOUSE_MOVED` records on Windows). Drags go to the component the press hit, even outside it, and are clamped to the screen.
- `TextFieldState.HandleMouse`: a click places the cursor. In both text states, Shift+click extends the selection and dragging selects, scrolling past the edges.
- Example: the Form demo lists the selection and clipboard keys, and its fields select with the mouse.
- `docs/SPEC-1.x.md` §10: the text selection and clipboard spec.

### Changed
- Ctrl+C copies when the focused component captures text and its field has a selection; otherwise it still quits.
- In text fields and text areas, Ctrl+A, Ctrl+X, Ctrl+V, Ctrl+Z and Ctrl+Y are handled by the field and no longer reach key bindings. With `WithSuspendKey`, Ctrl+Z suspends only while focus isn't on a text field or area.
- `TextFieldState.Insert` and `TextAreaState.Insert` replace the selection, if there is one.
- With `WithMouse`, `App.OnMouse` and `OnEvent` also see drag events.

## [1.4.0] - 2026-10-31

MiniTui 1.4: canvas drawing, line and bar charts, and a debug overlay.

### Added
- `Widget.Canvas()`, `CanvasPainter` and `Marker`: draw points, lines, rectangles, circles and text in app coordinates (y grows upward) at up to 2×4 dots per cell with braille, or with half blocks, blocks or dots. Lines are clipped at the edges; where two shapes share a cell, the later one's style wins.
- `Widget.Chart()`, `Dataset`, `Axis`, `GraphType` and `LegendPosition`: line and scatter plots of one or more datasets, with axes, labels, axis titles and a legend in a corner. Axis bounds come from the data unless set, and small areas drop the titles, then the labels, then the axes.
- `Widget.BarChart()` and `Bar`: labelled bars in eighths of a cell, vertical or horizontal, with each bar's value in or after it.
- `Theme.ChartSeries`: the styles of datasets without their own (cyan, yellow, magenta, green, blue, red; one plain style in `Theme.Monochrome`).
- In conhost, canvases and charts draw braille as half blocks.
- Example: a Charts demo with a live line chart (`m` cycles the marker, `s` toggles scatter), a bar chart (`d` toggles the direction) and an animated canvas.
- `docs/SPEC-1.x.md` §8: the Canvas and charts spec.
- A debug overlay: `App.WithDebugKey()` (F12 by default, off unless asked for), `App.ShowDebug`, and `MINITUI_DEBUG=1` to turn both on without code changes. It outlines each component's area (the focus chain and the focused component in their own colors) and shows a panel with the frame time, cells and bytes per frame, the terminal's size and capabilities, the focus chain and the component under the pointer.
- `App.LastFrame` and `FrameStats`: the last frame's number, render and flush times, cells changed, bytes written and whether it was a full redraw.
- Example: F12 toggles the debug overlay.
- `docs/SPEC-1.x.md` §9: the debug overlay spec.

## [1.3.0] - 2026-10-30

MiniTui 1.3: a multi-line text area, popup menus and dropdown selects.

### Added
- `Widget.TextArea()` and `TextAreaState`: multi-line text input. The text field's editing keys work across lines, plus Enter (new line), ↑ ↓ by wrapped row with a remembered column, PgUp/PgDn, Home/End by row then by line, and Ctrl+Home/End. Lines wrap at words by default (`Wrap(WrapMode.Char)` or `WrapMode.None` to scroll sideways), and the view scrolls to keep the cursor visible. `Paste` keeps line breaks, tabs become spaces, and `HandleMouse` places the cursor on a click and scrolls with the wheel. `TextPosition` is the cursor type.
- `PopupMenu` and `MenuItem`: a menu overlay placed below an anchor (or above it, when there's more room), with right-aligned hints, separators and disabled items. ↑ ↓ (wrapping), PgUp/PgDn, Home/End and type-ahead move the selection; Enter or a click chooses; Esc, Tab or a click outside dismisses. `app.Overlays.ShowMenuAsync(items, anchor)` answers the chosen index, or null.
- `Widget.Select()` and `SelectState`: a dropdown field that opens a `PopupMenu` of its options below the field, as wide as it. Enter, Space, ↑ ↓, Alt+↓, F4 or a click opens it; `Changed` fires when the user picks a different option.
- `Overlay.Framed` (false: the overlay draws its own border) and `Overlay.CloseOnClickOutside` (true: a press outside closes it and goes nowhere else). Both default to the old behavior.
- Example: the Form demo has a Notes text area and a Plan select; the Command Registry demo has a context menu on its notes (right-click or Shift+F10).
- `docs/SPEC-1.x.md` §6 and §7: the TextArea, PopupMenu and Select specs.

## [1.2.0] - 2026-10-30

MiniTui 1.2: Windows reads input the way Linux and macOS do (with bracketed paste), opt-in mouse support everywhere, and suspend and resume.

### Added
- Windows reads the console in VT input mode and decodes keys with MiniTui's own decoder, as Linux and macOS do since 1.1. Waiting for input on Windows no longer wakes every 5 ms, and a window resize is noticed at once.
- Bracketed paste on Windows: in Windows Terminal a paste arrives as one `PasteEvent`. Conhost has no bracketed paste, so there Ctrl+V pastes the clipboard's text as a `PasteEvent`.
- Mouse support, opt-in with `App.WithMouse()`: SGR (and X10) mouse reports on Linux and macOS and in Windows Terminal, and console mouse records in conhost. A `MouseEvent` goes to `App.OnMouse`, then to `Component.HandleMouse` on the component under the pointer and its ancestors; a press focuses that component first. `RenderContext.Render(child, buf, rect)` renders a child and records its `Component.Area` for hit testing.
- `SelectionState.HandleMouse` (lists and tables: the wheel moves the selection, a click selects a row), `ScrollState.HandleMouse` (the wheel scrolls text) and `TabBarState.HandleMouse` (a click selects a tab). `ConfirmDialog` answers clicks on its buttons; `MessageDialog` and `HelpOverlay` scroll with the wheel.
- `ScriptedInputSource.EnqueueMouse`, and `ComponentHarness.Click`, `Scroll` and `Mouse`, for tests.
- Example: the mouse is on; clicks focus fields, toggle the checkbox, select rows and tabs and open menu items, and the Input demo shows mouse events.
- `docs/SPEC-1.x.md` §4: the mouse and Windows input spec.
- `App.Suspend(action)` gives the terminal back while `action` runs, for running `$EDITOR` or another program in it: input stops, mouse reporting and bracketed paste turn off, the terminal mode is restored and the alternate screen left, then all of it comes back and the app redraws. Ctrl+C during the action stops the child program, not the app. Works on every platform.
- `App.SuspendProcess()` stops the process and returns to the shell with the terminal restored (Linux and macOS); `fg` resumes the app, which redraws. `App.WithSuspendKey()` makes an unhandled Ctrl+Z do that; it's off by default.
- `ResumeEvent`, sent to `App.OnEvent` whenever the app takes the terminal back.
- Example: Ctrl+Z suspends the demos, and Ctrl+E in the Input demo edits a file in `$VISUAL` or `$EDITOR`.
- `docs/SPEC-1.x.md` §5: the suspend and resume spec.

### Changed
- On Windows, keys decode by the same rules as on Linux and macOS: Esc is recognized after up to 50 ms, and Ctrl+Enter, Ctrl+Tab and Ctrl+digits arrive without the Ctrl. `MINITUI_INPUT=console` restores the 1.0 input path on Windows too.
- On Windows, pasted text no longer arrives as keys in Windows Terminal, and Ctrl+V in conhost pastes instead of being a key. A component that edits text needs `HandlePaste` to accept pastes, as on Linux and macOS since 1.1.
- On Linux and macOS, a `SIGTSTP` sent to a running app from outside (`kill -TSTP`) restores the terminal before the process stops, and the app redraws when it's continued. Before, the process stopped with the terminal still in raw mode on the alternate screen.

## [1.1.0] - 2026-10-30

MiniTui 1.1: raw terminal input and bracketed paste on Linux and macOS. Windows input is unchanged.

### Added
- `TerminalInputSource`, the new default input source. On Linux and macOS it reads the terminal in raw mode and decodes keys with MiniTui's own decoder: Esc, Alt and Alt+Shift letters, Ctrl+letters, Shift/Ctrl/Alt arrows, Home/End/PgUp/PgDn/Insert/Delete, Shift+Tab and F1–F12 decode the same way in every terminal. On Windows it reads keys as before.
- Bracketed paste on Linux and macOS: a paste arrives as one `PasteEvent` instead of a key per character. `Component.HandlePaste` and `App.OnPaste` receive it, `TextFieldState.Paste` inserts it as one line, and `CommandLineOverlay` handles it. `ScriptedInputSource.EnqueuePaste` and `ComponentHarness.Paste` script pastes in tests.
- The `MINITUI_INPUT=console` environment variable selects the 1.0 input path (`ConsoleInputSource`) without recompiling.
- Example: an **Input** demo that shows how each key and paste was decoded, and the Form screen accepts pastes.
- `docs/SPEC-1.x.md`: the post-1.0 roadmap and the raw input and bracketed paste specs.

### Changed
- On Linux and macOS, Esc is recognized after up to 50 ms (`TerminalInputSource.EscapeTimeout`), and Ctrl+Z, Ctrl+\, Ctrl+S, Ctrl+Q, Ctrl+V and Ctrl+O reach the app as keys instead of being handled by the terminal.
- Waiting for input no longer wakes every 5 ms on Linux and macOS.
- On Linux and macOS, pasted text no longer arrives as keys, so a pasted line break doesn't submit and pasted letters don't fire key bindings. A component that edits text needs `HandlePaste` (for example `=> State.Paste(text)`) to accept pastes; a paste nothing handles is dropped. On Windows and with `MINITUI_INPUT=console`, pastes still arrive as keys.

### Fixed
- A fast burst of keys (about 1,000 arrow keys at once) could decode as a stray Esc and leave the current screen. Key sequences split across reads are now reassembled.

## [1.0.0] - 2026-10-29

MiniTui 1.0: an event loop with async support, constraint layout, a themed widget set, and screens and
components for larger apps. It removes the 0.7 APIs that 0.8 and 0.9 marked obsolete (they were never
released separately); [docs/MIGRATION-0.7-to-1.0.md](docs/MIGRATION-0.7-to-1.0.md) maps each one to its
replacement.

### Added
- `docs/MIGRATION-0.7-to-1.0.md`.
- The public API is tracked with `Microsoft.CodeAnalysis.PublicApiAnalyzers` (`PublicAPI.Shipped.txt`); changing it fails the build until the file is updated.
- CI requires at least 85% line coverage of `src/MiniTui`.
- The publish workflow also pushes to nuget.org with trusted publishing (no stored API key) when the repository has a `NUGET_USER` variable.
- Components (§9): `Component` (keys, `HandleKey`, `CapturesText`, children, `Lifetime`, `Load`, mount/activate hooks, `Component.From`), `RenderContext` and `FocusScope`.
- `Screen` and `App.Screens` (`ScreenStack`: `Push`, `Pop`, `Replace`, `PopTo`). With screens, the app draws the top screen and a status bar with key hints and a breadcrumb (`WithChrome`), and quits when the last screen is popped (`QuitWhenEmpty`).
- `Overlay` and `App.Overlays` (`OverlayStack`: `Show`, `Close`, `ConfirmAsync`, `ShowMessageAsync`, `ShowHelp`), with the built-in `ConfirmDialog`, `MessageDialog`, `HelpOverlay` and `CommandLineOverlay`. Overlays are modal and work in render-callback apps too.
- Key routing (§9.4): the `OnKey` hook, then the top overlay, or the focus chain innermost first, then `App.Keys` (with `?` for help and `ctrl+c` built in), then Esc pops the screen. Text-capturing components keep single-character bindings from firing while you type.
- `App.Toasts`, drawn by the app over everything else.
- `ComponentHarness` for testing screens: `Press`, `Type`, `RunUntilIdle`, `Lines`, `CellAt`.
- `Theme` (`Default`, `Monochrome`, `Current`) and `App.WithTheme`. Every widget draws with the theme; backgrounds are left to the terminal except for bars, buttons and selections. Apps use `Monochrome` automatically when the terminal has no colors (`NO_COLOR`).
- Widget builders: `Widget.Block` (`Borders`), `Paragraph` (`WrapMode`, `Paragraph.MeasureLines`), `Table` (`Row`, column constraints, sort markers, only visible rows formatted), `Scrollbar`, `Gauge`, `Sparkline`, `Spinner` (`SpinnerFrames`), `Toasts` (`ToastQueue`, `ToastLevel`), `TextField`, `CommandLine`, `TabBar`, `Button` and `Checkbox`.
- `List` builder: `Items(list, format)` (only visible items are formatted), `State(SelectionState)`, `HighlightSymbol`, `Block`, `Style` and `HighlightStyle`.
- State classes that handle their own keys: `SelectionState`, `ScrollState`, `TextFieldState`, `CommandLineState` and `TabBarState`.
- Text editing (§8.4): any non-control character including surrogate pairs, cursor movement and deletion by grapheme cluster, Ctrl+←/→ by word, Ctrl+Backspace/Ctrl+W, Ctrl+U and Ctrl+K, and horizontal scrolling by display width. The text field shows the terminal cursor.
- `KeyMap` (`Add`, `Handle`, `Hints`, `HelpLines`) and `KeyBinding` (`Parse`, `Matches`).
- Spinners and visible toasts make the app tick while they're drawn, even with `WithTickRate(null)`.
- In conhost (the classic Windows console, whose fonts have no fallback), widgets draw only glyphs its fonts have: gauges and sparklines use half and full blocks, the braille spinner becomes the line spinner, and rounded borders become single. Windows Terminal and other terminals keep the full glyphs.
- Example: **Services** (the §3.1 async program against a fake HTTP API) and **Widgets** (every widget, theme toggle, Unicode panel) demos.
- `Constraint` (`Length`, `Percentage`, `Ratio`, `Min`, `Max`, `Fill(weight)`), `Flex` and `Direction` for layouts.
- `Layout.Add(name, Constraint)`, `Spacing`, `Margin`, `Flex` and `Layout.Split(area, direction, constraints)`.
- Event loop (§6): keys, ticks, resizes, posted actions and redraw requests go through one queue, handled on the thread that called `Run()`. Keys are read on a background thread.
- `App.OnKey`, `OnEvent` (`KeyEvent`, `ResizeEvent`, `TickEvent`), `OnError` (`ErrorAction.Continue`/`Quit`/`Rethrow`) and `OnSetup(Action<App>)`.
- `App.Post`, `RequestRedraw` and `Quit` are thread-safe. `App.RunTask` starts async work whose continuations resume on the UI thread (a `SynchronizationContext` is installed while running); its exceptions go to `OnError`.
- `App.RunAsync`, `Stopping` (cancelled on quit), `Size` and `Log`.
- `App.WithTickRate` (default 250 ms; `null` redraws only on events), `WithTimeProvider`, `WithBackend`, `WithInput`, `WithColorMode` and `CaptureConsoleOutput`.
- `Loadable<T>` and `LoadStatus`: async load state with stale values, last-load-wins, cancellation and `Match`/`Switch`.
- `LogBuffer` and `LogEntry`. While an app runs, `Console.Out` and `Console.Error` go into `app.Log`; error lines are written to stderr on exit.
- `InputRouter.Global`, `InputRouter.Refresh` and `Add(name, handler, capturesText)`.
- `ITerminalBackend` and `CellUpdate`. `Terminal(ITerminalBackend)` draws through any backend.
- `AnsiBackend`, the new default: one buffered UTF-8 write per frame, synchronized output (`?2026`), cursor and SGR tracking so only changes are emitted, colors downsampled to the detected `ColorMode`. An unchanged frame writes zero bytes and allocates nothing.
- `LegacyConsoleBackend`, used automatically on Windows consoles where VT can't be enabled (16 colors, no alternate screen). Line wrap is turned off while it runs, so the bottom-right cell doesn't scroll the screen.
- `TestBackend`, an in-memory screen for testing apps (`Lines`, `CellAt`, `CursorPosition`, `CursorVisible`).
- `IInputSource`, `ConsoleInputSource` and `ScriptedInputSource`.
- `TerminalNotSupportedException`, thrown when stdin or stdout is redirected.
- `TerminalLease`: terminals share a process-wide, ref-counted session.
- `tests/MiniTui.Tests` xUnit project with smoke tests, included in `src/MiniTui.sln`.
- CI workflow (`.github/workflows/ci.yml`): build with `-warnaserror`, test, and pack dry run on Ubuntu, Windows and macOS; code coverage collected on Ubuntu.
- Symbol packages (`.snupkg`), SourceLink repository metadata and embedded untracked sources.
- `Color` (reset, 16 named, 256-indexed and RGB, `FromHex`), with implicit conversion from `ConsoleColor`.
- `ColorMode` and `ColorCapability.Detect()`, which reads `NO_COLOR`, `COLORTERM`, `WT_SESSION`, `TERM_PROGRAM` and `TERM`. Colors are downsampled to the detected mode at encode time.
- `Style` and `Modifiers`. A style is a patch: unset colors keep the cell's existing ones.
- `Span`, `Line`, `Text` and `Alignment` for styled text.
- `UnicodeWidth` (`Of`, `OfGrapheme`, `Graphemes`, `Truncate`), with East Asian Width and emoji tables generated from Unicode 16.0 by `tools/GenWidthTable.cs`.
- `Buffer.SetString`, `SetLine`, `SetStyle`, `SetCursor`/`CursorRequest` and `ToLines`.
- `Rect.IsEmpty`, `Intersect`, `Clamp`, `Inner(Margin)`, `Rows()` and `Columns()`, and the `Margin` type.

### Changed
- Version 1.0.0.
- The app draws a frame only when something happened (input, tick, posted action, resize, redraw request) instead of every 16 ms.
- `OnKey` sees every key first; keys it doesn't handle are routed to overlays, screens and `App.Keys` (§9.4).
- An app started from inside another app receives the keys the outer app hadn't handled yet, and returns the ones it didn't handle when it exits.
- `OnSetup` runs on the UI thread after the terminal is entered, so it can `Post` and start loads.
- `App.RunTask` runs its work under the app's synchronization context even when called from another context, so its awaits resume on the UI thread.
- On Windows, VT processing is enabled and the output encoding is set to UTF-8 while an app runs; both are restored afterwards.
- `Layout` uses a deterministic constraint algorithm (§7.2). The indexer builds the layout if needed, and an unknown region name throws `KeyNotFoundException` naming it. Adding two regions with the same name throws `ArgumentException`.
- `Cell` is now `Cell(string Symbol, Color Fg, Color Bg, Modifiers Modifiers)`: one grapheme cluster per cell, and wide characters are followed by a continuation cell. `Cell.Fg`/`Bg` are `Color` instead of `ConsoleColor`.
- `Cell.Empty` uses the terminal's default colors instead of gray on black (D10).
- `Buffer.DrawHLine` and `DrawVLine` take a `Style` instead of a `ConsoleColor`, and keep the existing background.
- `Buffer.Set` and `FillRect` store a symbol that starts with a zero-width character (a lone combining mark, ZWJ, VS16) as a space, and `SetString` skips such graphemes, so they can't merge with the neighbouring cell on screen.
- Widgets draw with the theme, measure text by display width, end truncated text with `…`, and never throw on small or off-screen areas.
- An unfocused list or table shows its selection with the `SelectionUnfocused` style instead of hiding it (D11).
- `StatusBar` draws on the area's row when the area is one row high, and truncates the left text when both sides don't fit.
- `Rect.SplitHorizontal`/`SplitVertical` clamp the split offset, so neither part has a negative size.
- The Example is one app with a stack of screens, with no nested `App.Run`, and adds the Services and Widgets demos.
- `MiniTui.csproj` builds with `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, missing XML docs (CS1591) as errors, `IsAotCompatible`, and deterministic/CI builds.
- The publish workflow runs tests before packing and takes the package version from the tag (`v1.2.3` → `1.2.3`).
- `Terminal.Dispose()` calls `GC.SuppressFinalize`.
- Source files moved into `App/`, `Core/`, `Text/`, `Layout/`, `Terminal/`, `Components/`, `Input/` and `Widgets/` (no API change).

### Removed
See [docs/MIGRATION-0.7-to-1.0.md](docs/MIGRATION-0.7-to-1.0.md) for the replacement of each.
- `App.RunWhile`, `App.WithFrameDelay`, `App.UseRouter`, and the static `Input` class (`Poll`, `Read`, `IsKey`, `IsChar`, `IsQuit`).
- `InputRouter`, `FocusManager` and `InputHelpers`.
- `ListState`, `TextInputState`, `CommandInputState`, `CommandInputResult`, `TabsState` and `ConfirmState`.
- The static `Widgets` class (now internal), `Widget.Modal`, `Widget.Confirm`, `Widget.CommandInput`, `Widget.Tabs`, `ModalBuilder`, `ConfirmBuilder`, `CommandInputBuilder`, `TabsBuilder` and `ListBuilder.State(ListState)`.
- `Buffer.DrawText`, `DrawTextCentered`, `DrawBox`, `DrawBoxWithTitle` and `DrawProgressBar`.
- `Layout.Add(name, int)` and `Layout.AddFill(name)`.
- `Cell(char, ConsoleColor, ConsoleColor)`, `Cell.Char` and `Cell.From(char)`.
- `Command`, which had no public members, is internal.
- Stale duplicate solution file `src/MiniTui/MiniTui.sln` (D19). `src/MiniTui.sln` is the canonical solution.

### Fixed
- `LICENSE` contained the AGPL-3.0 text while the package and README declared MIT; it is now the MIT license (copyright Discover Edge LLC), and the package lists Discover Edge LLC as its author.
- Ctrl+C is delivered as input (`TreatControlCAsInput`) and quits `App.Run`, instead of killing the process with the terminal left in TUI mode; Ctrl+C sent as `\x03` is recognised too (D1).
- Apps run on the alternate screen and never call `Console.Clear()`, so the user's screen and scrollback are preserved (D2).
- A frame is one write instead of thousands of console calls (D5).
- The terminal is restored on SIGTERM, SIGHUP, SIGQUIT, process exit and unhandled exceptions.
- Nested apps share the terminal session: the inner app doesn't clear the screen, and the outer app redraws fully when it exits, without `app.Invalidate()` (D17).
- `Layout` never gives a region a negative size when fixed sizes exceed the space (they shrink proportionally), and the integer-division remainder is distributed instead of dropped: three fills in 10 rows are 4/3/3 (D7).
- All pending keys are handled before the next frame (up to 256 per frame), so pasted text and key repeat are no longer throttled to one key per frame (D3).
- Keys are no longer dropped: each key goes through one path (`OnKey`, then routing) exactly once (D4).
- A dialog opened from code (for example by an HTTP error) is modal and drawn at the next frame, without a key press (D12).
- An idle app no longer redraws every 16 ms (D16).
- README license section now states MIT, matching `LICENSE` and the package metadata (D18).
- README box-style examples show the actual box-drawing glyphs (D18).
- Drawing never throws for any coordinates or `Rect`, and `Rect.Shrink` never returns a negative size; an over-shrunk rect collapses to its center (D6). No widget throws on narrow areas.
- Wide characters and surrogate pairs are no longer split. Text, truncation and centering use display width, including in the existing widgets (D9).
- Control characters (including ESC) in drawn text are replaced with U+FFFD, so data can't inject escape sequences (D14).
- With `NO_COLOR`, an explicit `WithTheme(Theme.Default)` also becomes `Theme.Monochrome`, so the selection stays visible.
- Text input accepts any non-control character, including accented letters, CJK and emoji (D8).
- Widgets no longer paint a black background over light or custom terminal themes (D10).
- Command history is an immutable list that copies its seed, so copies of a state never share or change each other's history (D13).
- Tables size columns with constraints instead of equal widths, show rows beyond the area by scrolling, and keep the selected row visible (D15).

## [0.7.0]

### Added
- Tabs widget for multi-panel UIs.
