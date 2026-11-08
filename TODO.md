# MiniTui TODO

## Done in 1.0
- [x] Scrollbar indicator for lists - show position in long lists (e.g., `[1-10 of 50]`)
- [x] Color themes - configurable color schemes instead of hardcoded colors (`Theme`, `App.WithTheme`)
- [x] Confirmation dialog (`app.Overlays.ConfirmAsync`)
- [x] Tabs widget (`Widget.TabBar`)
- [x] Table widget - columns with headers, sorting markers, constraint widths, scrolling (`Widget.Table`)
- [x] Toast/notification widget - auto-dismiss visual messages (`ToastQueue`, `Widget.Toasts`)
- [x] Spinner/loading indicator - for async operations (`Widget.Spinner`, `Loadable<T>`)
- [x] Handle terminal resize - detect and respond to window size changes
- [x] Better text wrapping - for modals with long content (`Widget.Paragraph`, `MessageDialog`)

## Post-1.0
Planned in [docs/SPEC-1.x.md](docs/SPEC-1.x.md), one phase each.

- [x] Raw input parser (`TerminalInputSource`): Unix raw mode in 1.1, Windows VT input in Phase 10a
- [x] Mouse support - clicking to focus, scrolling (`App.WithMouse`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §4
- [x] Bracketed paste (`?2004`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §3
- [x] Popup menu - context menus, dropdowns (`PopupMenu`, `Select`, `SelectState`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §7
- [x] Multi-line text input widget (`TextArea`, `TextAreaState`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §6
- [x] Canvas (braille 2×4), line Chart and BarChart (`Canvas`, `Chart`, `BarChart`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §8
- [x] Debug mode - visualize focus, layout boundaries and frame time (`App.WithDebugKey`, `ShowDebug`, `LastFrame`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §9
- [x] Text selection, copy/cut and the system clipboard (`Clipboard`, `Select`, `Undo`, `MouseAction.Drag`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §10
- [x] Ctrl+Z suspend/resume (SIGTSTP/SIGCONT; `App.Suspend`, `App.WithSuspendKey`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §5
- [x] Tree view (`Widget.Tree`, `TreeState<T>`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §11
- [x] Terminal queries, light and dark themes, keyboard protocols (Ctrl+Enter, Shift+Enter) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §12
- [x] Command palette and filterable select (`CommandPalette`, `Fuzzy.Match`, `SelectState.Filterable`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §13
- [x] Split panes, scrollbar dragging and sortable tables (`Widget.Split`, `TableBuilder.Sortable`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §14
- [x] Menu bar and richer menus (`MenuBar`, mnemonics, check and radio items, submenus) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §15
- [x] Text area as an editor (`TabIndents`, `AutoIndent`, `LineNumbers`, a scrollbar, Ctrl+F find) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §16
- [x] Double-click and hover (`ClickCount`, `SelectionState.Activated`, `WithHover`, `ctx.Hover`, `Theme.Hover`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §17
- [x] Tree extras (`Reveal`, `Filter`, `Checkboxes`, `TreeState.Async`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §18
- [x] Chart extras (grouped and stacked bars, `Axis.Time`, `Axis.Duration`, `Ticks`, `Grid`, `GraphType.Area`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §19
- [x] Clipboard and selection extras (`WithClipboardRead`, `Clipboard.GetTextAsync`, the Insert keys, `ScrollState.Selectable`, `Component.HasSelection`) — [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §20

## Backlog
Ideas not specced yet, ranked by value in [docs/SPEC-1.x.md](docs/SPEC-1.x.md) §1.1:

Everything on it has shipped (through 1.15.0).

- [ ] Full CommonMark in `Widget.Markdown`: link reference definitions, HTML blocks, and every case in the CommonMark spec, tested against its examples. 2.4 covers a subset plus GitHub's tables and task lists ([docs/SPEC-1.x.md](docs/SPEC-1.x.md) §27.3).
