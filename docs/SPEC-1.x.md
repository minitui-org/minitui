# MiniTui 1.x — Post-1.0 Spec

| | |
|---|---|
| **Status** | §2 (Phase 8) and §3 (Phase 9) released in v1.1.0; §4 (Phase 10) and §5 (Phase 11) released in v1.2.0; §6 (Phase 12) and §7 (Phase 13) released in v1.3.0; §8 (Phase 14) and §9 (Phase 15) released in v1.4.0; §10 (Phase 16) released in v1.5.0; §11 (Phase 17) released in v1.6.0; §12 (Phase 18) released in v1.7.0; §13 (Phase 19) released in v1.8.0; §14 (Phase 20) released in v1.9.0; §15 (Phase 21) released in v1.10.0; §16 (Phase 22) released in v1.11.0; §17 (Phase 23) released in v1.12.0; §18 (Phase 24) released in v1.13.0; §19 (Phase 25) released in v1.14.0; §20 (Phase 26) released in v1.15.0; §21 (Phase 27) live at minitui.org, with its README and project URL released in v1.15.1; §22 (Phase 28) released in v2.0.0; §23 (Phase 29) and §24 (Phase 30) released in v2.1.0 |
| **Baseline** | `discover-edge/minitui` @ `v1.0.0` |
| **Target** | v1.1.0 and later minor releases |
| **Builds on** | [`docs/SPEC.md`](SPEC.md) (the 1.0 spec). Section references like "§9.4" without a file name point there. |

---

## 0. Instructions for Claude Code

The rules in SPEC.md §0 still apply (one branch and PR per phase, test-first defect fixes, the build gate, no
runtime dependencies, match Ratatui when this spec is silent, stop and ask before deviating from a
**Decision**, CHANGELOG and README in every PR, Windows-only code listed for manual verification). These replace
or add to them:

1. **Additive only.** 1.x follows semantic versioning: no public API is removed or changed incompatibly. New
   public members go in `PublicAPI.Unshipped.txt`; they move to `PublicAPI.Shipped.txt` when the version that
   ships them is tagged. No `[Obsolete]` shims are needed, because nothing is replaced.
2. **Behavior changes** that existing apps could notice (a key that used to do one thing now does another) are
   called out under "Changed" in the CHANGELOG and in the PR description.
3. **Phases are numbered on from SPEC.md §11**, starting at 8. Each roadmap item (§1) gets its own section in
   this file before work on it starts.

---

## 1. Roadmap

| Phase | Scope | Depends on | Spec | Version |
|---|---|---|---|---|
| **8. Raw input** | Read Unix terminal input as bytes and decode it ourselves: reliable Esc, Alt, function keys and modifier combinations, and no stray Esc from split sequences. Groundwork for paste and mouse. | — | §2 | **1.1.0** |
| **9. Bracketed paste** | `?2004`: a paste arrives as one `PasteEvent` instead of thousands of keys; text fields insert it in one step. | 8 | §3 | 1.1.0 |
| **10. Mouse and Windows input** | Windows reads `ReadConsoleInput` records directly. Paste events on Windows: bracketed paste in Windows Terminal (VT input), and a decision for conhost, which has none and delivers Ctrl+V as a key (found in Phase 9 QA). Mouse on both platforms (SGR 1006 on Unix, `MOUSE_EVENT` records on Windows): `MouseEvent`, click to focus, wheel scrolling in lists, tables and paragraphs, clicking buttons, checkboxes and tabs. | 8, 9 | §4 | 1.2.0 |
| **11. Suspend and resume** | Ctrl+Z suspends the app (SIGTSTP), restoring the terminal; resuming (SIGCONT) re-enters it and redraws. Also `app.Suspend(Action)` for running a child process such as `$EDITOR` in the real terminal. | 8 | §5 | 1.2.0 |
| **12. TextArea** | Multi-line text editing widget and `TextAreaState`: wrapping, vertical scrolling, selection-free editing with the §8.4 keys, paste of multi-line text. | 9 | §6 | 1.3.0 |
| **13. PopupMenu and Select** | A popup menu overlay (context menus, Enter/Esc, type-ahead) and a `Select` dropdown field built on it. | 6 (overlays) | §7 | 1.3.0 |
| **14. Canvas and charts** | `Canvas` with braille (2×4 dots per cell) drawing, line `Chart` with axes and labels, and `BarChart`. | — | §8 | 1.4.0 |
| **15. Debug overlay** | A toggleable overlay showing layout rects, the focus chain and frame time / bytes per frame. | — | §9 | 1.4.0 |
| **16. Text selection and clipboard** | Selection in `TextField` and `TextArea` (Shift+arrows, select all, typing replaces it; mouse drag needs motion reports), copy and cut to the system clipboard (OSC 52; `SetClipboardData` in conhost; an in-app fallback), and a decision on undo/redo. Open questions: the copy key (Ctrl+C quits) and whether undo takes Ctrl+Z from `WithSuspendKey`. | 10, 12 | §10 | 1.5.0 |
| **17. Tree view** | `Widget.Tree()` and `TreeState<T>` over the app's own nodes: expand and collapse, lazily read children, tree keys, type-ahead, the mouse and optional guide lines. | 10 | §11 | 1.6.0 |
| **18. Terminal queries and richer keyboard input** | Ask the terminal about itself (DA1 as the sentinel, OSC 10/11 colors, XTVERSION, keyboard protocol support) with replies never leaked as keys; `TerminalInfo`; light and dark themes; the kitty keyboard protocol, modifyOtherKeys and win32-input-mode for Ctrl+Enter, Shift+Enter, Ctrl+Tab and Ctrl+I. Backlog ranks 1 and 6. | 8, 10 | §12 | 1.7.0 |
| **19. Command palette and filterable select** | A `CommandPalette` overlay that fuzzy-filters the key bindings in scope and a `CommandRegistry`'s commands and runs the chosen one; `Fuzzy.Match`; `SelectState.Filterable` for a dropdown you type into. Backlog rank 2. | 13 | §13 | 1.8.0 |
| **20. Split panes, scrollbar dragging and sortable tables** | `SplitState` and `Widget.Split()`, by ratio or with a fixed-size pane, with a divider dragged by the mouse or moved with Alt+Shift+arrows; dragging the scrollbar thumbs of lists, tables, trees and paragraphs, and paging on a track press; clicking a table header to sort (`TableBuilder.Sortable`, `SelectionState.ToggleSort` and `SortChanged`). Backlog rank 3. | 10, 16 | §14 | 1.9.0 |
| **21. Menu bar and richer menus** | Mnemonics (`&File`), check and radio items, submenus and `Run` actions in `PopupMenu`; a `MenuBar` along the top opened with F10, Alt+mnemonic or a click, with ← → between menus; the palette lists its items. Backlog rank 4. | 13, 19 | §15 | 1.10.0 |
| **22. Text area as an editor** | Opt-in Tab-to-indent (Tab, Shift+Tab, Backspace by indent stops), auto-indent on Enter and `IndentSize`; a line number gutter; a draggable scrollbar on the block; a find bar (Ctrl+F, F3, Shift+F3) with smart case and highlighted matches (`Theme.SearchMatch`). Backlog rank 5. | 12, 16, 20 | §16 | 1.11.0 |
| **23. Double-click and hover** | `MouseEvent.ClickCount` counted by the app (`WithDoubleClickTime`): double-click selects a word and triple-click a line in text states, and activates list, table and tree rows (`SelectionState.Activated`). Opt-in hover (`WithHover`, `?1003`, `MouseAction.Move`): `ctx.Hover` and `.Hover()` on rows, buttons, checkboxes and tabs (`Theme.Hover`), and menus that follow the pointer. Backlog rank 7. | 10, 16 | §17 | 1.12.0 |
| **24. Tree extras** | `TreeState.Reveal(path)`; `Filter` showing matches and their ancestors, opened; tri-state `Checkboxes` (Space, a click, `CheckedNodes`, `CheckedChanged`); async children with a loader, `Loading…` and errors on the row, and `Reload`. Backlog rank 8. | 17 | §18 | 1.13.0 |
| **25. Chart extras** | Grouped and stacked bar charts (`BarGroup`, `Groups`, `Stacked`, `GroupGap`) with a series legend; time and duration axes (`Axis.Time`, `Axis.Duration`, `ChartTime`, `Formatter`, `LabelCount`); tick marks and grid lines (`Ticks`, `Grid`); area charts (`GraphType.Area`). Backlog rank 9. | 14 | §19 | 1.14.0 |
| **26. Clipboard and selection extras** | Reading the Unix clipboard with an OSC 52 query (`WithClipboardRead`, `Clipboard.GetTextAsync`); Ctrl+Insert, Shift+Insert and Shift+Delete; selecting text in paragraphs with the mouse (`ScrollState.Selectable`, `Component.HasSelection`), including the message and help dialogs. Backlog rank 10. | 12, 16, 23 | §20 | 1.15.0 |
| **27. Website** | minitui.org on GitHub Pages, built with DocFX: a landing page with generated recordings and screenshots of the example app, the README's content as guide pages, an API reference from the XML docs, and the changelog. | — | §21 | — (no package) |
| **28. Fixes from building apps from the docs** | `Buffer` renamed to `CellBuffer`; tree type-ahead and expander clicks, gauge label colors, text states leaving Alt+arrows, palette duplicates, toasts and the menu bar, status bar hints first, an `esc Back` hint, padded overlay titles, the harness and custom dialogs; `TextFieldState.Changed`, `ConfirmAsync(yesFirst)`. | 27 | §22 | **2.0.0** |
| **29. Bar values, paragraph scrolling, checkbox and button states** | A short bar's value drawn above it instead of splitting it; `ScrollState.ScrollsHorizontally` for unwrapped paragraphs; `CheckboxState` and `ButtonState`. | 28 | §23 | 2.1.0 |
| **30. Padding, selection by key, and wide bar values** | Blocks pad their content a column in from side borders by default; `Rows(items, format, key)` and `Items(items, format, key)` keep a list or table's selection on the same item when the items change (`SelectionState.SelectedKey`, `SelectKey`); bar values wider than their bar go above it, and values never touch or get cut. | 29 | §24 | 2.1.0 |
| **31. Gaps found building MiniTui Desk apps** | `TextAreaState.Changed`; `App.OnQuitting` and `RequestQuit`, for Ctrl+C, SIGHUP and SIGTERM; `OverlayStack.PromptAsync`; a public `FilterListState` and `Widget.FilterList`; `TextAreaBuilder.Highlight`; toasts that wrap. | 30 | §25 | 2.2.0 |

Phases 8–11 are input work, so they go first: each depends on Phase 8, and Phase 8 fixes a known bug (a fast
burst of arrow keys can produce a stray Esc, found in 1.0 QA). Phases 12–15 are independent of each other and
can be reordered. Phase 16 comes after the rest. Phase 17 was added after Phases 8–16 shipped.

### 1.1 Backlog

These ideas aren't specced yet. Most were left out of scope by an earlier phase. They're ordered by value: how
much each one helps typical apps, weighed against its cost. Each gets a phase number and a section when it's
picked, as in §0 rule 3.

| Rank | Idea | Why it matters | Deferred by | Depends on |
|---|---|---|---|---|
| 1 | **Richer keyboard input** (Phase 18, §12). The kitty keyboard protocol (`CSI > 1 u`) and xterm `modifyOtherKeys` on Unix, and `win32-input-mode` (`?9001`) in Windows Terminal, all opt-in and detected. They report Ctrl+Enter, Shift+Enter, Ctrl+Tab and Ctrl+Backspace as themselves, and tell Ctrl+I from Tab and Ctrl+M from Enter. | Today a text area has no natural submit key, and Ctrl+Enter, the usual answer, can't be told from Enter. Every input phase deferred this, and every app with a multi-line field runs into it. | §2.12, §4.14 | 8, 10 |
| 2 | **Command palette and filterable select** (Phase 19, §13). A `CommandPalette` overlay (Ctrl+P or Ctrl+Shift+P) that fuzzy-filters `CommandRegistry` entries and key map actions as you type, with their key hints. The same filtering list makes a combo box `Select` you can type into. | The discoverability of keys and commands is the main usability gap in larger apps, and VS Code, Textual and lazygit all have a palette. It reuses `PopupMenu`, `TextFieldState` and `CommandRegistry`. | §7.12 | 7, 13 |
| 3 | **Split panes and scrollbar dragging** (Phase 20, §14). A `Split` layout with draggable dividers (mouse and keyboard), dragging a scrollbar's thumb in lists, tables, paragraphs and text areas, and clicking a table header to sort (an event; the app sorts). | `MouseAction.Drag` (1.5) makes these cheap now. Resizable panes are standard in file managers, log viewers and dashboards. | §4.14, §10.12 | 10, 16 |
| 4 | **Menu bar and richer menus** (Phase 21, §15). A `MenuBar` along the top (F10, Alt+letter, mouse), submenus in `PopupMenu`, check and radio items, and mnemonics (underlined letters). | Apps in the style of classic desktop TUIs (Turbo Vision, Midnight Commander) expect a menu bar. Less central than a palette for keyboard-first apps. | §7.12 | 13 |
| 5 | **Text area as an editor** (Phase 22, §16). An opt-in Tab-to-indent mode, auto-indent on Enter, line numbers, find (Ctrl+F) with highlighted matches, and a built-in scrollbar. | It turns `TextArea` from a notes field into a usable config or script editor. Syntax highlighting stays out: it needs a tokenizer. | §6.12 | 12, 16 |
| 6 | **Terminal queries** (Phase 18, §12). Asynchronous queries with replies parsed by the decoder: the background color (OSC 11) to choose a light or dark theme automatically, the cursor position (DSR 6), and the kitty protocol's capability reply (useful for rank 1). | Themes picked for dark terminals are unreadable on light ones, and nothing detects that today. The reply parser also makes rank 1 detectable. | §2.12 | 8 |
| 7 | **Hover and double-click** (Phase 23, §17). Opt-in all-motion reports (`?1003`) with hover styles for buttons, menu items and rows, and double-click detection by timing presses (`MouseEvent.ClickCount`). Double-click selects a word in text fields and activates a tree leaf. | It makes mouse use feel native. It's opt-in because all-motion reports cost a frame per pointer move. | §4.14, §10.12, §11.6 | 10, 16 |
| 8 | **Tree extras** (Phase 24, §18). Revealing a node by path, filtering that expands to matches, multi-select and checkboxes (tri-state parents), and async children with a built-in loading row. | Useful for file pickers and settings trees, but apps can do most of it on the 1.6 API. | §11.10 | 17 |
| 9 | **Chart extras** (Phase 25, §19). Grouped and stacked bar charts, time axes (`DateTime` and `TimeSpan`), tick marks and grid lines, and area charts. | Dashboards want them, but the 1.4 charts cover the common cases. | §8.11 | 14 |
| 10 | **Clipboard and selection extras** (Phase 26, §20). Reading the Unix clipboard through OSC 52 queries (needs rank 6), Ctrl+Insert, Shift+Insert and Shift+Delete, and selecting text in paragraphs and the log view. | Edge cases. Terminals' own Shift+drag already selects screen text. | §10.12 | 16 |

Ranks 1 and 6 share the reply parser, so they could be one phase, with rank 6 first. Ranks 3 and 7 both build on
mouse motion.

---

## 2. Raw input (Phase 8)

### 2.1 Purpose

On Unix, MiniTui 1.0 reads keys through `Console.ReadKey`, which decodes the terminal's escape sequences inside
.NET. That decoder loses information and can't be extended:
- **Split sequences.** When a burst of input crosses a read boundary (about 3 KB of arrow keys at once in tmux),
  the tail of one sequence and the head of the next decode as a stray Esc plus printable characters. Esc pops
  the screen in a MiniTui app, so this is a real bug.
- **Alt and modifiers.** Alt+letter, Alt+arrow and Ctrl+Shift combinations come through inconsistently across
  terminals.
- **No room to grow.** Bracketed paste (Phase 9) and mouse reports (Phase 10) are escape sequences too, and
  `Console.ReadKey` can't deliver either.

Phase 8 puts the terminal in raw mode, reads stdin bytes on the input thread and decodes them with a MiniTui
decoder that keeps state across reads. Keys still reach apps as `ConsoleKeyInfo`, so no app code changes. Windows
keeps reading through `Console.ReadKey`, whose key handling is already reliable there; it switches to
`ReadConsoleInput` in Phase 10, where mouse input needs it.

**Decision:** Keys stay `ConsoleKeyInfo` everywhere in the public API (`OnKey`, `HandleKey`, `KeyMap`,
`KeyBinding`). Changing the key type would break every 1.0 app. The decoder maps everything it recognizes onto
`ConsoleKeyInfo`; keys `ConsoleKey` can't name (F13 and above, media keys) are dropped.

### 2.2 Public API

The only new public type is the input source. It becomes the default, so apps get it without changes.

```csharp
/// The default input source from 1.1: raw terminal input decoded by MiniTui on Linux and macOS,
/// Console.ReadKey on Windows (until Phase 10) and on platforms raw mode doesn't support.
public sealed class TerminalInputSource : IInputSource
{
    public static TimeSpan DefaultEscapeTimeout { get; }    // 50 ms
    public TerminalInputSource();
    public TerminalInputSource(TimeSpan escapeTimeout);     // ArgumentOutOfRangeException outside 0–1 s
    public TimeSpan EscapeTimeout { get; }
    public bool TryRead(out ConsoleKeyInfo key);            // never blocks
    public void Dispose();
}
```

- `App` uses a shared `TerminalInputSource` when `WithInput` isn't called. `ConsoleInputSource` stays public and
  unchanged; `app.WithInput(new ConsoleInputSource())` restores the 1.0 behavior.
- `app.WithInput(new TerminalInputSource(TimeSpan.FromMilliseconds(100)))` changes the escape timeout (§2.4).

### 2.3 Internal model

```csharp
// Decodes terminal input bytes. Pure: no I/O, no clock; the caller passes the time.
internal sealed class AnsiInputDecoder(TimeSpan escapeTimeout)
{
    public void Feed(ReadOnlySpan<byte> bytes, TimeSpan now, IInputSink sink);
    public TimeSpan? Deadline { get; }                  // when held bytes must be resolved; null if none are held
    public void Expire(TimeSpan now, IInputSink sink);  // resolve held bytes if now >= Deadline
}

// What the decoder produces. Phase 9 adds Paste(string), Phase 10 adds Mouse(...).
internal interface IInputSink
{
    void Key(ConsoleKeyInfo key);
}

// Optional extras the input pump uses when a source implements them.
internal interface IInputLifecycle
{
    void Activate();    // first subscriber: enter raw mode
    void Deactivate();  // last subscriber gone: restore the terminal mode
}
internal interface IWaitableInputSource
{
    bool WaitForInput(TimeSpan timeout); // block until input may be available, the timeout passes, or Deactivate
}

// libc P/Invokes ([LibraryImport], AOT-safe): isatty, tcgetattr, tcsetattr, cfmakeraw, poll, read.
[SupportedOSPlatform("linux"), SupportedOSPlatform("macos")]
internal static partial class UnixTerminal
{
    public static bool TryEnterRaw(int fd);   // false if fd isn't a terminal or a call fails
    public static void Restore();             // idempotent; never throws; safe from signal handlers
    public static bool IsRaw { get; }
}
```

### 2.4 Decoding rules

The decoder maps input onto `ConsoleKeyInfo(KeyChar, Key, shift, alt, control)`. These rules are the
contract the tests check.

**Plain bytes**

| Bytes | Key | KeyChar | Modifiers |
|---|---|---|---|
| Printable ASCII (0x20–0x7E) | Letters `A`–`Z`, digits `D0`–`D9`, space `Spacebar`, others `0` | the character | Shift for `A`–`Z` |
| UTF-8 sequence (2–4 bytes) | `0` | the character; non-BMP characters become two keys, high then low surrogate | none |
| Invalid UTF-8 | `0` | U+FFFD | none |
| 0x0D | `Enter` | `\r` | none |
| 0x09 | `Tab` | `\t` | none |
| 0x7F | `Backspace` | `\b` | none |
| 0x08 | `Backspace` | `\b` | Control (terminals send ^H for Ctrl+Backspace; §8.4 already handles it) |
| 0x00 | `Spacebar` | `\0` | Control |
| 0x01–0x1A, except the above | `A`–`Z` | the control character | Control |
| 0x1C–0x1F | `0` | the control character | Control |
| 0x1B | see Esc and Alt below | | |

**Esc and Alt**
- ESC followed by a byte that starts a key (not `[` or `O`) is that key with Alt added: `ESC a` is Alt+A,
  `ESC A` is Alt+Shift+A, `ESC 0x01` is Ctrl+Alt+A, `ESC 0x7F` is Alt+Backspace.
- `ESC [` and `ESC O` start a sequence (below). `ESC ESC` followed by a sequence is that sequence with Alt
  added (iTerm2 and some xterm settings send Alt+arrow this way).
- **Escape timeout.** Bytes that could still be the start of a sequence are held. If no byte arrives for
  `EscapeTimeout` (default 50 ms) after the last held byte, they resolve as typed: a lone ESC becomes `Escape`
  (`KeyChar` U+001B), ESC followed by any one byte becomes that byte with Alt, ESC ESC becomes two `Escape`s,
  `ESC [` becomes Alt+`[`, and an incomplete sequence becomes `Escape` followed by its bytes decoded as plain
  input. An incomplete UTF-8 sequence becomes U+FFFD.
- Bytes are never resolved early just because a read ended. A sequence split across reads is reassembled.
  **This fixes the stray Esc from arrow-key bursts.**

**Sequences.** CSI is `ESC [`, then parameter bytes (0x30–0x3F), intermediate bytes (0x20–0x2F) and one final
byte (0x40–0x7E). SS3 is `ESC O` and one byte.

| Sequence | Key |
|---|---|
| `CSI A` `B` `C` `D`, `SS3 A` `B` `C` `D` | `UpArrow`, `DownArrow`, `RightArrow`, `LeftArrow` |
| `CSI H`, `CSI F`, `SS3 H`, `SS3 F` | `Home`, `End` |
| `SS3 P` `Q` `R` `S`, `CSI P` `Q` `R` `S` (with modifiers) | `F1`–`F4` |
| `CSI 1~` `7~` / `4~` `8~` | `Home` / `End` |
| `CSI 2~` `3~` `5~` `6~` | `Insert`, `Delete`, `PageUp`, `PageDown` |
| `CSI 11~`–`15~`, `17~`–`21~`, `23~`, `24~` | `F1`–`F5`, `F6`–`F10`, `F11`, `F12` |
| `CSI [ A`–`E` (Linux console) | `F1`–`F5` |
| `CSI Z` | `Tab` with Shift |
| `CSI 1;m X`, `CSI n;m ~` | the key above with modifiers from `m` |

- **Modifier parameter `m`:** take `m − 1` as bits: 1 Shift, 2 Alt, 4 Control, 8 Meta (treated as Alt).
  `CSI 1;5C` is Ctrl+Right.
- `KeyChar` for named keys is `\0`, except `Tab` (`\t`).
- `CSI 1;mR` is F3 with modifiers. MiniTui never requests a cursor position report, so this form is never
  a report.
- **Unknown or malformed sequences are dropped whole**, never leaked as characters: any CSI whose final byte
  isn't in the table, SS3 with another byte, OSC (`ESC ]` … BEL or `ESC \`), DCS (`ESC P` … `ESC \`), SGR mouse
  reports (`CSI <` …, enabled in Phase 10) and paste markers (`CSI 200~`/`201~`, Phase 9). A CSI longer than 64
  bytes without a final byte is dropped, and decoding resumes at the next byte.

### 2.5 Terminal mode

- **Raw mode** applies on Linux (x64, arm64, arm) and macOS (x64, arm64) when stdin is a terminal. Elsewhere,
  or if any call fails, `TerminalInputSource` behaves exactly like `ConsoleInputSource`.
- **Entering:** `tcgetattr(0)` into a saved buffer, copy it, `cfmakeraw` on the copy, then `tcsetattr(0,
  TCSANOW)`. The `termios` struct is handled as an opaque, zero-initialized 256-byte buffer (larger than the
  Linux and macOS layouts), so MiniTui never depends on field offsets or flag values.
- **Decision:** raw mode clears `ISIG`, so Ctrl+C (0x03), Ctrl+Z (0x1A) and Ctrl+\ (0x1C) arrive as keys.
  Ctrl+C keeps quitting through the §9.4 routing. Ctrl+Z does nothing until Phase 11 adds suspend; until then,
  suspending would leave the shell on MiniTui's alternate screen. Clearing `IXON` also makes Ctrl+S and Ctrl+Q
  ordinary keys instead of freezing output.
- **Restoring:** `tcsetattr` with the saved buffer. `Deactivate` restores, and so do the existing restore
  paths in `TerminalSession` (SIGTERM, SIGHUP, SIGQUIT, process exit, unhandled exception), which call
  `UnixTerminal.Restore()` before `Leave()`. Restore is idempotent.
- **.NET resetting the mode.** .NET's console code reapplies its own terminal settings in some cases (after a
  child process started with `Process.Start` exits, and on SIGCONT). While raw mode is active, the reader
  checks the mode with `tcgetattr` at most every 250 ms while idle and reapplies it if it changed.
- MiniTui doesn't call `Console.ReadKey`, `Console.KeyAvailable` or `Console.In` while raw mode is active.

### 2.6 Reading

- `InputPump` calls `Activate` when its reader thread starts and `Deactivate` when the last subscriber leaves,
  so nested apps share one raw-mode session.
- When the source implements `IWaitableInputSource`, the pump calls `WaitForInput` instead of sleeping 5 ms.
  `TerminalInputSource` implements it with `poll(0, POLLIN, timeout)`, where the timeout is the decoder's
  deadline or 100 ms, whichever is sooner. This also lowers idle wakeups from 200/s to 10/s.
- `TryRead` first returns any decoded keys, then reads what's available (`read(0, buf, 4096)`, only after
  `poll` reported input, so it never blocks), feeds the decoder and calls `Expire` with the current time.
  Time comes from `Stopwatch.GetElapsedTime`, not the App's `TimeProvider`, because the escape timeout is about
  real terminal latency.
- `EINTR` retries. `read` returning 0 (the terminal closed) or an error stops reading: `TryRead` returns false
  from then on, and the existing SIGHUP handling restores the terminal.
- The decoder allocates nothing per key: keys go to the sink as structs, and the read buffer is reused.

### 2.7 Configuration

| Setting | Effect |
|---|---|
| `new TerminalInputSource(TimeSpan)` | Escape timeout, 0–1 s. 0 resolves held bytes at the end of each read (crossterm's behavior). |
| `app.WithInput(new ConsoleInputSource())` | The 1.0 input path. |
| `MINITUI_INPUT=console` | The default input source becomes `ConsoleInputSource`, without recompiling. An escape hatch for terminals the decoder mishandles. |

### 2.8 Example

Add an **Input** demo screen to `MiniTui.Example`. It lists the last 20 keys as decoded (`Key`, `KeyChar` as a
code point, and modifiers), with the Esc-pops-screen binding replaced by `q`, so every key can be inspected.
It's how the QA checks below are run, and how users report a terminal the decoder gets wrong.

### 2.9 Tests and acceptance

Automated (all platforms unless noted):
- **Decoder table tests** for every row in §2.4, with and without each modifier.
- **Round-trip property test:** 10,000 seeded cases. Generate a random key sequence (printable, UTF-8, emoji,
  control keys, named keys with random modifiers, Alt combinations), encode it with a test-only encoder that
  produces xterm's bytes, split the bytes at random points into chunks fed with time advancing less than the
  escape timeout, and check that the decoded keys equal the input.
- **Burst test:** 3,000 bytes of arrow-key sequences, split at every possible offset into two reads: exactly
  1,000 arrows and no `Escape`.
- **Timeout tests** with explicit times: lone ESC resolves at exactly the deadline, `ESC [` resolves to Alt+`[`,
  an incomplete CSI resolves to Escape plus characters, and a byte arriving before the deadline continues the
  sequence.
- **Unknown sequences** (CSI with unknown finals, OSC, DCS, SGR mouse, paste markers, an overlong CSI) produce no
  keys, and the input after them decodes normally.
- **Allocation test:** decoding 1 MB of mixed input allocates nothing per key
  (`GC.GetAllocatedBytesForCurrentThread` delta below a small fixed bound).
- **Pseudo-terminal test** (Linux and macOS CI): open a pty with `posix_openpt` in the test project, enter raw
  mode on the follower, check that bytes written to the leader arrive unchanged (no echo, no CR translation, 0x03
  and 0x1A delivered), restore, and check that `tcgetattr` matches the saved state byte for byte.
- `TerminalInputSource` on a non-terminal stdin (CI) behaves like `ConsoleInputSource` and doesn't throw.
- Existing tests keep passing unchanged; `ScriptedInputSource` and `ComponentHarness` aren't affected.

Manual QA (results in the PR), in WSL in Windows Terminal, tmux on Linux and SSH to Linux, using the Input demo.
There's no Mac to test on, so on macOS the pseudo-terminal tests in CI are the check: they cover entering raw
mode, decoding, reapplying and restoring there. The decoder is the same code on every platform, and Terminal.app
and iTerm2 send xterm sequences it already handles. Until someone checks a Mac by hand, the README says so.
1. A 3 KB burst of arrow keys (e.g. `tmux send-keys` or a held key) produces only arrows and never leaves the
   screen.
2. Alt+letter, Alt+Shift+letter, Alt+arrows, Ctrl+arrows, Shift+arrows, Ctrl+Shift+arrows, Shift+Tab, F1–F12,
   Insert, Delete, Home, End, PgUp and PgDn decode as named.
3. Esc responds without a noticeable delay.
4. Ctrl+C quits, and Ctrl+Z, Ctrl+S and Ctrl+Q do nothing harmful.
5. After a normal exit, Ctrl+C, the forced exception, and `kill -TERM` / `kill -HUP` from another shell, the
   shell echoes typing and `stty -a` matches its output from before the run.
6. A screen that runs `Process.Start("true")` and waits for it still decodes arrows afterwards (§2.5).
7. Pasting 1,000 characters into a text field loses none, and checks 1–10 from SPEC.md §10.5 still pass.
8. Windows Terminal and conhost: the Example behaves as in 1.0 (no Windows changes in this phase).

### 2.10 Migration impact

- No public API changes or removals; one new type in `PublicAPI.Unshipped.txt`.
- **Behavior changes on Linux and macOS** (CHANGELOG "Changed"): Esc resolves after up to 50 ms; Ctrl+Z,
  Ctrl+\, Ctrl+S, Ctrl+Q, Ctrl+V and Ctrl+O reach the app as keys instead of being handled by the terminal; keys
  that previously decoded inconsistently now decode per §2.4.
- README: the keys/terminal section describes the escape timeout, `MINITUI_INPUT=console` and Ctrl+Z.

### 2.11 Implementation order

1. `AnsiInputDecoder` and `IInputSink`, with the table, round-trip, burst, timeout, unknown-sequence and
   allocation tests. Pure code, so it's built and tested first.
2. `UnixTerminal` P/Invokes and the pty test. Verify that `[LibraryImport("libc")]` resolves on glibc and macOS
   CI; if it doesn't, add a `NativeLibrary` resolver mapping `libc` to `libc.so.6` / `libSystem.dylib`.
3. `TerminalInputSource`, `IInputLifecycle` and `IWaitableInputSource`, and the `InputPump` changes.
4. The restore hook in `TerminalSession`, the mode re-check, and the default-source switch with
   `MINITUI_INPUT`.
5. Input demo, README, CHANGELOG, manual QA.

### 2.12 Out of scope

- Windows `ReadConsoleInput` (Phase 10).
- Enabling bracketed paste or mouse reporting; the decoder only drops their sequences (Phases 9 and 10).
- The kitty keyboard protocol and xterm `modifyOtherKeys`, which would distinguish Ctrl+I from Tab or report
  key releases. They could be added later behind an opt-in, since `ConsoleKeyInfo` can't carry most of what
  they report.
- rxvt-specific modifier forms (`CSI a`, `CSI 2$`), which are dropped as unknown.
- Suspend on Ctrl+Z (Phase 11).
- Terminal queries (cursor position, colors, capabilities).

### 2.13 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Move Windows to `ReadConsoleInput` in this phase, as SPEC.md §11 originally listed, or with mouse in Phase 10? Windows keys already work through `Console.ReadKey`, and I can't test Windows from WSL. | **Phase 10**, together with Windows mouse input. |
| 2 | Escape timeout default? Lower makes Esc snappier; higher protects slow links from split sequences. | **50 ms.** |
| 3 | Until Phase 11, Ctrl+Z is a key that does nothing. Alternative: keep `ISIG` and let Ctrl+Z suspend without restoring the terminal. | **Clear `ISIG`** (no broken shell). |
| 4 | Ship `MINITUI_INPUT=console` as an escape hatch? | **Yes.** |
| 5 | Release cadence: one minor version per group of phases, as in §1, or one per phase? | **Per group** (§1). |

---

## 3. Bracketed paste (Phase 9)

### 3.1 Purpose

In 1.0 and Phase 8, pasted text arrives as one key per character. That's slow for a large paste (each key is
routed and can redraw), and it's wrong: a pasted line break is Enter, which submits a form or runs a command
halfway through the paste, and a pasted `q` or `?` can fire a global binding when no text field has focus.

Terminals solve this with bracketed paste (`CSI ?2004h`): while it's on, the terminal wraps a paste in
`CSI 200~` … `CSI 201~`. Phase 9 turns it on wherever Phase 8's raw input is active, decodes the markers, and
delivers the paste as one event. Text fields insert it in one step.

**Decision:** a paste nobody handles is dropped, never replayed as keys (§3.4). Pasted text is never run as
commands, as in Textual, Bubble Tea and the shells. A component that edits text with `TextFieldState.HandleKey`
needs one line to accept pastes: `public override bool HandlePaste(string text) => State.Paste(text);`.

### 3.2 Public API

```csharp
/// A paste from the terminal. Line breaks are normalized to "\n".
public sealed record PasteEvent(string Text) : Event;

public abstract class Component
{
    /// Handle pasted text. Return true if handled; otherwise the paste goes on up the focus chain (§3.4).
    public virtual bool HandlePaste(string text) => false;
}

public sealed class App
{
    /// Called for each paste before the focus chain sees it; return true to consume it. Like OnKey.
    public App OnPaste(Func<string, bool> handler);
}

public class TextFieldState
{
    /// Insert pasted text at the cursor: leading and trailing line breaks are dropped, each other line break
    /// and each tab becomes a space, and other control characters are removed. Always returns true, so a
    /// component can write `HandlePaste(t) => State.Paste(t)`.
    public bool Paste(string text);
}

public sealed class ScriptedInputSource
{
    /// Queue a paste, in order with the queued keys.
    public ScriptedInputSource EnqueuePaste(string text);
}

public sealed class ComponentHarness
{
    /// Paste text, then RunUntilIdle.
    public void Paste(string text);
}
```

- `CommandLineOverlay` handles pastes with `CommandLineState.Paste` (inherited from `TextFieldState`).
- `IInputSource` doesn't change. A custom source can't produce pastes; only `TerminalInputSource` (on raw-mode
  terminals) and `ScriptedInputSource` do. A public way for custom sources to produce events can come with
  mouse events in Phase 10, if needed.

### 3.3 Internal model

```csharp
// What the input pump carries: a key or a paste, in arrival order. Phase 10 adds mouse.
internal readonly struct InputItem
{
    public static InputItem FromKey(ConsoleKeyInfo key);
    public static InputItem FromPaste(string text);
    public ConsoleKeyInfo Key { get; }
    public string? Paste { get; }        // null for a key
}

internal interface IInputSink
{
    void Key(ConsoleKeyInfo key);
    void Paste(string text);             // new
}

// Sources that can produce more than keys. TerminalInputSource and ScriptedInputSource implement it.
internal interface IInputItemSource
{
    bool TryReadItem(out InputItem item);
}
```

- `InputPump` reads `InputItem`s (from `IInputItemSource.TryReadItem` when the source implements it, otherwise
  wrapping `IInputSource.TryRead` keys), and its batches, orphans and the nested-app handoff in `AppLoop`
  (`RemoveQueuedKeys`/`ReturnKeys`) carry `InputItem[]` instead of `ConsoleKeyInfo[]`. A paste handed back to
  an outer app stays a paste.
- `IInputSource.TryRead(out ConsoleKeyInfo)` on `TerminalInputSource` and `ScriptedInputSource` still works for
  callers outside the pump: it returns the next key and skips any pastes ahead of it.

### 3.4 Routing

A paste goes through these steps, stopping at the first that handles it:

1. `App.OnEvent` sees `PasteEvent` (it observes; it can't consume, as for keys).
2. `App.OnPaste`, if set.
3. The focus chain, innermost first: `Component.HandlePaste`. With an overlay open, only the overlay's chain,
   as for keys. Key maps (`Component.Keys`, `App.Keys`) aren't consulted: a paste is never a binding.
4. Nobody handled it: the paste is dropped. With no text field focused, a paste does nothing.

### 3.5 Decoding

- On `CSI 200~` the decoder switches to paste mode and collects bytes until `CSI 201~`, decoding nothing in
  between: Esc, control bytes and other sequences inside a paste are text. The end marker may be split across
  reads at any byte, like any sequence.
- At the end marker the bytes are decoded as UTF-8 (invalid bytes become U+FFFD), `\r\n` and lone `\r` become
  `\n`, and the result goes to `IInputSink.Paste`. An empty paste (`CSI 200~ CSI 201~`) is dropped.
- The escape timeout doesn't apply in paste mode: a paste over a slow link can pause mid-way.
- **No size limit:** one paste is always one `PasteEvent`, however large, as in crossterm, Bubble Tea and
  Textual. A terminal that never sends `201~` leaves the decoder in paste mode until the app exits; terminals
  generate both markers themselves, so this isn't guarded against.
- A stray `CSI 201~` outside a paste is dropped, as in Phase 8. A `CSI 200~` inside a paste is text.
- The paste buffer is reused between pastes and grows as needed; the only per-paste allocation is the
  delivered string (and the buffer's growth for a paste larger than any before it).

### 3.6 Terminal mode

- `UnixTerminal.TryEnterRaw` writes `CSI ?2004h` to stdout after entering raw mode, and `Restore` writes
  `CSI ?2004l` before restoring the mode, both only when stdout is a terminal (`isatty(1)`). So bracketed
  paste is on exactly while raw input is, and every Phase 8 restore path (Deactivate, signals, exit, unhandled
  exception) turns it off.
- The write is a direct `write(1, …)`, retried on `EINTR`. `TryEnterRaw` runs on the app thread before the
  first frame, so it can't interleave with frame output.
- **Decision:** bracketed paste is never enabled without raw input (Windows until Phase 10,
  `MINITUI_INPUT=console`, a custom source): `Console.ReadKey` would deliver the markers as a stray Esc and
  `[200~`. Pastes there stay keys, as in 1.0.
- A child process started while the app runs inherits bracketed paste mode. `true` and most programs don't
  care; Phase 11's `app.Suspend` restores it fully for programs like `$EDITOR`.

### 3.7 Configuration

| Setting | Effect |
|---|---|
| `MINITUI_INPUT=console` | Also turns bracketed paste off (no raw input). |

No new settings.

### 3.8 Example

- The Form screen's text fields (`TextInput` in `Controls.cs`) handle pastes with `State.Paste`.
- The Input demo lists pastes as `Paste  N chars, M lines: <first 40 chars>`, so a terminal's paste support can
  be checked.

### 3.9 Tests and acceptance

Automated (all platforms unless noted):
- **Decoder:** a paste with plain text, UTF-8, Esc, control bytes, `CSI A` and `CSI 200~` inside it decodes as
  one `Paste` with that text; `\r\n` and `\r` become `\n`; the start and end markers split at every byte offset;
  keys before and after a paste stay in order; an empty paste produces nothing; a stray `201~` is dropped; a
  10 MiB paste arrives as one paste with the same text; a paste held across a pause longer than the escape
  timeout isn't resolved early; invalid UTF-8 becomes U+FFFD.
- **Routing** (`ComponentHarness`): a focused component's `HandlePaste` gets the text; `OnPaste` returning true
  stops it; an overlay's chain gets it while open; `PasteEvent` reaches `OnEvent`; a paste is never matched
  against key maps; with no handler, a paste of `"q?\n"` fires nothing and changes nothing.
- **TextFieldState.Paste:** inserts at the cursor; `"\nab\r\ncd\tef\n"` inserts `"ab cd ef"`; other controls
  are removed; the cursor ends after the text on a cluster boundary.
- **Pump:** `ScriptedInputSource` pastes and keys arrive in order; a paste queued for a nested app that exits
  goes back to the outer app as a paste.
- **Pseudo-terminal** (Linux and macOS CI): entering raw mode writes `CSI ?2004h` to a terminal stdout and
  restoring writes `CSI ?2004l`. The pty test points stdout at the pty leader via an internal fd parameter, as
  Phase 8's tests do for stdin.
- `CommandLineOverlay` inserts a paste instead of running a command at its line break.

Manual QA (results in the PR), in WSL in Windows Terminal, tmux on Linux and SSH to Linux:
1. Pasting 1,000 characters with line breaks into a Form field inserts them in one step, as one line, without
   submitting.
2. The same paste on the Counter screen (no text field) does nothing.
3. The Input demo shows one `Paste` entry with the right counts.
4. After exit, by every Phase 8 restore path, pasting into the shell isn't wrapped in `^[[200~` (bracketed
   paste is off again, unless the shell itself turns it on).
5. In tmux, pasting with `prefix ]` produces one paste (tmux brackets when the app asked for it).
6. Windows Terminal and conhost: pastes behave as in 1.0.

### 3.10 Migration impact

- New public API (§3.2) in `PublicAPI.Unshipped.txt`. Nothing changes incompatibly: `HandlePaste` is a new
  virtual with a default, and `IInputSource` is unchanged.
- **Behavior changes on Linux and macOS** (CHANGELOG "Changed"): pastes no longer arrive as keys. A component
  that edits text needs `HandlePaste` to accept them; without it, a paste is dropped. On Windows and with
  `MINITUI_INPUT=console`, pastes still arrive as keys until Phase 10.
- README: a "Paste" paragraph under "Keyboard input" with `HandlePaste`, `TextFieldState.Paste` and the drop
  rule.

### 3.11 Implementation order

1. `IInputSink.Paste` and paste mode in `AnsiInputDecoder`, with the decoder tests.
2. `InputItem` and `IInputItemSource`, and carrying items through `InputPump` and `AppLoop`
   (including the nested-app handoff), with `ScriptedInputSource.EnqueuePaste`. Existing tests must pass
   unchanged at this step.
3. `PasteEvent`, `OnPaste`, `Component.HandlePaste`, routing, `ComponentHarness.Paste`, with the
   routing tests.
4. `TextFieldState.Paste`, `CommandLineOverlay`.
5. `CSI ?2004h`/`l` in `UnixTerminal`, with the pty test.
6. Example, README, CHANGELOG, manual QA.

### 3.12 Out of scope

- Windows paste events (Phase 10 decides, with `ReadConsoleInput`). Phase 9 QA found that in conhost a pasted
  line break is an Enter key (which a text field ignores, so lines run together) and Ctrl+V arrives as a key,
  because MiniTui clears processed input to receive Ctrl+C; right-click paste types the text as keys.
- Multi-line paste into a text area (Phase 12 builds on `HandlePaste`).
- A public way for custom input sources to produce pastes.
- Filtering pasted escape sequences ("paste injection" through a pasted `CSI 201~`): the terminal is
  responsible, and most strip the markers from pasted text. Text after an early end marker decodes as keys, as
  in every terminal app.
- Clipboard access (OSC 52) or a copy command.

### 3.13 Decisions taken

| # | Question | Decision |
|---|---|---|
| 1 | A paste nobody handles: replay it as keys (1.0 behavior), or drop it? | **Drop** (§3.1), as Textual and crossterm apps do. |
| 2 | Normalize line breaks in `PasteEvent.Text` to `\n`, or deliver them as the terminal sent them (usually `\r`)? | **Normalize**, as prompt_toolkit does. |
| 3 | A single-line field given pasted line breaks: spaces, first line only, or reject? | **Spaces**, as Bubbles' `textinput` does, after dropping leading and trailing line breaks. |
| 4 | A size limit for very large pastes? | **None**, as in crossterm, Bubble Tea and Textual. |

---

## 4. Mouse and Windows input (Phase 10)

### 4.1 Purpose

Two gaps remain after Phases 8 and 9:
- **Windows input is still 1.0's.** Keys come from `Console.ReadKey`, so a paste arrives as keys (a pasted line
  break is Enter), and in conhost Ctrl+V reaches the app as a key and pastes nothing (found in Phase 9 QA).
  Windows also can't report the mouse through `Console.ReadKey`.
- **No mouse.** Users expect to click a field to focus it, click a checkbox, tab or list row, and scroll a list
  with the wheel.

Phase 10 reads Windows input with `ReadConsoleInput` in VT input mode, so Windows Terminal sends the same bytes
as a Unix terminal and the Phase 8 decoder handles both: Windows gets the Unix key rules (§2.4), bracketed paste
(§3) and mouse reports. Conhost has no bracketed paste, so there MiniTui answers Ctrl+V by reading the clipboard
itself, as PSReadLine does. Then mouse support is added on every platform: a `MouseEvent`, click to focus, and
mouse handling in the built-in states and dialogs.

**Decision:** mouse reporting is opt-in (`app.WithMouse()`), as in crossterm, Bubble Tea and Ratatui. While the
app captures the mouse, the terminal's own text selection needs Shift+drag (Option+drag in iTerm2), so turning
it on is the app's choice, not a 1.2 upgrade surprise. Without `WithMouse`, nothing changes for the mouse.

**Decision:** Windows keys decode through VT input mode (§4.4), not through key records. That makes key handling
the same on every platform and is what gives bracketed paste and mouse in Windows Terminal. The cost: keys that
VT can't express (Ctrl+Enter, Ctrl+Tab, Ctrl+digits) lose their modifier on Windows, as they already do on
Unix. `MINITUI_INPUT=console` restores 1.0 input on Windows too.

### 4.2 Public API

```csharp
public enum MouseButton { None, Left, Middle, Right }

public enum MouseAction { Press, Release, ScrollUp, ScrollDown, ScrollLeft, ScrollRight }

/// A mouse report. X and Y are 0-based cells, like Buffer coordinates. Button is None for scrolls, and for a
/// release when the terminal doesn't say which button was released.
public sealed record MouseEvent(MouseAction Action, MouseButton Button, int X, int Y,
                                ConsoleModifiers Modifiers = 0) : Event
{
    public Vec2 Position { get; }       // (X, Y)
    public bool IsClick { get; }        // a left-button press
}

public sealed class App
{
    /// Ask the terminal to report mouse presses, releases and the wheel while this app runs (§4.6).
    public App WithMouse(bool enabled = true);

    /// Called for each mouse event before the components see it; return true to consume it. Like OnKey.
    public App OnMouse(Func<MouseEvent, bool> handler);
}

public abstract class Component
{
    /// Handle a mouse event inside this component's Area (§4.7). Return true if handled; otherwise it goes on to
    /// the parent, and is dropped if nothing handles it.
    public virtual bool HandleMouse(MouseEvent mouse) => false;

    /// The area the component was last rendered in, through RenderContext.Render or by the app (the top screen
    /// and overlays). Rect.Empty until then.
    public Rect Area { get; }
}

public sealed class RenderContext
{
    /// Render a child: records its Area for mouse hit testing, then calls
    /// child.Render(buf, area, For(child)).
    public void Render(Component child, Buffer buf, Rect area);
}

public sealed class SelectionState
{
    /// The wheel over the list or table moves the selection by one; a left press on a row selects it.
    /// Uses the area from the last draw. Returns true if the event did either.
    public bool HandleMouse(MouseEvent mouse, int count);
}

public sealed class ScrollState
{
    /// The wheel over the text scrolls by three rows. Returns true if it scrolled or tried to.
    public bool HandleMouse(MouseEvent mouse);
}

public sealed class TabBarState
{
    /// A left press on a tab selects it. Returns true if it hit a tab.
    public bool HandleMouse(MouseEvent mouse);
}

public sealed class ScriptedInputSource
{
    /// Queue a mouse event, in order with the queued keys and pastes.
    public ScriptedInputSource EnqueueMouse(MouseEvent mouse);
}

public sealed class ComponentHarness
{
    /// Press and release a button at (x, y), then RunUntilIdle.
    public void Click(int x, int y, MouseButton button = MouseButton.Left);

    /// Scroll the wheel at (x, y): positive rows scroll down, one event per row. Then RunUntilIdle.
    public void Scroll(int x, int y, int rows);

    /// Send one mouse event, then RunUntilIdle.
    public void Mouse(MouseEvent mouse);
}
```

- `child.Render(buf, rect, ctx.For(child))` keeps working; a child rendered that way just isn't a hit target, so
  its clicks go to its nearest ancestor that is (§4.7). The README and Example switch to `ctx.Render`.
- `ConfirmDialog`, `MessageDialog` and `HelpOverlay` handle the mouse (§4.8).

### 4.3 Internal model

```csharp
// Phase 9's item gains a third kind.
internal readonly struct InputItem
{
    public static InputItem FromMouse(MouseEvent mouse);
    public MouseEvent? Mouse { get; }    // null unless a mouse event
}

internal interface IInputSink
{
    void Key(ConsoleKeyInfo key);
    void Paste(string text);
    void Mouse(MouseEvent mouse);        // new
}

// Sources that can turn mouse reporting on and off. TerminalInputSource implements it.
internal interface IMouseCapture
{
    void SetMouseCapture(bool enabled);  // reference counted across running apps
}

// Win32 P/Invokes ([LibraryImport], AOT-safe): GetStdHandle, Get/SetConsoleMode, GetNumberOfConsoleInputEvents,
// ReadConsoleInputW, WaitForSingleObject, GetConsoleScreenBufferInfo, WriteFile; and the clipboard:
// OpenClipboard, IsClipboardFormatAvailable, GetClipboardData, GlobalLock, GlobalUnlock, CloseClipboard.
[SupportedOSPlatform("windows")]
internal static partial class WindowsConsoleInput
{
    public static bool TryEnterVtInput();            // false if stdin isn't a console or VT input is refused
    public static void SetMouse(bool enabled);       // the mouse sequences, ENABLE_MOUSE_INPUT and quick edit
    public static Vec2 WindowOrigin();               // srWindow's top-left, for mouse record positions
    public static void Restore();                    // idempotent; never throws
    public static bool Wait(int milliseconds);       // WaitForSingleObject on the input handle
    public static int Read(Span<InputRecord> records); // only after GetNumberOfConsoleInputEvents > 0
    public static string? ReadClipboardText();      // CF_UNICODETEXT, or null
}

// Turns console input records into decoder bytes and mouse events, in order. Pure, so it's tested on every
// platform. windowOrigin is read once per batch that has mouse records.
internal sealed class WindowsInputTranslator(Func<Vec2> windowOrigin)
{
    public void Translate(ReadOnlySpan<InputRecord> records, AnsiInputDecoder decoder, TimeSpan now,
                          IInputSink sink, out bool resized);
}

// On Windows, turns a Ctrl+V key into a paste of the clipboard's text (§4.5).
internal sealed class ClipboardPasteSink(IInputSink inner, Func<string?> readClipboard) : IInputSink;
```

### 4.4 Windows input

- **Entering** (in `TerminalInputSource.Activate` on Windows, when stdin is a console): save the input mode,
  then set it to the saved mode with `ENABLE_VIRTUAL_TERMINAL_INPUT` and `ENABLE_WINDOW_INPUT` added, and
  `ENABLE_PROCESSED_INPUT`, `ENABLE_LINE_INPUT` and `ENABLE_ECHO_INPUT` removed. Ctrl+C arrives as 0x03, a key,
  as on Unix. If `SetConsoleMode` fails (Windows before 10, or a console without VT input), the source falls back
  to `Console.ReadKey` as in 1.1.
- **Reading:** `WaitForInput` waits on the input handle with `WaitForSingleObject` (no 5 ms polling, as on Unix);
  `TryReadItem` reads what's pending with `ReadConsoleInputW` (never blocking) and translates it:
  - `KEY_EVENT` key-downs with a character: the UTF-16 character, repeated `wRepeatCount` times, is encoded as
    UTF-8 and fed to `AnsiInputDecoder`. A surrogate pair split across records is joined. Key-ups and
    key-downs with no character (bare Shift, Ctrl, Alt) are skipped: in VT input mode, the console has already
    encoded every key as text or a sequence.
  - `MOUSE_EVENT`: translated to `MouseEvent` directly (§4.6), for consoles that report the mouse as records
    instead of sequences.
  - `WINDOW_BUFFER_SIZE_EVENT`: wakes the loop to check the size (the 100 ms poll stays as a fallback).
  - `FOCUS_EVENT` and `MENU_EVENT` are ignored.
- **Output modes:** after entering, `CSI ?2004h` (and the mouse modes, §4.6) are written to stdout when stdout
  is a console with VT processing on. `AnsiBackend.Enter` turns VT processing on, and runs before input starts.
- **Restoring:** `SetConsoleMode` with the saved input mode, after writing `CSI ?2004l` and the mouse-off
  sequences. It's called from the same paths as on Unix (`Deactivate` and `TerminalSession`'s restore hooks,
  including process exit, unhandled exceptions and the console close event).
- The escape timeout (§2.4) applies on Windows too: a lone Esc resolves after 50 ms.
- `Console.ReadKey`, `Console.KeyAvailable` and `Console.In` aren't called while VT input is active.

### 4.5 Paste on Windows

- **Windows Terminal** honors `?2004h` in VT input mode, so pastes there are `PasteEvent`s, exactly as on Unix.
- **Conhost** has no bracketed paste. **Decision:** when VT input is active on Windows and Ctrl+V (0x16)
  arrives as a key outside a paste, MiniTui reads the clipboard's text and delivers it as a `PasteEvent`
  (line breaks normalized, §3.5), instead of the key. If the clipboard has no text, or can't be opened after 3
  tries 10 ms apart, the Ctrl+V key is delivered. Windows Terminal handles Ctrl+V itself, so the app never sees
  0x16 there unless the user unbinds it, and then pasting from the clipboard is still what they meant.
- Right-click paste in conhost types the text as keys, as in 1.0 (a line break is Enter). There's no reliable
  way to tell it from typing; the README says to use Ctrl+V.
- `MINITUI_INPUT=console` and the `Console.ReadKey` fallback keep 1.0 behavior: no paste events, and Ctrl+V is a
  key.

### 4.6 Mouse reporting and decoding

- **Turning it on:** each running app that called `WithMouse` holds a reference on its input source
  (`IMouseCapture`); while any is held, the source has the terminal report the mouse:
  - Unix and VT consoles: `CSI ?1000h` (presses, releases, wheel) then `CSI ?1006h` (SGR encoding), and
    `CSI ?1006l CSI ?1000l` to turn it off, on stdout when it's a terminal. No motion modes (`?1002`, `?1003`).
  - Windows: also `ENABLE_MOUSE_INPUT` on, and `ENABLE_QUICK_EDIT_MODE` off (with `ENABLE_EXTENDED_FLAGS`),
    since quick edit takes mouse drags for selection in conhost. Both are restored with the rest of the mode.
  - Mouse modes are turned off by every restore path, like bracketed paste. Without raw or VT input (fallbacks,
    `MINITUI_INPUT=console`, custom sources), `WithMouse` does nothing.
- **Decoding SGR:** `CSI < b ; x ; y M` is a press and `… m` a release. In `b`: the low two bits are the button
  (0 left, 1 middle, 2 right), +4 Shift, +8 Alt, +16 Control, +32 motion, +64 the wheel (64 up, 65 down,
  66 left, 67 right). `x` and `y` are 1-based and become 0-based. Motion reports and other buttons (128 and up)
  are dropped, as are reports with missing or out-of-range numbers.
- **Decoding X10:** `CSI M` followed by three bytes (button + 32, x + 33, y + 33) decodes the same way, with a
  release (button 3) reporting `MouseButton.None`. A terminal that ignores `?1006` sends this, and without it
  the three bytes would leak as typed characters.
- **Windows records:** `dwButtonState` bit 1 is left, 2 right, 4 middle; a press or release is a bit that
  changed since the previous record. `MOUSE_WHEELED` and `MOUSE_HWHEELED` scroll up or right when the high word
  of `dwButtonState` is positive. `MOUSE_MOVED` records are dropped; `DOUBLE_CLICK` is a press. Positions are
  made relative to the window (`srWindow.Left`, `srWindow.Top`). Modifiers come from `dwControlKeyState`.
- Mouse events outside the current buffer size are dropped. The decoder allocates one `MouseEvent` per report;
  keys still allocate nothing.

### 4.7 Hit testing and routing

- **Hit map.** While a frame renders, the app records each hit target and its rect in render order: the top
  screen (its body rect), each overlay (its placement rect, border included), and every child rendered with
  `ctx.Render`. `Component.Area` is set at the same time. Mouse events are matched against the last frame
  drawn, which is what the user sees.
- **Target.** The target is the last-rendered entry containing the point (later draws are on top, and children
  render after their parents) whose component is still mounted under the current root: the top overlay if one
  is open, otherwise the top screen.
- **Overlays are modal:** with an overlay open, only the top overlay's tree can be hit, and a click outside it
  is dropped. The status bar and toasts aren't targets.

A mouse event goes through these steps, stopping at the first that handles it:

1. `App.OnEvent` sees `MouseEvent`.
2. `App.OnMouse`, if set. Render-callback apps (no screens) stop here.
3. **Click to focus:** a press of any button focuses the target. For each component from the root down to the
   target whose parent's `FocusScope` contains it, the parent focuses it. A target that isn't focusable
   focuses its nearest focusable ancestor. This happens before step 4, so a field is focused when its handler
   runs. Scrolls and releases don't move focus: the wheel scrolls what's under the pointer, as in GUIs.
4. `HandleMouse` on the target, then each ancestor up to the root.
5. Nobody handled it: the event is dropped. Key maps are never consulted.

### 4.8 Built-in mouse handling

| Where | Behavior |
|---|---|
| `SelectionState.HandleMouse` (lists, tables) | Wheel over the widget's area: `Previous`/`Next` (since Phase 20, §14.4: scroll the view by one row, keeping the selection). Left press on a visible row: select it. Header and border presses aren't handled. |
| `ScrollState.HandleMouse` (paragraphs, dialogs) | Wheel over the area: `ScrollBy(±3)`. |
| `TabBarState.HandleMouse` | Left press on a tab's label: select it. |
| `ConfirmDialog` | Left press on Yes or No: choose it and close, as Enter would. |
| `MessageDialog`, `HelpOverlay` | Wheel scrolls the text. |

- The widgets record what the states need while drawing, as `Fit` already does: the list and table record their
  area and row rect (below the header), the paragraph its area, the tab bar each label's x range.
- Checkboxes and buttons are drawn by stateless builders, so the component that owns them handles the click
  (`HandleMouse(m) => m.IsClick && Toggle()`). The README shows the pattern.
- Clicking in a text field focuses it (§4.7); placing the cursor by clicking is out of scope (§4.14).

### 4.9 Configuration

| Setting | Effect |
|---|---|
| `app.WithMouse()` | Report the mouse while the app runs. Off by default. |
| `MINITUI_INPUT=console` | Now also applies on Windows: 1.0 input, no paste events, no mouse. |

### 4.10 Example

- The Example calls `WithMouse()`. Its components render children with `ctx.Render`: clicking focuses text
  fields, toggles checkboxes, selects list rows and tabs, and the wheel scrolls lists and the paragraph demo.
  Clicking a menu item opens it.
- The Input demo lists mouse events as `Mouse  Press Left  (12, 4)  Ctrl`.

### 4.11 Tests and acceptance

Automated (all platforms unless noted):
- **Decoder:** SGR presses, releases and wheels for every button and modifier bit; 1-based to 0-based; motion
  and out-of-range reports dropped; X10 reports, including release and the 223-column limit; a report split at
  every byte offset; mouse reports between keys and pastes stay in order; a mouse report inside a paste is text.
- **Windows translator** (pure, on every OS): key-down records become the right UTF-8, repeat counts expand,
  key-ups and bare modifiers are skipped, a surrogate pair split across records joins, and the bytes for
  arrows, Alt+letter and F5 decode to those keys; mouse records give presses and releases from button-state
  changes, wheel direction from the high word, window-relative positions, and drop moves.
- **Clipboard paste:** with a fake clipboard, Ctrl+V on the Windows VT path becomes a `PasteEvent`; with no text
  it stays a key; inside a bracketed paste 0x16 stays text.
- **Routing** (`ComponentHarness`): the innermost `ctx.Render`ed child gets the event, then its ancestors; a
  press focuses the target's path (and the nearest focusable ancestor of a non-focusable target); wheel and
  release don't move focus; with an overlay open, a click outside it does nothing and a click inside goes to
  it; `OnMouse` returning true stops it; `OnEvent` sees it; key bindings never fire; a popped screen's
  children aren't hit; a child rendered without `ctx.Render` routes to its parent; render-callback apps get
  `OnMouse` only.
- **States and dialogs:** a click on row N of a scrolled list selects `Offset + N`; header and border clicks
  don't; the wheel moves the selection and scrolls a paragraph by three rows; tab clicks select the tab under
  the pointer, including when the first tabs are scrolled off; Yes and No clicks in `ConfirmDialog` resolve it.
- **Pseudo-terminal** (Linux and macOS CI): an app with `WithMouse` writes `?1000h` and `?1006h` after raw mode
  and `?1006l ?1000l` on restore; an app without it writes neither.
- Existing tests pass unchanged.

Manual QA (results in the PR):
- **Windows Terminal (PowerShell) and conhost**, the Windows-only code: Esc, Alt+letter, Ctrl+arrows,
  Shift+Tab, F1–F12, Ctrl+C quits, Ctrl+Z and Ctrl+S do nothing harmful; a 1,000-character multi-line paste
  into a Form field is one line and doesn't submit (Windows Terminal: bracketed; conhost: Ctrl+V); the Input
  demo shows one `Paste`; mouse checks below; after exit by `q`, Ctrl+C and the forced exception, the shell
  echoes, quick edit and selection work again, and pasting isn't bracketed.
- **WSL in Windows Terminal, tmux on Linux and SSH to Linux** (`tools/ssh-test`): click to focus each Form field,
  toggle the checkbox, select list rows and tabs, scroll lists and the paragraph with the wheel, click a
  menu item, click Yes/No in a confirm dialog, click outside an open dialog (nothing happens); the Input demo
  shows presses, releases, wheels and modifiers with the right cells; Shift+drag still selects text; after
  exit by every restore path, clicking in the shell prints nothing.
- **Without `WithMouse`:** the wheel and clicks behave as before (the terminal's own scrolling and selection).

### 4.12 Migration impact

- New public API (§4.2) in `PublicAPI.Unshipped.txt`. Nothing changes incompatibly: `HandleMouse` is a new
  virtual with a default, `Area` and `ctx.Render` are new members, and `IInputSource` is unchanged.
- **Behavior changes on Windows** (CHANGELOG "Changed"): keys decode by the §2.4 rules (Esc resolves after up to
  50 ms; Ctrl+Enter, Ctrl+Tab and Ctrl+digits lose their modifier, as on Unix); in Windows Terminal, pastes
  are `PasteEvent`s; in conhost, Ctrl+V pastes the clipboard as a `PasteEvent`. A component that edits text
  needs `HandlePaste`, as on Unix since 1.1.
- Apps that don't call `WithMouse` see no mouse change.
- README: a "Mouse" section (`WithMouse`, `ctx.Render`, `HandleMouse`, click to focus, the state helpers,
  Shift+drag to select); the Windows paste note is replaced.

### 4.13 Implementation order

Two PRs, both shipping in 1.2.0:

**10a. Windows input**
1. `WindowsInputTranslator` with its tests (pure).
2. `WindowsConsoleInput` P/Invokes, VT input in `TerminalInputSource` on Windows, `WaitForSingleObject`, the
   resize wake, restore hooks, and the fallback.
3. Bracketed paste on Windows and the conhost Ctrl+V clipboard paste, with the fake-clipboard tests.
4. README, CHANGELOG, Windows manual QA.

**10b. Mouse**
1. `IInputSink.Mouse`, SGR and X10 decoding, and Windows mouse records, with the decoder and translator tests.
2. `InputItem.FromMouse`, `MouseEvent`, `ScriptedInputSource.EnqueueMouse`, carrying mouse items through the pump.
3. `IMouseCapture`, `WithMouse` and the mode sequences, with the pty test.
4. The hit map, `ctx.Render`, `Component.Area`, routing, click to focus, `OnMouse`, the harness helpers, with the
   routing tests.
5. The state helpers and dialogs, the Example, README, CHANGELOG, manual QA.

### 4.14 Out of scope

- Drag and motion (`?1002`/`?1003`): dragging a scrollbar or selecting text, and hover styles.
- Double click: terminals don't report it; an app can time two presses itself.
- Placing the text cursor by clicking, and text selection in fields (with TextArea, Phase 12, if wanted).
- Clicking a table header to sort.
- Closing an overlay by clicking outside it (PopupMenu, Phase 13, will need it for its own overlay).
- The Windows Terminal `win32-input-mode` (`?9001`) and the kitty keyboard protocol, which would keep Ctrl+Enter
  and the like.
- Telling right-click paste from typing in conhost.
- A public way for custom input sources to produce pastes or mouse events.

### 4.15 Decisions taken

| # | Question | Decision |
|---|---|---|
| 1 | Mouse on by default (as Textual), or opt-in with `WithMouse` (as crossterm, Bubble Tea and Ratatui)? Capturing the mouse takes over the terminal's text selection. | **Opt-in** (§4.1). |
| 2 | Windows keys through VT input mode (Unix decoding rules everywhere, bracketed paste and mouse in Windows Terminal), or through key records (keeps Ctrl+Enter and friends, but Windows Terminal pastes arrive as keys)? | **VT input** (§4.1), as Textual does. |
| 3 | Conhost Ctrl+V: read the clipboard and deliver a `PasteEvent`, or leave it a key? | **Read the clipboard** (§4.5), as PSReadLine does. |
| 4 | Clicks act on press (crossterm and most TUIs) or on release over the same target (GUI buttons)? | **Press.** Release events are still delivered. |
| 5 | Wheel step: one item per event in lists and tables, three rows in text? | **1 and 3.** |
| 6 | One PR for the phase (SPEC.md §0), or two, 10a Windows input and 10b mouse? | **Two** (§4.13): the Windows part needs hand testing on its own. |

---

## 5. Suspend and resume (Phase 11)

### 5.1 Purpose

Since Phase 8, raw mode clears `ISIG`, so Ctrl+Z reaches the app as a key and does nothing (§2.5). Unix users
expect Ctrl+Z to put a terminal program in the background and `fg` to bring it back. And an app that runs a
child process in the terminal, such as `$EDITOR` for a commit message or `less` for a log, has no way to hand
the terminal over: the child inherits raw mode, bracketed paste and mouse reporting, draws on MiniTui's
alternate screen, and MiniTui's input thread steals its keystrokes.

Phase 11 adds `app.Suspend(Action)`, which gives the terminal back to the shell's state, runs the action, then
re-enters and redraws, and builds job control on it: on Linux and macOS, `app.SuspendProcess()` stops the
process (SIGTSTP) with the terminal restored, `fg` resumes it (SIGCONT) and the app redraws, and
`app.WithSuspendKey()` makes an unhandled Ctrl+Z do that. `Suspend` works on Windows too; Ctrl+Z there stays an
ordinary key, since Windows consoles have no job control.

**Decision:** Ctrl+Z suspending is opt-in (`WithSuspendKey()`), as in Bubble Tea, crossterm, prompt_toolkit and
tview, which deliver Ctrl+Z as a key and leave suspending to the app. Without it, Ctrl+Z stays a key that does
nothing unless the app binds it, as in 1.1.

### 5.2 Public API

```csharp
public sealed class App
{
    /// Give the terminal back while action runs: stop reading input, turn mouse reporting and bracketed paste
    /// off, restore the terminal mode, leave the alternate screen and stop capturing Console.Out/Error. Then
    /// enter again, send OnEvent a ResumeEvent and redraw fully. Input typed during the action goes to the
    /// action or its child process, not to the app. If action throws, the app resumes and the exception
    /// propagates. Calls from inside an action run their action directly.
    /// Throws InvalidOperationException if the app isn't running or this isn't the UI thread.
    public void Suspend(Action action);

    /// Stop the process and return to the shell, with the terminal given back as Suspend does; returns once the
    /// shell resumes it (fg), after the app has re-entered. Returns false without doing anything where there's no
    /// job control: Windows, or an app not drawing to the real console. Same thread rules as Suspend.
    public bool SuspendProcess();

    /// Make an unhandled Ctrl+Z call SuspendProcess (§5.5). Off by default. Has no effect on Windows.
    public App WithSuspendKey(bool enabled = true);
}

/// Sent to OnEvent after the app re-enters the terminal: after Suspend, after the process is resumed from
/// Ctrl+Z, and after a SIGCONT the app didn't cause.
public sealed record ResumeEvent : Event;
```

- README: a "Running other programs" section with the `$EDITOR` example (§5.8), and Ctrl+Z in the keyboard
  section.

### 5.3 Internal model

```csharp
// Pauses the reader thread for a source and gives the terminal back (Deactivate); Resume reactivates it.
// Pause returns only once the reader is outside TryRead, so it never reads a byte meant for a child.
internal sealed class InputPump
{
    public static IDisposable Pause(IInputSource source);   // nests; the last dispose resumes
}

// Leaves the whole session, whatever its lease count (nested apps share it), and enters it again.
internal static class TerminalSession
{
    static IDisposable Suspend(Session session);       // via Terminal.Suspend(); Enter and bump Version on dispose
    static IDisposable ListenForJobControl(Action stop, Action continued);  // the most recent listener is called
    static bool HandleStopSignal();                    // SIGTSTP from outside; true = cancel the default action
    static void HandleContinueSignal(bool suspended);  // SIGCONT
}

internal sealed class ConsoleCapture
{
    public IDisposable Pause();   // put the original writers back until disposed; lines already captured stay
}

public partial class App
{
    // Stopping the process, swappable in tests so a test never stops the test runner.
    internal Action StopProcessAction { get; set; }     // UnixTerminal.StopProcess: kill(getpid(), SIGTSTP)
    internal bool? CanStopProcessOverride { get; set; } // default: Linux or macOS, and drawing to the real console
}
```

`UnixTerminal` gains a `kill` P/Invoke (SIGTSTP is 20 on Linux and 18 on macOS). `UnixTerminal` and `WindowsConsoleInput` keep the mode saved
on first entry across a suspend: re-entering reapplies raw (or VT) mode without saving again, so a child that
exits without restoring its own mode can't become the mode MiniTui restores at exit.

### 5.4 Suspend

`app.Suspend(action)` on the UI thread:

1. **Give the terminal back,** in this order: pause the app's input source (`InputPump.Pause`: the reader
   stops, then `Deactivate` turns mouse reporting and bracketed paste off and restores the saved termios or
   console mode); pause console capture; suspend the session (`Backend.Leave`: reset attributes, show the
   cursor, re-enable wrapping, leave the alternate screen, restore `TreatControlCAsInput`, the output encoding
   and the Windows output mode).
2. **Run the action.** While it runs, MiniTui's SIGINT and SIGQUIT handling (and Ctrl+C on Windows) is
   cancelled, as `system(3)` does, so Ctrl+C in a child like `ping` stops the child and not the app. The
   SIGTERM and SIGHUP restore handlers stay; they find the terminal already restored.
3. **Take the terminal again,** in reverse: enter the session (alternate screen, cleared; every terminal on
   the session redraws fully next frame, as after a nested app), resume capture, resume input (`Activate`:
   raw or VT mode, bracketed paste, and mouse reporting if any running app holds a capture). Step 3 runs in a
   `finally`, so an action that throws still resumes.
4. Send `ResumeEvent` to `OnEvent`, check the size (the terminal may have been resized meanwhile) and draw
   a full frame.

- Keys decoded but not yet handled before the suspend stay queued and are handled after it. An Esc the decoder
  is holding resolves when the source is paused.
- Ticks keep queueing during the action, at most one at a time as usual; nothing is drawn until it returns.
- .NET reapplies its own terminal settings after a child started with `Process.Start` exits, possibly after
  step 3. The 250 ms mode check (§2.5) puts raw mode back, as it already does.
- With a `TestBackend` and a scripted source, `Suspend` does the same steps on them: the backend is left and
  entered again, and scripted input isn't read during the action. That's what the tests check.

### 5.5 Ctrl+Z

- **With `WithSuspendKey()`,** Ctrl+Z (0x1A) suspends only if nothing else handled it. It goes through `OnKey`
  and the §9.4 routing as usual; if no component or binding handles it (and no overlay is open, since overlays
  swallow keys), the app calls `SuspendProcess`. In render-callback apps, the step comes after `OnKey`. A
  component or binding for `ctrl+z` (undo, for instance) still gets it first. Without `WithSuspendKey()`, an
  unhandled Ctrl+Z is dropped, as in 1.1; an app can also bind any key to `SuspendProcess` itself.
- **Stopping** (`SuspendProcess`), where there's job control: `Suspend(StopProcessAction)`. `Stop` sends SIGTSTP to the
  process itself; the default action stops it and the shell reports it as stopped. On `fg`, SIGCONT continues
  it, `kill` returns, and `Suspend` re-enters and redraws.
- **The SIGTSTP handler** (for "SIGTSTP from outside", below) is registered only while the app is in the
  terminal, and removed for the length of every `Suspend`. The Phase 11 spike found that once .NET has a SIGTSTP
  handler, an uncancelled SIGTSTP doesn't stop the process; without one, the default action does. So `Stop`
  runs with no handler and stops the process, and a Ctrl+Z inside a `Suspend` action stops MiniTui together
  with the child.
- **After SIGCONT,** .NET reapplies its own terminal mode, after MiniTui's handlers run (seen in the spike: ISIG
  comes back on). So for 1 s after a resume, the input source checks the mode every 20 ms instead of every
  250 ms.
- **`bg` instead of `fg`:** the process continues in the background, and re-entering raw mode from a background
  process group raises SIGTTOU, which stops it again until `fg`. That's standard job-control behavior; MiniTui
  doesn't fight it.
- **SIGTSTP from outside** (`kill -TSTP`, a debugger, an IDE's terminal): the handler cancels the default action
  and calls the most recent `ListenForJobControl` listener; that app posts `SuspendProcess` to its UI thread,
  whether or not it called `WithSuspendKey`, since the stop was asked for either way. With no app running (a
  bare `Terminal`), no handler is registered and the process stops as before.
- **SIGCONT the app didn't cause** (resumed after `kill -STOP`): the shell may have drawn over the alternate
  screen, so the app sends `ResumeEvent` and redraws fully. The mode check restores raw mode.
- **Windows,** `MINITUI_INPUT=console`, custom backends and sources: Ctrl+Z is an ordinary key even with
  `WithSuspendKey()`, and `SuspendProcess` returns false.
- Ctrl+Z inside a running `Suspend` action reaches the child (the terminal is cooked), and stops the whole
  process group, MiniTui included; `fg` resumes both, and the action carries on.

### 5.6 Nested apps

- `Suspend` from an inner app suspends the whole session: the outer app's frame is gone too, and both redraw
  after (the outer one when it next draws). Input is paused for the shared source, so the outer app doesn't read
  either.
- Running another MiniTui app inside a `Suspend` action isn't supported: it throws `InvalidOperationException`
  rather than drawing on the shell's screen.

### 5.7 Configuration

| Setting | Effect |
|---|---|
| `app.WithSuspendKey()` | An unhandled Ctrl+Z suspends the process to the shell (Linux and macOS). Off by default. |

No environment variables.

### 5.8 Example

- The Example's Input demo gets Ctrl+E (it captures plain keys), which opens `$VISUAL`, `$EDITOR`, or `vi`
  (`notepad` on Windows) on a temporary file through `app.Suspend`, then shows the saved text in a
  `MessageDialog`. The editor runs through `sh -c` (`cmd /c` on Windows), as git does, since `$EDITOR` may hold
  arguments. The demo passes Ctrl+Z on to the app, as it does Ctrl+C. The README shows the pattern:

```csharp
app.Keys.Add("ctrl+e", "Edit notes", () => app.Suspend(() =>
{
    var editor = Environment.GetEnvironmentVariable("EDITOR") ?? "vi";
    using var process = Process.Start(new ProcessStartInfo(editor) { ArgumentList = { path } })!;
    process.WaitForExit();
}));
```

- The Example calls `WithSuspendKey()`, and the menu's footer mentions Ctrl+Z on Linux and macOS.

### 5.9 Tests and acceptance

Automated (all platforms unless noted):
- **Suspend** (`ComponentHarness`/`TestBackend`): the action runs with the backend left and `Console.Out` the
  original writer; afterwards the backend is entered again, a full redraw follows, and `OnEvent` gets one
  `ResumeEvent`; scripted keys queued during the action are handled after it, in order, and keys queued before
  it stay ahead of them; an action that throws resumes and the exception reaches `OnError`; `Suspend` off the
  UI thread or when not running throws; a nested `Suspend` runs its action directly; `Suspend` from a nested
  app leaves and re-enters once and the outer app redraws fully; starting an app inside the action throws.
- **Input pause:** while paused, the pump doesn't call `TryReadItem`; `Pause` waits for a read in progress;
  pauses nest.
- **Ctrl+Z** (with `StopProcessAction` replaced): without `WithSuspendKey`, Ctrl+Z never calls `Stop`; with it,
  an unhandled Ctrl+Z calls `Stop` inside a suspend, once; `SuspendProcess` returns false and does nothing when
  there's no job control; a
  component or app binding for `ctrl+z` gets it and `Stop` isn't called; with an overlay open, or
  no job control, nothing happens; render-callback apps suspend after `OnKey`.
- **Signals** (with the handlers' logic called directly): an outside SIGTSTP goes to the most recent listener
  and is cancelled, and with no listener it isn't; SIGCONT goes to the listener unless the session is suspended;
  the app's stop handler suspends the process even without `WithSuspendKey`, and its continue handler sends a
  `ResumeEvent`.
- **Pseudo-terminal** (Linux and macOS CI): while input is paused, the pty's termios is the saved (cooked) mode
  and `?1006l ?1000l ?2004l` were written; after it, raw mode is back with `?2004h ?1000h ?1006h`; the mode
  restored at exit is the original one even if the termios was changed during the pause. (The alternate screen
  is the backend's, covered by the `TestBackend` tests.)
- Existing tests pass unchanged.

Manual QA (results in the PR):
- **Linux (bash and zsh), tmux, SSH (`tools/ssh-test`) and macOS if available:** Ctrl+Z from a screen, a text
  field and the Input demo returns to a working shell (echo, line editing, no stray mouse or paste sequences);
  `jobs` lists it as stopped; `fg` restores the screen exactly, with keys, paste and mouse working; repeat
  several times; resize while stopped, then `fg`; `bg` then `fg`; `kill -TSTP` and `kill -CONT` from another
  terminal; Ctrl+Z inside an open dialog does nothing; an app without `WithSuspendKey` ignores Ctrl+Z.
- **The editor demo** with `vi`, `nano` and `$EDITOR` unset: the editor gets every key, paste and the mouse
  (where it supports them); after it exits the app redraws and shows the text; Ctrl+C in the editor doesn't quit
  the app; Ctrl+Z in vi stops everything and `fg` returns to vi.
- **Windows Terminal and conhost:** the editor demo with `notepad` and with a console editor (`edit` or `vim` if
  installed); Ctrl+Z still does nothing; quick edit and the cursor are back during the action.

### 5.10 Migration impact

- New public API (§5.2) in `PublicAPI.Unshipped.txt`. Nothing changes incompatibly.
- No behavior change for apps that don't call the new API: Ctrl+Z still does nothing unless bound. The one
  exception (CHANGELOG "Changed"): a SIGTSTP sent from outside now restores the terminal before stopping, and
  redraws on resume, instead of stopping with the terminal in raw mode.

### 5.11 Implementation order

One PR, shipping in 1.2.0:
1. A spike on Linux: what an uncancelled SIGTSTP does with and without a `PosixSignalRegistration`, and what
   .NET does to the termios on SIGCONT and after a child exits. Done; the results are in §5.5.
2. `InputPump.Pause`, keeping the saved mode across re-entry in `UnixTerminal` and `WindowsConsoleInput`, with
   the pause and pty tests.
3. `TerminalSession.Suspend`, `ConsoleCapture.Pause`, `App.Suspend`, `ResumeEvent`, with the suspend tests.
4. `StopProcessAction`, `SuspendProcess`, the Ctrl+Z step, `WithSuspendKey`, the SIGTSTP and SIGCONT handlers, with the Ctrl+Z and
   signal tests.
5. The Example's editor demo, README, CHANGELOG, manual QA.

### 5.12 Out of scope

- Ctrl+Z on Windows (no job control), and a "minimize" or other stand-in.
- `SuspendAsync`, or keeping the event loop running while the action does: the action blocks the UI thread,
  as a child editor blocks the user.
- Running a MiniTui app inside a `Suspend` action (§5.6); nested apps already cover showing another app.
- Capturing a child's output into the app (use `Process` with redirection; no suspend needed).
- Saving and restoring the shell screen's scrollback beyond what the alternate screen already does.

### 5.13 Decisions taken

| # | Question | Decision |
|---|---|---|
| 1 | Ctrl+Z suspends by default (ncurses, vim, less) or opt-in (Bubble Tea, crossterm, prompt_toolkit, tview)? | **Opt-in**, `WithSuspendKey()` (§5.1). |
| 2 | With it on, Ctrl+Z suspends only when nothing handled it (app bindings and components win), or always, before routing, as Ctrl+C quits? | **Only when unhandled** (§5.5): `ctrl+z` stays free for undo. |
| 3 | During a `Suspend` action, ignore Ctrl+C in MiniTui so it only stops the child, as `system(3)`? Otherwise Ctrl+C in `ping` would quit the app too. | **Ignore** (§5.4). |
| 4 | Handle SIGTSTP sent from outside (`kill -TSTP`) like `SuspendProcess`, restoring the terminal, even without `WithSuspendKey`, or leave the 1.1 behavior (stops with the terminal still in raw mode)? | **Like `SuspendProcess`** (§5.5). |
| 5 | Send a `ResumeEvent` so apps can refresh data after a long stop? | **Yes** (§5.2), as Bubble Tea's `ResumeMsg`. |

---

## 6. TextArea (Phase 12)

### 6.1 Purpose

`TextField` edits one line. Apps also need multi-line input: notes, descriptions, commit messages, a message
to send. Today they'd have to build it from scratch. Phase 12 adds `TextAreaState` and `Widget.TextArea()`: the
same editing rules as a text field (§8.4: grapheme-safe cursor, word moves and deletes), plus line breaks,
soft wrapping, moving between rows, vertical scrolling, multi-line paste (Phase 9) and placing the cursor with
the mouse (Phase 10). There's no selection, clipboard or undo (§6.12).

### 6.2 Public API

```csharp
/// A position in a TextAreaState: a line, and a UTF-16 index into that line on a grapheme cluster boundary.
public readonly record struct TextPosition(int Line, int Index);

public class TextAreaState
{
    /// Create a state with optional initial text. The cursor starts at the end.
    public TextAreaState(string text = "");

    /// The text, lines joined by "\n". Setting it normalizes "\r\n" and "\r" to "\n", turns tabs into spaces
    /// (§6.4), removes other control characters, and moves the cursor to the end.
    public string Text { get; set; }

    /// The lines, without line breaks. Never empty: empty text is one empty line.
    public IReadOnlyList<string> Lines { get; }

    /// The cursor. Set values are clamped to the text and snap to the next cluster boundary.
    public TextPosition Cursor { get; set; }

    /// The first visible row (a wrapped row, not a line). Written by the widget while drawing, and by the wheel.
    public int ScrollOffset { get; }

    /// Clear the text.
    public void Clear();

    /// Insert text at the cursor and move the cursor past it. Line breaks in the text start new lines.
    public void Insert(string text);

    /// Insert pasted text at the cursor, keeping its line breaks (tabs and control characters as for Text).
    /// Returns true, so a component can write HandlePaste(text) => State.Paste(text).
    public bool Paste(string text);

    /// Handle an editing or movement key (§6.3). Returns true if it was one.
    public virtual bool HandleKey(ConsoleKeyInfo key);

    /// A left press inside the text moves the cursor there; the wheel scrolls by three rows (§6.6). Uses the
    /// area from the last draw. Returns true if it did either.
    public bool HandleMouse(MouseEvent mouse);
}

public static class Widget
{
    public static TextAreaBuilder TextArea();
}

/// A multi-line text input. Long lines wrap; the view scrolls vertically to keep the cursor visible, and when
/// focused shows the terminal cursor at the insertion point.
public sealed class TextAreaBuilder
{
    public TextAreaBuilder State(TextAreaState state);
    public TextAreaBuilder Placeholder(string? placeholder);  // muted text while empty, on the first row
    public TextAreaBuilder Focused(bool focused = true);
    public TextAreaBuilder Block(BlockBuilder block);
    public TextAreaBuilder Style(Style style);                 // patched over the theme's input style
    public TextAreaBuilder Wrap(WrapMode wrap);                // Word (the default), Char, or None (§6.5)
    public void Draw(Buffer buf, Rect area);
}
```

- It draws with the existing `Theme.Input`, `Theme.InputFocused` and `Theme.Cursor` styles; no theme changes.
- A component that hosts a text area overrides `CapturesText => true` (§9.4), as for a text field, so plain-key
  bindings don't fire while typing.

### 6.3 Keys

The §8.4 rules apply within a line: any non-control `KeyChar` is typed, surrogate pairs are joined, the cursor
moves and deletes by grapheme cluster. Across lines:

| Key | Effect |
|---|---|
| Enter | Split the line at the cursor; the cursor goes to the start of the new line. |
| ← / → | Move by cluster; at the start or end of a line, to the end of the previous or start of the next line. |
| Ctrl+← / → | Move by word, across line breaks (a line break counts as whitespace). |
| ↑ / ↓ | Move to the previous or next **row** (wrapped row, not line), to the column nearest the *goal column*. On the first row, ↑ moves to the start of the text; on the last row, ↓ to the end. |
| PgUp / PgDn | Move up or down by the visible height (from the last draw), keeping the goal column. |
| Home / End | Start or end of the current row. Pressed again at the start of a wrapped row, Home goes to the start of the line (End likewise). |
| Ctrl+Home / Ctrl+End | Start or end of the text. |
| Backspace / Delete | Delete a cluster; at the start (end) of a line, join it with the previous (next) line. |
| Ctrl+Backspace, Ctrl+W | Delete the previous word, across a line break. |
| Ctrl+U | Delete to the start of the line; at the start, join with the previous line. |
| Ctrl+K | Delete to the end of the line; at the end, join with the next line (as in Emacs). |

- **The goal column** is the display column the cursor had after the last horizontal move, edit or click, so
  moving ↓ through a short line and on to a long one returns to the same column. Vertical moves keep it.
- **Not handled** (they return false, so they reach the component and the bindings): Tab and Shift+Tab (focus
  moves, §6.12), Esc, and keys with Ctrl or Alt that aren't in the table, such as Ctrl+S or Alt+Enter, which an
  app can bind to submit. Ctrl+Enter can't be told from Enter in most terminals (§4.1), so it isn't used.
- Every key in the table returns true even when it can't move (↑ at the very start), so it never leaks to a
  binding.

### 6.4 Text model

- Stored as a list of lines (`string`), so an edit costs one line, not the whole text.
- **Tabs** become spaces up to the next multiple of 4 display columns, when typed, inserted or pasted, so the
  text never holds a `\t` and the cursor never sits inside one. Other control characters are removed, as in
  `TextFieldState.Paste`. `Text` uses `\n` only.
- **Paste** keeps line breaks (normalized by Phase 9 to `\n`), unlike `TextFieldState.Paste`. A 10,000-line
  paste is one `Insert`.
- `Cursor.Index` is a UTF-16 index, like `TextFieldState.Cursor`; it's always on a cluster boundary.

### 6.5 Layout and drawing

- **Wrapping** (`WrapMode.Word`, the default): each line is split into rows no wider than the area, breaking
  after whitespace, as `Paragraph` does; a word wider than the area breaks anywhere. Unlike `Paragraph`, the
  spaces at a wrap point stay at the end of their row, so every index has a place for the cursor and the text
  isn't changed. A wide character is never split across rows. `WrapMode.Char` breaks anywhere.
- **`WrapMode.None`:** one row per line, and the whole view scrolls horizontally by display width to keep the
  cursor visible, as a text field does.
- **The cursor at a wrap point** shows at the start of the next row; at the end of a line, after the last
  character. A row that's full to the edge with the cursor at its end shows the cursor on the next row's first
  cell (a row that exists only for the cursor), so the cursor is always drawn inside the area.
- **Vertical scrolling:** after an edit, move or click, the view scrolls just enough to show the cursor's row.
  The wheel scrolls without moving the cursor, and the view stays where the wheel left it until the cursor
  moves again. `ScrollOffset` is clamped so the last row can reach the bottom but not above it.
- The layout is computed per draw from the lines; the rows above the view are measured but not drawn. That's
  linear in the text, fine for the notes and messages this is for (§6.12).
- Empty text with a placeholder shows the placeholder in `TextMuted` on the first row, with the cursor at its
  start.
- When focused, the cursor cell gets `Theme.Cursor` and the terminal cursor is placed there, as in a text
  field.

### 6.6 Mouse

- `HandleMouse`: a left press inside the area (from the last draw, as with `SelectionState`) moves the cursor
  to the nearest cluster boundary at that cell: the clicked character's start, or the row's end if the click is
  past it. It sets the goal column. Presses on the block's border, and other buttons, aren't handled.
- The wheel scrolls by three rows (decision 5 of §4.15), without moving the cursor.
- Click to focus (§4.7) runs first, so a click in an unfocused text area focuses it and places the cursor.

### 6.7 Configuration

None.

### 6.8 Example

- The Form demo gets a **Notes** text area below the existing fields, in the Tab order, with a placeholder. The
  toast shown on submit includes the line count. Submit stays on Ctrl+S (Enter in the notes is a line break).

### 6.9 Tests and acceptance

Automated (all platforms):
- **Editing:** typing, Enter splitting a line (at start, middle, end), Backspace and Delete joining lines,
  Ctrl+W across a line break, Ctrl+U and Ctrl+K at line ends, surrogate pairs and combining marks (never split),
  tabs to spaces at the right column, control characters removed, `Text` round trip with `\r\n`.
- **Movement:** ← → across lines; Ctrl+← → across lines; ↑ ↓ by wrapped row with the goal column, through short
  and wide-character lines; ↑ on the first row and ↓ on the last; Home/End by row then by line; Ctrl+Home/End;
  PgUp/PgDn by the drawn height.
- **Layout:** word and char wrap with the spaces kept at the wrap point; a wide character at the edge moves to the
  next row; the full-row cursor row; `WrapMode.None` horizontal scrolling; scroll follows the cursor, the wheel
  scrolls without moving it, and the clamp at the bottom.
- **Snapshots** (`TestBackend`): empty with placeholder, focused and unfocused, wrapped and scrolled text, a block.
- **Paste:** multi-line paste inserts lines at the cursor and the cursor ends after it; a large paste (10,000
  lines) completes quickly.
- **Mouse:** a click on a character, past a row's end, on a wrapped row, below the last row (end of text) and on
  the border; wheel scrolling; click to focus then place, through `ComponentHarness.Click`.
- **Routing:** Tab and Ctrl+S aren't handled; Enter in a text area doesn't fire an Enter binding.

Manual QA (results in the PR): in the Form demo on Linux (tmux and Windows Terminal through WSL) and Windows
(Windows Terminal and conhost): type several paragraphs, resize narrower and wider, move with every key in §6.3,
paste a few hundred lines, click to place the cursor and scroll with the wheel; emoji and CJK text.

### 6.10 Migration impact

- New public API (§6.2) in `PublicAPI.Unshipped.txt`. Nothing changes for existing code.
- README: `TextArea()` in the widget table, `TextAreaState` in the state table, and a short example.

### 6.11 Implementation order

One PR, in 1.3.0 with Phase 13:
1. `TextAreaState`: the line model, editing, Paste, ←→ and word moves, with their tests.
2. The row layout (wrapping into rows of indexes) shared by drawing, ↑↓, PgUp/PgDn, Home/End and the mouse, with
   the layout tests.
3. `TextAreaBuilder`: drawing, the cursor, vertical (and `None` horizontal) scrolling, with snapshots.
4. `HandleMouse`.
5. The Example, README, CHANGELOG, manual QA.

### 6.12 Out of scope

- Selection, copy/cut, and the system clipboard (paste comes from the terminal's paste).
- Undo and redo.
- Indenting: Tab moves focus, as everywhere else in MiniTui, and there's no indent key; indentation is typed as
  spaces, and pasted tabs become spaces. An opt-in Tab-to-indent mode (as Textual's `tab_behavior`) and
  auto-indent on Enter were considered and left for later.
- Line numbers, syntax highlighting, search, and a scrollbar (an app can draw `Widget.Scrollbar()` beside it
  from `ScrollOffset`).
- Large documents: the layout is linear in the text per frame (§6.5).
- A maximum length or line count, and validation.
- Dragging with the mouse (no motion reports, §4.14).

### 6.13 Decisions taken

| # | Question | Decision |
|---|---|---|
| 1 | Undo and redo (Ctrl+Z / Ctrl+Y) in this phase, as Textual's `TextArea` has, or leave them out, as Bubbles' `textarea` does? Undo would also take Ctrl+Z from `WithSuspendKey` while a text area is focused. | **Leave out** (§6.12); a later phase can add it. |
| 2 | Tab: move focus (consistent with forms), or insert spaces (as editors do)? An opt-in indent mode, or auto-indent on Enter? | **Move focus; no indent mode or auto-indent for now** (§6.12). |
| 3 | ↑ / ↓ by wrapped row (editors, Textual) or by line (Bubbles)? | **By wrapped row.** |
| 4 | Tabs in pasted or set text: spaces to the next multiple of 4, or keep `\t` and draw it as spaces? | **Spaces** (§6.4): the text and the cursor stay simple. |
| 5 | Mouse click to place the cursor in this phase (§4.14 deferred it here)? | **Yes** (§6.6). |

---

## 7. PopupMenu and Select (Phase 13)

### 7.1 Purpose

Apps that want a context menu or a dropdown field today have to build one from an `Overlay`, and the overlay
frame (a centered, padded, titled block) doesn't fit either. Phase 13 adds a `PopupMenu` overlay placed next to
an anchor (a cell, a list row, a field) that the user moves through with the arrow keys, type-ahead or the mouse, and that
answers with the chosen item; and `SelectState` and `Widget.Select()`, a form field that shows one choice and
opens a `PopupMenu` of its options. Two small `Overlay` additions make this possible: an overlay can draw its own
frame, and can close when the user clicks outside it (deferred here by §4.14).

### 7.2 Public API

```csharp
/// An item in a PopupMenu.
public sealed record MenuItem(string Label)
{
    /// Display-only text drawn right-aligned and muted, such as a key: "Ctrl+S", "x".
    public string? Hint { get; init; }

    /// A disabled item is drawn muted, skipped by the keys and not chosen by a click.
    public bool Enabled { get; init; } = true;

    /// A divider line. It is never selected or chosen.
    public static MenuItem Separator { get; }
    public bool IsSeparator { get; }
}

/// A menu overlay next to an anchor (§7.4). Enter or a click chooses an item; Esc, Tab or a click outside
/// dismisses it.
public sealed class PopupMenu : Overlay
{
    /// anchor is in screen cells: a 1×1 rect for a point (a right-click), or the rect of a row or field.
    /// selected is the item selected at first; a separator or disabled item moves it to the next enabled one.
    public PopupMenu(IReadOnlyList<MenuItem> items, Rect anchor, int selected = 0);

    /// The chosen item's index into items, or null when the menu was dismissed or closed any other way.
    public Task<int?> Result { get; }

    /// The menu is at least this wide, border included (a Select passes its field's width). Default 0.
    public int MinWidth { get; init; }

    /// At most this many item rows are shown; more scroll. Default 12.
    public int MaxRows { get; init; }
}

public sealed class OverlayStack
{
    /// Show a PopupMenu and await the chosen index (null if dismissed). With no items it shows nothing and
    /// answers null.
    public Task<int?> ShowMenuAsync(IReadOnlyList<MenuItem> items, Rect anchor, int selected = 0);
}

public abstract class Overlay
{
    /// False: the app only clears the Placement rect and the overlay draws everything in it, border included
    /// (Render gets the whole rect). True by default: the themed block as in 1.0 (§9.3).
    public virtual bool Framed => true;

    /// True: a press of any button outside the overlay closes it, and the press goes nowhere else (§7.6).
    /// False by default.
    public virtual bool CloseOnClickOutside => false;
}

/// The options and choice of a dropdown field.
public sealed class SelectState
{
    public SelectState(IReadOnlyList<string>? options = null, int? selected = null);

    /// The options. Setting them keeps Selected if it's still in range, otherwise clears it.
    public IReadOnlyList<string> Options { get; set; }

    /// The chosen option's index, or null for none (the placeholder shows). Clamped to Options when set.
    public int? Selected { get; set; }

    /// The chosen option, or null.
    public string? Value { get; }

    /// True while its menu is open.
    public bool IsOpen { get; }

    /// Raised on the UI thread when the user's choice changes Selected (not when code sets it).
    public event EventHandler? Changed;

    /// Show the options in a PopupMenu below the field from the last draw, with the current choice selected.
    public void Open(App app);

    /// Enter, Space, ↑, ↓, Alt+↓ and F4 open the menu (§7.5). Returns true if it opened.
    public bool HandleKey(ConsoleKeyInfo key, App app);

    /// A left press on the field opens the menu. Returns true if it opened.
    public bool HandleMouse(MouseEvent mouse, App app);
}

public static class Widget
{
    public static SelectBuilder Select();
}

/// A dropdown field: the chosen option and a ▼ at the right edge.
public sealed class SelectBuilder
{
    public SelectBuilder State(SelectState state);
    public SelectBuilder Placeholder(string? placeholder);  // muted, while nothing is chosen
    public SelectBuilder Focused(bool focused = true);
    public SelectBuilder Block(BlockBuilder block);
    public SelectBuilder Style(Style style);                 // patched over the theme's input style
    public void Draw(Buffer buf, Rect area);
}
```

- A component that hosts a select passes its `App`: `HandleKey(key) => State.HandleKey(key, App)`. The state
  needs it to open the overlay; the closed field has no text capture, so it doesn't set `CapturesText`.
- No theme changes: the menu uses `ModalBorder`, `Text`, `Selection` and `TextMuted`; the field uses `Input`,
  `InputFocused` and `TextMuted`.

### 7.3 PopupMenu keys

| Key | Effect |
|---|---|
| ↑ / ↓ | Previous or next enabled item, wrapping around at the ends. |
| PgUp / PgDn | Up or down by the visible rows, stopping at the first or last enabled item. |
| Home / End | First or last enabled item. |
| Enter | Choose the selected item (nothing happens when no item is enabled). |
| Esc, Tab, Shift+Tab | Dismiss (`Result` is null). Focus doesn't move. |
| A printable character | Type-ahead (below). |

- **Type-ahead:** characters typed without Ctrl or Alt build a search string, cleared 1 s after the last one
  (the app's `TimeProvider`, so tests use fake time). The selection moves to the first enabled item, starting
  at the selected one, whose label starts with the string (ordinal, ignoring case). Typing the same character
  again (`b`, `b`) moves to the next item starting with it, as in Windows menus and list boxes. No match leaves
  the selection where it is. Type-ahead never chooses an item.
- The menu handles every key (it's an overlay, §9.4), so nothing leaks to the screen behind it.

### 7.4 Placement and drawing

- **Rows:** a border on all sides (`ModalBorder`), one row per item, no title, no padding. Each item row is
  ` Label` on the left and `Hint ` on the right, at least two spaces apart. A separator is a `─` line across
  the inner width. The selected row gets `Theme.Selection` across the whole inner width; disabled items and
  hints are `TextMuted` (on the selected row, the selection style wins).
- **Width:** the widest label plus the widest hint plus the gaps and border, at least `MinWidth`, at most the
  screen width. Labels that don't fit are truncated with `…`.
- **Height:** the item count, at most `MaxRows`, plus the border. More items scroll like a list, keeping the
  selection visible, with a scrollbar on the right border.
- **Where:** below the anchor (top-left corner at the anchor's bottom-left). If the full height doesn't fit
  below but does above, above (its bottom at the anchor's top). If it fits on neither side, on the side with
  more room, shortened to fit. Then it shifts left as far as needed to stay on screen. The screen is the area
  above the status bar, as for every overlay.
- **Resize** while open: the menu keeps its anchor and is placed again, clamped to the screen.
- `PopupMenu.Framed` is false and `CloseOnClickOutside` is true. It counts as waiting for the user while open,
  as the dialogs do.

### 7.5 Select

- **Drawing:** the chosen option (or the placeholder in `TextMuted`), truncated with `…`, and `▼` in the last
  column, in `Input` / `InputFocused`, inside the optional block. `▼` is in WGL4, so conhost draws it.
- **Keys, closed:** Enter, Space, ↑, ↓, Alt+↓ and F4 open the menu. Other keys return false, so Tab moves focus
  and letters reach the bindings.
- **Open:** a `PopupMenu` of the options anchored to the field's area (the block, when there is one) with
  `MinWidth` set to its width, so it drops down below the field, as wide as it. Choosing an option sets
  `Selected` and raises `Changed` if it's a different option; dismissing changes nothing. `IsOpen` is true until
  the menu closes. With no options, opening does nothing.
- **Mouse:** a left press on the field opens it (click to focus runs first, §4.7). The wheel over a closed
  field does nothing, so scrolling a page never changes a choice.

### 7.6 Mouse

- **In the menu:** a left press on an enabled item chooses it. Presses on a separator, a disabled item or the
  border do nothing. The wheel moves the selection by one item. Releases are ignored, so the release of the
  right-click that opened a context menu doesn't choose anything.
- **Outside an overlay with `CloseOnClickOutside`:** a press of any button closes the top overlay and is
  consumed, not routed to the screen below, as in Windows and macOS menus. The wheel outside does nothing.
  Step 3 of §4.7 gains this case; overlays without the flag keep the 1.0 behavior (the press is dropped).
- **Context menus** are opened by the app: a component's `HandleMouse` answers a right press with
  `App.Overlays.ShowMenuAsync(items, new Rect(mouse.X, mouse.Y, 1, 1))`. Shift+F10, the Windows context menu
  key, is a binding the app adds; the Menu key isn't sent by most terminals.

### 7.7 Configuration

None.

### 7.8 Example

- **Form demo:** a **Plan** select (Free, Pro, Team, Enterprise) between Email and Notes, in the Tab order, with
  the placeholder "Choose a plan". The submit toast includes the plan.
- **Command Registry demo:** a context menu on the notes list: a right-click on a note (it selects the row
  first), or Shift+F10 on the selected note (anchored to its row), opens Delete (hint `x`), Delete all, a
  separator, and Help (hint `:help`). Delete and Delete all are disabled while the list is empty.

### 7.9 Tests and acceptance

Automated (all platforms):
- **Menu keys:** ↑ ↓ wrap and skip separators and disabled items; PgUp/PgDn/Home/End; Enter answers the index;
  Esc and Tab answer null; the first selection skips a disabled item; Enter with nothing enabled.
- **Type-ahead** with fake time: prefix matching, the same letter cycling, the 1 s reset, no match, case.
- **Placement:** below, above, the larger side when neither fits (shortened, scrolling), shifted left at the right
  edge, `MinWidth`, `MaxRows`, truncation, placed again after a resize.
- **Snapshots:** hints right-aligned, separators, disabled items, the selection, the scrollbar; the Select
  closed with the placeholder and with a value, focused and unfocused.
- **Mouse:** click an item, a disabled item, a separator, the border; wheel; a press outside closes and isn't
  routed to the screen; a release is ignored; `CloseOnClickOutside` false keeps the old behavior.
- **Overlay framing:** `Framed = false` gives `Render` the whole placement rect and draws no block.
- **Select:** every opening key, through `ComponentHarness`; choosing raises `Changed` once and sets `Selected`;
  choosing the same option and dismissing don't raise it; the menu sits under the field at its width;
  `Options` set with `Selected` out of range clears it; no options.
- **ShowMenuAsync** with no items answers null without showing anything.

Manual QA (results in the PR): both demos on Linux (tmux and Windows Terminal through WSL) and Windows (Windows
Terminal and conhost): open the Select with each key and the mouse, choose and dismiss; the menu near the bottom
of a short window opens above; right-click and Shift+F10 context menus; click outside to close; type-ahead.

### 7.10 Migration impact

- New public API (§7.2) in `PublicAPI.Unshipped.txt`. The two `Overlay` members default to the 1.0 behavior, so
  existing overlays don't change.
- README: `Select()` in the widget table, `SelectState` in the state table, a PopupMenu and context-menu
  snippet, and the overlay members in the overlays section.

### 7.11 Implementation order

One PR, in 1.3.0 with Phase 12:
1. `Overlay.Framed` and `CloseOnClickOutside`, in the frame drawing and the mouse router, with tests.
2. `MenuItem` and `PopupMenu`: keys, type-ahead, placement, drawing, mouse; `ShowMenuAsync`.
3. `SelectState` and `SelectBuilder`.
4. The Example, README, CHANGELOG, manual QA.

### 7.12 Out of scope

- Submenus, checkable and radio items, icons, and mnemonics (an underlined letter that chooses at once).
- A menu bar.
- Hover highlighting and drag-to-choose (no motion reports, §4.14).
- A filterable or editable select (a combo box you type into), and multi-select.
- Options other than strings (`SelectState<T>`); the app maps the index.
- Key hints in the status bar for the open menu.
- Detecting the Menu key.

### 7.13 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Select as a state plus builder, with the `App` passed to `HandleKey` (the pattern of every other widget), or a ready-made `Select` component that apps add as a child? | **State plus builder** (§7.2). |
| 2 | ↑ / ↓ on a closed Select: open the menu (Textual, macOS), or change the choice without opening it (Windows combo boxes)? | **Open the menu** (§7.5): a choice never changes without the list in view. |
| 3 | A press outside a menu: close it and consume the press, or close it and also deliver the press to what's below? | **Consume** (§7.6), as Windows and macOS menus do. |
| 4 | ↑ / ↓ at the ends of a menu: wrap around, or stop? | **Wrap** (§7.3), as Windows menus do. |
| 5 | Type-ahead: a prefix with a 1 s reset and same-letter cycling, or first letter only? | **Prefix with reset and cycling** (§7.3). |

---

## 8. Canvas and charts (Phase 14)

### 8.1 Purpose

`Sparkline` and `Gauge` are the only data widgets in 1.0, and neither can plot one value against another.
Phase 14 adds three widgets: `Canvas`, which draws points, lines, rectangles, circles and text in a
coordinate space of the app's choosing, at up to 2×4 dots per cell with braille; `Chart`, a line or scatter plot
of one or more datasets with axes, labels and a legend, drawn on a canvas; and `BarChart`, labelled bars,
vertical or horizontal, at eighth-cell resolution. All three are plain builders, like `Sparkline`: they hold no
state and handle no input.

### 8.2 Public API

```csharp
/// How a canvas turns dots into cells.
public enum Marker
{
    Braille,    // ⣿: 2×4 dots per cell (the default)
    HalfBlock,  // ▀ ▄ █: 1×2 dots per cell
    Block,      // █: one dot per cell
    Dot,        // •: one dot per cell
}

public static class Widget
{
    public static CanvasBuilder Canvas();
    public static ChartBuilder Chart();
    public static BarChartBuilder BarChart();
}

/// A drawing surface in app coordinates: x grows to the right, y grows upward (§8.3).
public sealed class CanvasBuilder
{
    public CanvasBuilder XBounds(double min, double max);   // default 0..1
    public CanvasBuilder YBounds(double min, double max);   // default 0..1
    public CanvasBuilder Marker(Marker marker);              // default Braille
    public CanvasBuilder Paint(Action<CanvasPainter> paint);
    public CanvasBuilder Block(BlockBuilder block);
    public CanvasBuilder Style(Style style);                 // patched over Theme.Text: the background, and dots with no style
    public void Draw(Buffer buf, Rect area);
}

/// Passed to CanvasBuilder.Paint. Shapes are drawn in call order.
public sealed class CanvasPainter
{
    public void Point(double x, double y, Style style = default);
    public void Points(IEnumerable<(double X, double Y)> points, Style style = default);
    public void Line(double x1, double y1, double x2, double y2, Style style = default);
    public void Rectangle(double x, double y, double width, double height, Style style = default);  // outline; (x, y) is the bottom-left
    public void Circle(double x, double y, double radius, Style style = default);                   // outline
    public void Print(double x, double y, Line text);    // text with its left end at the cell holding (x, y), above the dots

    /// The size of the dot grid: the inner area times the marker's dots per cell.
    public int DotWidth { get; }
    public int DotHeight { get; }
}

/// How a dataset is drawn.
public enum GraphType { Line, Scatter }

/// A series of (x, y) values for a Chart.
public sealed record Dataset(string Name, IReadOnlyList<(double X, double Y)> Data)
{
    public GraphType Type { get; init; } = GraphType.Line;
    public Marker Marker { get; init; } = Marker.Braille;
    public Style? Style { get; init; }    // null: Theme.ChartSeries[index of the dataset, wrapping around]
}

/// One axis of a Chart.
public sealed record Axis
{
    public string? Title { get; init; }
    public double? Min { get; init; }                  // null: the smallest value in the data
    public double? Max { get; init; }                  // null: the largest
    public IReadOnlyList<string>? Labels { get; init; } // null: three labels, Min, middle and Max, formatted with LabelFormat
    public string LabelFormat { get; init; } = "0.##"; // a .NET numeric format, invariant culture
}

/// Where a Chart draws its legend.
public enum LegendPosition { TopRight, TopLeft, BottomRight, BottomLeft, None }

/// A line or scatter plot of one or more datasets with axes (§8.4).
public sealed class ChartBuilder
{
    public ChartBuilder Datasets(IReadOnlyList<Dataset> datasets);
    public ChartBuilder Datasets(params Dataset[] datasets);
    public ChartBuilder XAxis(Axis axis);
    public ChartBuilder YAxis(Axis axis);
    public ChartBuilder Legend(LegendPosition position);   // default TopRight
    public ChartBuilder Block(BlockBuilder block);
    public ChartBuilder Style(Style style);                // patched over Theme.Text
    public void Draw(Buffer buf, Rect area);
}

/// A bar in a BarChart.
public sealed record Bar(string Label, double Value)
{
    public Style? Style { get; init; }    // null: Theme.GaugeFilled
    public string? ValueText { get; init; } // null: Value formatted with the chart's ValueFormat; "" hides it
}

/// Labelled bars, vertical (growing up) or horizontal (growing right) (§8.5).
public sealed class BarChartBuilder
{
    public BarChartBuilder Bars(IReadOnlyList<Bar> bars);
    public BarChartBuilder Bars(params Bar[] bars);
    public BarChartBuilder Direction(Direction direction);   // default Vertical
    public BarChartBuilder BarWidth(int width);              // default 3; vertical only
    public BarChartBuilder BarGap(int gap);                  // default 1
    public BarChartBuilder Max(double? max);                 // the value of a full bar; default the largest value
    public BarChartBuilder ValueFormat(string format);       // default "0.##"
    public BarChartBuilder Block(BlockBuilder block);
    public BarChartBuilder Style(Style style);               // patched over Theme.Text, for labels and values
    public void Draw(Buffer buf, Rect area);
}

public sealed record Theme
{
    /// The styles of chart datasets without their own, in order. Default: Cyan, Yellow, Magenta, Green, Blue, Red;
    /// Monochrome: plain text for all, so datasets differ by marker.
    public IReadOnlyList<Style> ChartSeries { get; init; }
}
```

- `Direction` is the existing layout enum. Axis lines use `Theme.Border`, axis labels `Theme.TextMuted`, axis
  titles and the legend `Theme.Text`.
- `Paint` runs during `Draw`, on the UI thread, every frame; the painter is only valid inside it.

### 8.3 Canvas

- **Dot grid:** the inner area times the marker's dots per cell (Braille 2×4, HalfBlock 1×2, Block and Dot 1×1).
  A point maps to dot column `round((x − xMin) / (xMax − xMin) × (DotWidth − 1))` and dot row
  `round((yMax − y) / (yMax − yMin) × (DotHeight − 1))`, so `xMin`/`yMax` is the top-left dot and
  `xMax`/`yMin` the bottom-right one. Points outside the bounds aren't drawn; NaN and infinite values are
  skipped. Bounds with `max <= min` draw nothing.
- **Shapes:** `Line` is Bresenham over dots, clipped to the grid, so a line that leaves the canvas draws the
  part inside. `Rectangle` is four lines. `Circle` is joined segments about a dot long, with the radius in
  coordinate units on both axes (a circle looks round only when the bounds match the area's aspect; the app
  chooses them).
- **Cells:** a cell shows the union of its dots (`⠁`…`⣿` from U+2800, `▀ ▄ █`, `█`, `•`), with the style of the
  last shape that set a dot in it; a cell has one foreground color, so where two colors share a cell the later
  shape wins. Cells with no dots keep the canvas style. `Print` text is drawn after all dots, on top, clipped to
  the inner area.
- **Basic glyphs** (conhost and other unknown Windows hosts, where widgets avoid non-WGL4 characters): braille
  isn't in WGL4, so `Marker.Braille` draws as `HalfBlock`. `▀ ▄ █ •` are in WGL4.

### 8.4 Chart

Layout of the inner area, from the outside in:
- **Y axis:** the labels, right-aligned in a column as wide as the widest, then `│`. Labels are spread evenly from
  the bottom row (the first) to the top row (the last). The Y title, if any, goes on the row above the plot,
  left-aligned over the axis.
- **X axis:** a `─` row with `└` at the corner, and a row of labels under it, spread evenly from the axis start
  (the first, left-aligned) to the right edge (the last, right-aligned), the others centered. The X title, if
  any, goes right-aligned on the row above the X axis, at the right end of the plot.
- **Plot:** the remaining area is a canvas with the axes' bounds. Each dataset is drawn in order, with its own
  marker and style: `Line` joins consecutive points, `Scatter` draws the points only. Points are taken in data
  order; the app sorts them if it wants a function plot.
- **Bounds:** an axis without `Min`/`Max` uses the extent of all datasets' finite values on that axis; if the
  extent is a single value v, it becomes v − 1 .. v + 1; with no data, 0 .. 1.
- **Legend:** a bordered box (`Theme.Border`) in the chosen corner of the plot, one row per dataset: a marker
  sample (`⣿`, or `█` in basic glyphs or for Block and HalfBlock, `•` for Dot) in the dataset's style, then its
  name. It's omitted when it would cover more than half the plot's width or height (Ratatui's quarter hides a
  two-dataset legend on a 15-row plot), or with `LegendPosition.None`, or when no dataset has a name.
- **Small areas:** the axes, labels and titles are dropped (titles first, then labels, then axis lines) until the
  plot is at least 1×1; an area too small for that draws nothing.

### 8.5 BarChart

- **Vertical:** bars `BarWidth` cells wide with `BarGap` between them, from the left. The bottom row holds the
  labels, centered under each bar and truncated with `…` to its width; the bars fill the rows above it, growing
  up in eighths (`▁▂▃▄▅▆▇█`; basic glyphs round to empty, `▄` or `█`, as `Sparkline` does). The value text is
  centered in the bar's bottom row, in the bar's color reversed, when it fits in `BarWidth`; otherwise it's
  omitted. Bars that don't fit across the area are not drawn (the first ones are kept).
- **Horizontal:** one row per bar with `BarGap` empty rows between; labels left-aligned in a column as wide as
  the widest, at most a third of the area, truncated with `…`; then a space and the bar growing right in eighths
  (`▏▎▍▌▋▊▉█`; basic: empty, `▌` or `█`), then a space and the value text when it fits. Bars that don't fit
  down the area are not drawn.
- **Values:** `Max` (or the largest value) is a full bar; values are clamped to 0 .. Max, and negative and NaN
  values draw as empty bars with their labels.

### 8.6 Configuration

None.

### 8.7 Example

A new **Charts** screen in the menu ("Line chart, bar charts and a canvas"), animated from an `Animation.Every`
loop like the dashboard:
- **Line chart** (top half): two live datasets, sin and cos of time with noise, over a sliding 10 s window, with
  axis titles, auto Y bounds and a legend; `m` cycles the marker (Braille, HalfBlock, Dot) for both datasets, and
  `s` toggles Line and Scatter.
- **Bar chart** (bottom left): requests per service as vertical bars, values changing; `d` toggles vertical and
  horizontal.
- **Canvas** (bottom right): a rotating line in a circle inside a rectangle, a few points, and a `Print` label.

### 8.8 Tests and acceptance

Automated (all platforms), mostly buffer snapshots:
- **Canvas mapping:** corners map to the corner dots; out-of-bounds, NaN and infinite points are skipped;
  `max <= min` draws nothing; each marker's dots per cell and glyphs; braille bit order (dot 1–8 to U+2800 bits);
  basic glyphs turn Braille into HalfBlock.
- **Shapes:** horizontal, vertical, diagonal and steep lines; a line clipped at each edge; rectangle; circle;
  the later shape's style wins in a shared cell; `Print` on top and clipped.
- **Chart:** auto bounds (several datasets, a single value, no data); explicit and default labels and their
  spreading; titles; Line and Scatter; the legend in each corner, omitted when too big and with None; series
  styles from the theme, wrapping; small areas drop parts in order and never throw.
- **BarChart:** vertical and horizontal eighths; basic-glyph rounding; labels truncated; value text shown and
  hidden (fit, `""`, custom); `Max`; negative and NaN; bars that don't fit are dropped; gap and width.
- **Theme:** `ChartSeries` in Default and Monochrome.
- No widget throws for any area size from 0×0 up, with or without a block (a property-style loop, as for the 1.0
  widgets).

Manual QA (results in the PR): the Charts screen on Linux (tmux and Windows Terminal through WSL) and Windows
(Windows Terminal, braille; conhost, the half-block fallback), each key, and a resize down to a small window.

### 8.9 Migration impact

- New public API (§8.2) in `PublicAPI.Unshipped.txt`. `Theme.ChartSeries` is a new init member with a default,
  so themes built with `with { … }` keep working; a theme built from `new Theme { … }` without it gets the
  default list.
- README: the three widgets in the widget table, a chart snippet and a canvas snippet; `ChartSeries` in the
  theme section.

### 8.10 Implementation order

One PR, in 1.4.0:
1. The dot grid and `CanvasBuilder`/`CanvasPainter`: mapping, markers, shapes, `Print`, basic glyphs.
2. `Theme.ChartSeries`, `Dataset`, `Axis` and `ChartBuilder` on the canvas: layout, bounds, labels, legend.
3. `Bar` and `BarChartBuilder`, both directions.
4. The Example, README, CHANGELOG, manual QA.

### 8.11 Out of scope

- Filled shapes, polygons, arcs, and the Ratatui world map.
- Canvas layers with per-layer blending, and dots of different colors in one cell.
- Bar charts with groups, stacked bars, negative bars below a baseline, and bar charts on a canvas.
- Logarithmic, time and category axes, tick marks, grid lines, and axis label formatting beyond `LabelFormat`.
- Chart interaction: zoom, pan, hover, a cursor, mouse input of any kind.
- Area charts and the Ratatui `Bar` graph type in `Chart`.
- Caching between frames: `Paint` and the chart layout run on every draw.

### 8.12 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Dataset colors: a new `Theme.ChartSeries` list, or a fixed palette in the widget (no theme change), or always the dataset's own style? | **`Theme.ChartSeries`** (§8.2): Monochrome can turn colors off, as it does everywhere else. |
| 2 | Axis bounds: required (Ratatui), or optional with auto bounds from the data? | **Optional** (§8.4): live data needs no bookkeeping in the app. |
| 3 | Canvas y direction: up (math and Ratatui) or down (screen)? | **Up** (§8.3). |
| 4 | Braille on conhost: fall back to half blocks, or draw `?`? | **Half blocks** (§8.3), as other widgets avoid non-WGL4 glyphs. |
| 5 | A shared cell with two colors: the later shape wins, or Ratatui-style layers? | **Later shape wins** (§8.3); layers are out of scope. |
| 6 | `BarChart` value text: inside the bar's bottom row (Ratatui) or above the bar? | **Inside** (§8.5), so values don't need a spare row. |

---

## 9. Debug overlay (Phase 15)

### 9.1 Purpose

When a layout is off by a cell, focus goes somewhere unexpected or a screen feels slow, the app author has
nothing to look at but the result. Phase 15 adds a debug overlay that the app draws on top of every frame: an
outline of each component's area, with the focus chain picked out, and a panel with the frame time, the cells and
bytes sent, the focus chain by name, the component under the pointer and the terminal's capabilities. It is off
until the app opts in to a toggle key (or sets an environment variable), it takes no input, and it costs nothing
when hidden beyond two timestamps per frame. The frame numbers are also public as `App.LastFrame`, for apps that
want them in their own status line or log.

### 9.2 Public API

```csharp
public partial class App
{
    /// Make key (default F12) toggle the debug overlay (§9.3). Off by default. The key is checked right after
    /// OnKey, before Ctrl+C, overlays and the focus chain, so it works while a dialog is open or a text field has
    /// focus. Null turns the key off again.
    public App WithDebugKey(string? key = "f12");

    /// True while the debug overlay is drawn. Set it from code, or with the debug key. Redraws on change.
    public bool ShowDebug { get; set; }

    /// Numbers about the last frame drawn (default before the first one).
    public FrameStats LastFrame { get; }
}

/// One frame's numbers (§9.4).
public readonly record struct FrameStats(
    long Frame,              // 1 for the first frame drawn by this run
    TimeSpan RenderTime,     // the Render callback or screen, overlays and toasts, without the debug overlay
    TimeSpan FlushTime,      // comparing the buffers and the backend writing the changes
    int CellsChanged,        // cells sent to the backend (every cell on a full redraw)
    long? BytesWritten,      // bytes written to the terminal; null on backends that don't write bytes (conhost, TestBackend)
    bool FullRedraw);
```

- `key` uses the `KeyMap` syntax (`"f12"`, `"ctrl+shift+d"`); an invalid key throws `FormatException`, as
  `KeyBinding.Parse` does.
- **Environment:** `MINITUI_DEBUG=1` at startup turns on the F12 key (unless the app chose another key) and sets
  `ShowDebug`, so an app can be inspected without changing it. Any other value is ignored.
- No theme changes: the overlay uses fixed colors (§9.3), so it looks the same whatever the app's theme.

### 9.3 What the overlay draws

Drawn by the app loop after the toasts, on the frame buffer, and recorded nowhere (it takes no input; clicks go
through it to what's underneath).

- **Outlines:** every rect the frame recorded for hit testing (§4.7): the top screen, each child rendered with
  `ctx.Render`, and each overlay, in render order. An outline patches the background of the rect's edge cells,
  keeping their symbols, so text stays readable: dark blue for most, dark magenta for the components on the
  focus chain, and yellow for the focused component (the last on the chain). A 1-row or 1-column rect is patched
  whole. With the monochrome theme (`NO_COLOR`), outlines are underlined and the focused one reversed.
- **Panel:** a bordered box, `Theme`-independent (white on dark gray), in the top-right corner of the screen, at
  most 44 columns wide and as tall as its lines; if the pointer is over it, it moves to the bottom-left so it
  doesn't hide what's being inspected. Lines, each truncated with `…`:
  1. `frame 1234  render 1.2 ms  flush 0.3 ms` (this frame's render time; the previous frame's flush time,
     since this frame's isn't known until it's drawn)
  2. `avg 1.0 ms  max 4.1 ms  fps 12` (render time over the last 60 frames; frames drawn in the last second)
  3. `cells 57  bytes 1.2 KB` (the previous frame; `bytes –` when unknown; `full` appended after a full redraw)
  4. `120×40  truecolor  braille  mouse` (size, `ColorMode`, `braille` or `basic` glyphs, and `mouse` when on)
  5. `focus: MenuScreen › FormScreen › TextInput` (type names of the focus chain from the top screen, or the
     top overlay when one is open; truncated from the left)
  6. `under pointer: TextInput (2, 5, 40×3)` (the topmost recorded rect under the last mouse position; omitted
     without mouse support)
  7. `screens 2  overlays 1  toasts 0`
- Debug drawing happens after `RenderTime` is measured, so it doesn't count toward it.

### 9.4 Measuring

- `RenderTime` and `FlushTime` use `App.Time` (the `TimeProvider`), so tests with fake time see exact values.
  Two timestamps per frame are taken whether or not the overlay is shown.
- `CellsChanged` is the count of updates passed to the backend. `BytesWritten` is the length of the escape
  sequences the ANSI backend wrote for the frame (cursor moves, styles, text and cursor show/hide).
- `LastFrame` is updated after the flush; the overlay drawn in frame N therefore shows frame N's render time and
  frame N − 1's flush numbers, which line 1 and 3 say.
- Showing the overlay changes cells every frame it's drawn (the numbers change), but it never asks for a frame:
  frames still happen only when something else makes the app dirty.

### 9.5 Configuration

`MINITUI_DEBUG=1` (§9.2).

### 9.6 Example

`WithDebugKey()` in the example app, and "F12 toggles the debug overlay" in the menu's footer.

### 9.7 Tests and acceptance

Automated (all platforms), through `AppHarness` and `TestBackend` with fake time:
- **Key:** F12 toggles `ShowDebug` and redraws; works with a dialog open and with a text field focused (the field
  doesn't get the key); a custom key; null turns it off; without `WithDebugKey`, F12 reaches the focus chain.
- **Environment:** `MINITUI_DEBUG=1` turns on the key and shows the overlay; other values don't (through an
  injected environment lookup, as `Glyphs.DetectBasic` does).
- **Stats:** `Frame` counts up; `RenderTime` and `FlushTime` with fake time advanced inside the render callback
  and the backend; `CellsChanged`, `FullRedraw`; `BytesWritten` from the ANSI backend on a fake console and null
  on `TestBackend`.
- **Outlines:** a screen with two children: edge cells' backgrounds patched with the three colors, symbols
  unchanged; 1-row rects; monochrome modifiers; overlays outlined.
- **Panel:** each line's text; truncation at narrow widths; moving to the bottom-left when the pointer is over
  it; nothing drawn on a terminal too small for the box (no throw at any size).
- **No input:** a click on the panel reaches the component below; the overlay never schedules a frame.

Manual QA (results in the PR): the example on Linux (tmux and Windows Terminal through WSL) and Windows (Windows
Terminal and conhost): F12 on several demos, with a dialog and a popup menu open, and with the mouse over
different components; the numbers move while the dashboard animates.

### 9.8 Migration impact

- New public API (§9.2) in `PublicAPI.Unshipped.txt`.
- README: a "Debugging" subsection under "The terminal" (the key, the environment variable, `LastFrame`).

### 9.9 Implementation order

One PR, in 1.4.0 with Phase 14:
1. Frame measuring in the loop and the terminal, `FrameStats`, `LastFrame`; bytes from the ANSI backend.
2. `ShowDebug`, `WithDebugKey`, `MINITUI_DEBUG`.
3. Outlines and the panel.
4. The Example, README, CHANGELOG, manual QA.

### 9.10 Out of scope

- Inspecting widgets drawn without a component (builders called directly in a `Render` method have no recorded
  rect); apps that want them outlined wrap them in a component.
- Clicking in the overlay to select a component, a tree view, or editing anything.
- Logging stats to a file, a frame-time graph, and memory or GC numbers.
- Showing the key event log (the Input demo already does).
- A debug overlay for apps that use the `Render` callback only (they have no components; the panel is still
  shown, with an empty focus chain).

### 9.11 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | The toggle key: opt-in with `WithDebugKey` (like `WithSuspendKey`), or F12 on by default? | **Opt-in** (§9.2): F12 stays free for apps. |
| 2 | `MINITUI_DEBUG=1` turns the overlay on without code changes? | **Yes** (§9.2). |
| 3 | Outlines: patch the edge cells' background (content stays readable), or draw box lines over them? | **Background** (§9.3). |
| 4 | Panel corner: top-right, moving away from the pointer, or a key to cycle corners? | **Top-right, moving to the bottom-left under the pointer** (§9.3). |
| 5 | Frame stats public (`App.LastFrame`) or internal to the overlay? | **Public** (§9.2), for apps' own status lines and logs. |

---

## 10. Text selection and clipboard (Phase 16)

### 10.1 Purpose

Text fields and text areas can type, move and delete, but not select: there's no way to replace a word without
deleting it a character at a time, to copy what was typed into another program, or to take back a mistake.
Phase 16 adds a selection to `TextFieldState` and `TextAreaState` (Shift with the movement keys, Ctrl+A, and
dragging with the mouse), copy and cut to the system clipboard (OSC 52 on Unix terminals, the Win32 clipboard on
Windows, and an in-app clipboard that always works), Ctrl+V pasting from it where the terminal doesn't take that
key itself, and undo and redo. Every widget built on these states (`TextField`, `TextArea` and `CommandLine`) gets
them without changes to the app.

### 10.2 Public API

```csharp
public class TextFieldState
{
    /// The other end of the selection, or null for none; the cursor is the end that moves. Setting Text, Cursor or
    /// typing without Shift clears it.
    public int? SelectionAnchor { get; }

    /// The selection as an ordered range (Start ≤ End) of UTF-16 indexes on cluster boundaries; empty when there's
    /// no selection (Start = End = Cursor).
    public int SelectionStart { get; }
    public int SelectionEnd { get; }
    public bool HasSelection { get; }
    public string SelectedText { get; }

    /// Select from anchor to cursor (both clamped and snapped, as Cursor is). Equal values clear the selection.
    public void Select(int anchor, int cursor);
    public void SelectAll();
    public void ClearSelection();

    /// Copy the selection to the Clipboard (§10.5). Returns false, and does nothing, without a selection.
    public bool Copy();
    /// Copy the selection and delete it. Returns false without a selection.
    public bool Cut();

    /// Undo or redo the last edit group (§10.6). Return false when there's nothing to undo or redo.
    public bool Undo();
    public bool Redo();
    public bool CanUndo { get; }
    public bool CanRedo { get; }

    /// A left press in the text moves the cursor there; a drag extends the selection; Shift+press extends it
    /// (§10.7). Uses the area from the last draw. Returns true if it did any of these.
    public bool HandleMouse(MouseEvent mouse);
}

public class TextAreaState
{
    public TextPosition? SelectionAnchor { get; }
    public TextPosition SelectionStart { get; }   // ordered: Start ≤ End
    public TextPosition SelectionEnd { get; }
    public bool HasSelection { get; }
    public string SelectedText { get; }           // lines joined by "\n"

    public void Select(TextPosition anchor, TextPosition cursor);
    public void SelectAll();
    public void ClearSelection();
    public bool Copy();
    public bool Cut();
    public bool Undo();
    public bool Redo();
    public bool CanUndo { get; }
    public bool CanRedo { get; }
    // HandleMouse (from Phase 12) also handles drags and Shift+press (§10.7).
}

/// The clipboard that copy, cut and Ctrl+V use (§10.5).
public static class Clipboard
{
    /// Put text on the system clipboard where the platform and terminal allow it, and on the in-app clipboard.
    public static void SetText(string text);

    /// The text Ctrl+V pastes: the system clipboard on Windows, otherwise the last text set in this process.
    /// Null if there's none.
    public static string? GetText();
}

public enum MouseAction
{
    // Press, Release, ScrollUp, ScrollDown, ScrollLeft, ScrollRight, as in 1.2, then:

    /// The pointer moved with a button held (§10.7). Button is the held button.
    Drag,
}
```

- `TextPosition` gets `IComparable<TextPosition>` and the comparison operators (by line, then index), which
  ordering a selection needs. That's additive.
- No theme changes: selected text is drawn with `Theme.Selection` while the widget is focused and
  `Theme.SelectionUnfocused` while it isn't (§10.8), the slots lists and tables already use.

### 10.3 Keys

Added to the §8.4 (text field) and §6.3 (text area) keys, in both states unless marked:

| Key | Effect |
|---|---|
| Shift + ← → Home End, Ctrl+Shift + ← → | Move as without Shift, extending the selection: the first one sets the anchor at the cursor. |
| Shift + ↑ ↓ PgUp PgDn, Ctrl+Shift + Home End | (Text area.) Likewise. |
| ← / → with a selection | Collapse it to its start / end, without moving further (as GUIs do). |
| Any other move without Shift | Clear the selection and move from the cursor. |
| Ctrl+A | Select all. |
| Typing, paste, Enter (text area) | Replace the selection. |
| Backspace, Delete, Ctrl+W, Ctrl+Backspace, Ctrl+U, Ctrl+K | With a selection, delete it (and nothing else); without one, as before. |
| Ctrl+C | Copy the selection. Without one it isn't handled, so it quits as before (§10.4). |
| Ctrl+X | Cut the selection. Without one it isn't handled. |
| Ctrl+V | Paste `Clipboard.GetText()`, replacing the selection. Handled even when the clipboard is empty. |
| Ctrl+Z / Ctrl+Y | Undo / redo (§10.6). Always handled, even with nothing to undo, so the key never leaks. |
| Esc | Clear the selection, keeping the cursor. Without one it isn't handled, so it still closes or goes back (added in 1.15, §20.5). In a command line, Esc without a selection clears the text, as before; in a text area with the find bar open, Esc closes the bar first. |

- **Where these keys arrive.** Shift with the arrows, Home and End is decoded since Phase 8 (`CSI 1;2D` and so
  on). Some terminals keep Shift+PgUp/PgDn (xterm, GNOME Terminal: scrollback) or Ctrl+Shift+arrows (Windows
  Terminal: scroll) for themselves; those keys then don't reach the app, and the others still work. Windows
  Terminal and most Linux terminals take Ctrl+Shift+C/V for their own copy and paste, which still work on
  whatever the terminal shows (with the mouse captured, after Shift+drag in most terminals).
- **Ctrl+V** reaches the app on most Unix terminals (they paste with Ctrl+Shift+V), so it pastes what the app
  copied. In Windows Terminal, Ctrl+V is the terminal's paste and arrives as a `PasteEvent`; in conhost, the
  input source already turns it into a `PasteEvent` from the clipboard (§4.5). Either way the state sees a
  paste, which replaces the selection the same way.
- **Ctrl+A** used to be unhandled in a text field and reach the bindings; Ctrl+X, Ctrl+V, Ctrl+Z and Ctrl+Y
  likewise. That's a "Changed" entry (§0 rule 2).

### 10.4 Ctrl+C

Ctrl+C quits unless `OnKey` takes it (1.0). That stays true, with one addition, between `OnKey` (and the debug
key) and quitting:

- If the focused component (the last on the focus chain, with an overlay's chain while one is open) has
  `CapturesText` true, Ctrl+C goes to **its** `HandleKey` only: not to its ancestors and not to any key map. If
  it returns true (a state with a selection copied it), that's the end of it; otherwise the app quits.
- So Ctrl+C copies when there's a selection in the focused field, and quits when there isn't, as Ctrl+C in
  Windows Terminal copies with a selection and interrupts without one. A component that doesn't capture text, or
  a binding, still can't block quitting.
- Render-callback apps (no components) are unchanged.

### 10.5 Clipboard

- **`SetText`** always keeps the text as the in-app clipboard (process-wide, shared by nested apps), and then:
  - **Windows** (any console, Windows Terminal or conhost): writes `CF_UNICODETEXT` with `OpenClipboard`,
    `EmptyClipboard` and `SetClipboardData`, retrying three times when the clipboard is locked, as
    `ReadClipboardText` does. Failures are ignored.
  - **Unix**, while an app is running on the ANSI backend: writes `ESC ] 52 ; c ; <base64 UTF-8> BEL` to the
    running app's terminal right away, under the backend's lock, so it never lands inside a frame. Terminals that don't support OSC 52 ignore it, as they must ignore any OSC.
    tmux passes it on only with `set -s set-clipboard on`; screen and some terminals limit its size. None of
    this can be detected, so the in-app clipboard is the fallback.
  - With no running app (a state used in a test or by a custom loop): the in-app clipboard only.
- **`GetText`:** on Windows, `ReadClipboardText()`, falling back to the in-app text if it fails; on Unix, the
  in-app text (reading the system clipboard through OSC 52 is disabled by most terminals and needs a reply
  parser; §10.12).
- Text is set as is (`\n` line breaks from a text area; Windows gets `\r\n`, as other Windows programs expect).
  Pasting it back goes through the usual paste rules.
- Tests replace the platform writer and reader through internal hooks, as with `GetEnvironment`.

### 10.6 Undo and redo

- Each state keeps an undo stack of snapshots (text, cursor and selection before the edit) and a redo stack. A
  text area's snapshot copies its list of lines (the strings are shared), not the text.
- **Edit groups.** Consecutive typed characters are one group, so Ctrl+Z takes back a word rather than a letter.
  A new group starts when: the kind of edit changes (typing, deleting backward, deleting forward, or a one-off:
  paste, cut, Enter, deleting a selection, Ctrl+W/U/K); the cursor moved, or the selection changed, since the
  last edit; or a typed non-space follows a typed space (so typing a sentence undoes by word).
- Undo restores the snapshot (text, cursor and selection), pushing the current state on the redo stack; redo
  does the reverse. A new edit clears the redo stack. Up to 100 groups are kept; the oldest is dropped.
- Setting `Text` or calling `Clear()` from code clears both stacks: it's a new value, not an edit.
- **Ctrl+Z and `WithSuspendKey`:** §5.5 already routes Ctrl+Z to the focus chain before suspending. Since a text
  state now always handles it, Ctrl+Z suspends only while focus isn't in a text field or area. The README says
  so next to `WithSuspendKey`; an app that wants Ctrl+Z to suspend everywhere can bind undo elsewhere by
  overriding `HandleKey`.

### 10.7 Mouse

- **Motion reports.** With `WithMouse`, Unix and VT consoles turn on `CSI ?1002h` (button-event tracking:
  presses, releases, the wheel, and motion while a button is held) instead of `?1000h`, and turn it off with
  `?1002l`. No `?1003` (all motion): hover is still out of scope. The SGR and X10 decoders turn a report with +32
  (motion) and button 0–2 into `MouseAction.Drag` with that button; motion with no button (3) is still dropped.
- **Windows records:** a `MOUSE_MOVED` record with a button held (`dwButtonState`) becomes a `Drag` with the
  lowest held button, left first; without a button it's still dropped.
- **Clamped, not dropped:** a drag outside the buffer (the pointer left the window while dragging, where the
  terminal reports it) is clamped to the buffer's edge, so a selection can reach the first and last rows. Other
  events outside it are still dropped.
- **Routing (pointer capture).** A drag goes to the component that was the target of the last press, then its
  ancestors, as long as it's still mounted under the current root; otherwise it's dropped. It doesn't go through
  click to focus. `App.OnEvent` and `App.OnMouse` see drags first, as they see every mouse event. Releases are
  routed as before (to what's under the pointer); since Phase 20 (§14.3) they go to the press target too.
- **In the states** (positions from the last draw, as in §6.6):
  - Left press: move the cursor there and clear the selection (Phase 12 behavior; new for text fields).
  - Shift+left press: extend the selection to there (anchor at the old cursor). Many terminals keep Shift+click
    for their own selection, so it often doesn't arrive.
  - Left drag: extend the selection from where the press put the cursor to the point. Above or below a text
    area's view, each drag report scrolls one row and selects to the first or last visible row; left or right of
    a text field (or a `WrapMode.None` text area), it scrolls one column. Terminals report only when the pointer
    moves, so scrolling happens as the mouse moves, not on a timer.
  - A drag that started outside the state's area (a press on the border, or on another component) isn't handled.
- Existing handlers ignore `Drag`: `IsClick` is false, and `SelectionState`, `ScrollState`, `TabBarState` and the
  dialogs handle only presses and the wheel. Apps with their own `OnMouse` start seeing drags ("Changed").

### 10.8 Drawing

- **Text field:** selected clusters are patched with `Theme.Selection` (focused) or `Theme.SelectionUnfocused`,
  then the cursor cell gets `Theme.Cursor` as before, so the cursor stays visible at the selection's moving end.
  Scrolling still follows the cursor.
- **Text area:** the same, row by row. A selected line break is shown as one selected cell after the line's last
  character when the row has room, so selecting across an empty line is visible.
- The selection is kept when focus moves away (shown unfocused) and when the text area scrolls.

### 10.9 Configuration

None. (OSC 52 can't be detected, and writing it to a terminal that doesn't support it is harmless, so there's no
switch for it.)

### 10.10 Example

- The Form demo: its footer lists Shift+arrows, Ctrl+A, Ctrl+C/X/V and Ctrl+Z/Y. The Notes text area and the
  fields above it get everything through the states.
- The menu footer gains nothing (Ctrl+C still says it quits).

### 10.11 Tests and acceptance

Automated (all platforms):
- **Selection keys** in both states: each Shift move sets the anchor once and extends; ←/→ collapse; other moves
  clear; Ctrl+A; typing, paste and Enter replace; each delete key deletes only the selection; selections over
  surrogate pairs, combining marks and wide characters never split a cluster; `Select` clamps and snaps; setting
  `Text`/`Cursor` clears.
- **Text area across lines:** Shift+↑↓ by wrapped row with the goal column, Shift+PgUp/PgDn, Ctrl+Shift+Home/End,
  `SelectedText` over several lines and over an empty line, cutting across lines joins them.
- **Clipboard:** `SetText` keeps the in-app text; the Windows writer and the OSC 52 writer are called through the
  hooks (the OSC sequence's exact bytes, base64 of UTF-8, and nothing written before `Enter` or after
  `Leave`); `GetText` on each platform path; `\r\n` for Windows (manual QA: the Win32 calls aren't run in CI).
- **Ctrl+C routing** (`AppHarness`): copies with a selection in a focused text field, quits without one; quits
  when the focused component doesn't capture text, even if it handles every key; a key map binding for ctrl+c
  never sees it; inside an overlay.
- **Undo:** typing a sentence undoes by word; each group boundary in §10.6; redo; a new edit clears redo; the 100
  limit; `Text =` clears history; undo restores the cursor and selection; Ctrl+Z with an empty stack is handled
  and doesn't suspend; Ctrl+Z with focus on a list still suspends.
- **Mouse decoding:** SGR and X10 drags (each button, modifiers), motion without a button dropped, `?1002h` and
  `?1002l` in the mode strings and every restore path; Windows `MOUSE_MOVED` with and without buttons; clamping.
- **Mouse routing:** drags go to the pressed target even when the pointer leaves it; a target unmounted since the
  press drops them; click to focus isn't run for drags.
- **Mouse in the states:** press, Shift+press, drag in both directions, drag past the edges scrolls, drag from
  the border ignored; text field click placement with a scrolled field and wide characters.
- **Snapshots:** selected text focused and unfocused, the cursor at either end, a selected line break, monochrome.

Manual QA (results in the PR): the Form demo on Linux (tmux with `set-clipboard on`, and Windows Terminal through
WSL) and Windows (Windows Terminal and conhost): select with every key in §10.3, copy and paste into another
program and back, cut, undo and redo a paragraph, drag across wrapped rows and past the top and bottom, Ctrl+C
with and without a selection; emoji and CJK text.

### 10.12 Out of scope

- Selecting text anywhere else: paragraphs, lists, tables, the log view, or the whole screen (terminals' own
  Shift+drag selection still does that).
- Reading the system clipboard on Unix (OSC 52 queries), and primary selection (X11 middle-click paste).
- Double- and triple-click to select a word or line: terminals don't report them, and timing presses is left to
  apps. Windows' `DOUBLE_CLICK` stays a press.
- Hover and all-motion reports (`?1003`); dragging scrollbars, splitters or list items (apps can use `Drag` for
  those now).
- Ctrl+Insert, Shift+Insert and Shift+Delete as copy, paste and cut.
- Persistent or unlimited undo, undo across `Text =`, and an undo API for app-made edits (`Insert`, `Paste` from
  code are undoable groups; there's no way to group several calls into one).
- A selection or undo in `SelectState`'s type-ahead and `PopupMenu`.

### 10.13 Implementation order

Two PRs, like Phase 10, since the mouse part changes input decoding on both platforms and needs hand testing of its
own:
1. **16a** (keys): the selection model, keys and drawing in both states; `Clipboard` with the Windows and OSC 52
   writers; the Ctrl+C step; undo and redo; the Example footer, README, CHANGELOG, manual QA.
2. **16b** (mouse): `?1002`, `MouseAction.Drag` in the SGR, X10 and Windows decoders; clamping; pointer capture
   in the router; press, Shift+press and drag in both states; README, CHANGELOG, manual QA.

Both go out in one release (1.5.0).

### 10.14 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | The copy key: Ctrl+C when the focused field has a selection (quitting otherwise), or a separate key such as Alt+C (Ctrl+Shift+C can't be told from Ctrl+C in most terminals, and terminals keep it)? | **Ctrl+C with a selection** (§10.4), as Windows Terminal does. |
| 2 | Undo and redo in this phase? A text state then always takes Ctrl+Z, so `WithSuspendKey` suspends only while focus isn't on text. | **Yes, on Ctrl+Z / Ctrl+Y** (§10.6). |
| 3 | Ctrl+A: select all (GUIs, Textual's `TextArea`) or start of line (readline, Emacs)? Home already moves to the start. | **Select all.** |
| 4 | System clipboard on Windows: the Win32 API in every console, or OSC 52 in Windows Terminal and Win32 only in conhost? | **Win32 everywhere** (§10.5): one path, no size limit, works in both. |
| 5 | Mouse drag in this phase (it needs `?1002` and pointer capture), or keys only, with the mouse later? | **In this phase, as PR 16b** (§10.13). |
| 6 | The selection when focus leaves: keep it, shown with `SelectionUnfocused` (GUIs), or clear it? | **Keep it** (§10.8). |

---

## 11. Tree view (Phase 17)

### 11.1 Purpose

MiniTui has lists and tables but no way to show a hierarchy: a file browser, a project outline, a JSON document
or a settings tree has to be flattened by the app, which then writes its own expand and collapse logic,
indentation and keys. Phase 17 adds `Widget.Tree()` and `TreeState<T>`: a scrollable tree over the app's own
objects, with nodes that expand and collapse, the keys of tree views in file managers and GUIs, type-ahead,
clicking and the wheel, optional guide lines, and children that are read only when a node is expanded, so a tree
over a large or slow source (a disk, an API) costs only what's open.

### 11.2 Public API

```csharp
/// The nodes, expanded set, selection and scroll offset of a tree (§11.4), drawn with Widget.Tree. T is the
/// app's own node type; the state asks for children instead of holding a copy of the tree.
public sealed class TreeState<T> where T : class
{
    /// children: a node's children, asked for only while the node is expanded (§11.4).
    /// text: a node's label, drawn and used by type-ahead.
    /// comparer: how nodes are matched, for the expanded set and the selection (default: EqualityComparer<T>.Default).
    public TreeState(Func<T, IReadOnlyList<T>> children, Func<T, string> text, IEqualityComparer<T>? comparer = null);

    /// The top-level nodes. Setting them keeps the expanded set and the selection where they still match.
    public IReadOnlyList<T> Roots { get; set; }

    /// Whether a node can expand, without reading its children; drawn as an expander. Default: children(node)
    /// isn't empty. Set it when reading children is slow, or to show an expander before they're loaded.
    public Func<T, bool>? HasChildren { get; set; }

    /// The selected node, or null for none (the first row is selected at the next draw or key). Set from code, it
    /// doesn't raise SelectionChanged and doesn't expand anything.
    public T? Selected { get; set; }

    /// The first visible row. Adjusted when drawn so Selected is visible.
    public int Offset { get; set; }

    /// How many rows were visible at the last draw. Used by PgUp and PgDn.
    public int ViewportHeight { get; }

    /// The rows the tree shows: every root, and the children of each expanded node, depth first (§11.4).
    public IReadOnlyList<TreeRow<T>> Rows { get; }

    public bool IsExpanded(T node);
    /// Expand, collapse or toggle one node. Expand does nothing for a node without children; Collapse moves the
    /// selection to the node if it was inside it. Each raises Expanded or Collapsed when it changes something.
    public void Expand(T node);
    public void Collapse(T node);
    public void Toggle(T node);
    /// Expand every node that has children, reading the whole tree (§11.4), or collapse every node.
    public void ExpandAll();
    public void CollapseAll();

    /// Raised on the UI thread when the user moves the selection (not when code sets it).
    public event EventHandler? SelectionChanged;
    /// Raised when Enter is pressed on a node without children (§11.3).
    public event EventHandler<TreeNodeEventArgs<T>>? Activated;
    /// Raised when a node expands or collapses, from a key, a click or code. Lazy loaders start loading here.
    public event EventHandler<TreeNodeEventArgs<T>>? Expanded;
    public event EventHandler<TreeNodeEventArgs<T>>? Collapsed;

    /// The keys in §11.3. Returns true if the key was one of them.
    public bool HandleKey(ConsoleKeyInfo key);

    /// The wheel moves the selection; a left press on a row selects it, and on its expander also toggles it
    /// (§11.6). Uses the area from the last draw. Returns true if it did any of these.
    public bool HandleMouse(MouseEvent mouse);
}

/// One visible row of a tree.
public readonly record struct TreeRow<T>(T Node, T? Parent, int Depth, bool HasChildren, bool IsExpanded)
    where T : class;

public sealed class TreeNodeEventArgs<T>(T node) : EventArgs where T : class
{
    public T Node { get; }
}

public static class Widget
{
    public static TreeBuilder<T> Tree<T>(TreeState<T> state) where T : class;
}

/// A bordered, scrollable tree (§11.5).
public class TreeBuilder<T> where T : class
{
    /// How a node is drawn. Default: its text from the state, in the theme's text style.
    public TreeBuilder<T> Format(Func<T, Line> format);
    /// Guide lines (│ ├─ └─) from each node to its parent, instead of indentation only. Default: off.
    public TreeBuilder<T> Guides(bool guides = true);
    /// Columns per level without guides (default 2, at least 1). Guides always take 2.
    public TreeBuilder<T> Indent(int columns);

    // As ListBuilder:
    public TreeBuilder<T> Focused(bool focused = true);
    public TreeBuilder<T> Title(string title);
    public TreeBuilder<T> ShowScrollInfo(bool show = true);
    public TreeBuilder<T> HighlightSymbol(string symbol);
    public TreeBuilder<T> Block(BlockBuilder block);
    public TreeBuilder<T> Style(Style style);
    public TreeBuilder<T> HighlightStyle(Style style);
    public void Draw(Buffer buf, Rect area);
}
```

- The state holds the tree's view, and the app holds the tree. This is the shape of `ListBuilder.Items<T>`
  (the app's list, the widget's formatting) and of Textual's and Terminal.Gui's tree data providers. An app with
  no node type of its own uses a small record, such as `record Node(string Name, List<Node> Children)`.
- `where T : class` keeps `Selected` and `Parent` nullable without a `HasSelection` flag. Value-type nodes
  (an index into an array) can be boxed in a record.
- No theme changes: the selection uses `Theme.Selection` and `Theme.SelectionUnfocused`, and the expanders and
  guides use `Theme.TextMuted`.

### 11.3 Keys

| Key | Effect |
|---|---|
| ↑ / ↓ | The previous or next row, stopping at the first or last. |
| PgUp / PgDn | Up or down by the visible rows. |
| Home / End | The first or last row. |
| → | On a collapsed node with children: expand it. On an expanded node: move to its first child. On a leaf: nothing (handled). |
| ← | On an expanded node: collapse it. Otherwise: move to its parent. On a collapsed root: nothing (handled). |
| Enter | On a node with children: toggle it. On a leaf: raise `Activated`. |
| A printable character | Type-ahead (below). |

- **Type-ahead** works as in `PopupMenu` (§7.3): characters typed without Ctrl or Alt build a search string
  cleared 1 s after the last one, and the selection moves to the next row, starting at the selected one and
  wrapping around, whose text starts with it (ordinal, ignoring case). The same character again moves to the next
  match. It searches the rows (the expanded part of the tree) and never expands anything. No match leaves the
  selection where it is, and the key is still handled.
- So, unlike `SelectionState`, `j` and `k` don't move: they're type-ahead, as in file manager trees and Windows'
  tree view. Since a focused component gets keys before bindings (§9.4), plain-letter bindings (`q` for back)
  don't fire while a tree has focus; the example and README bind tree commands with Alt. Space, Tab, Esc and keys with Ctrl or Alt aren't handled, so they reach the screen's bindings and
  focus.
- Each user move raises `SelectionChanged` once; moving to the row already selected (↑ on the first row)
  doesn't.

### 11.4 Rows and the expanded set

- **Rows** are computed by walking the roots and, depth first, the children of each expanded node. Children are
  read with `children(node)` only for expanded nodes, and `HasChildren` only for rows. They're recomputed at each
  draw and at each key and mouse event, so a tree whose source changed (a loader added children, a file was
  deleted) shows it at the next frame without a refresh call. The cost is linear in the visible part of the tree
  (the expanded nodes, not the screen). That's fine for thousands of open nodes, and §11.10 leaves caching out.
- **The expanded set** is a `HashSet<T>` with the state's comparer. Nodes that leave the tree stay in it, so
  removing and re-adding a node (a rebuilt model with equal records, or a comparer by path) keeps it expanded.
  `Roots =` doesn't clear it; `CollapseAll` does.
- **Cycles:** a node that equals one of its own ancestors on the walk is shown as a leaf and isn't walked again,
  so a cyclic source (symlinked directories, a graph) can't hang the walk or `ExpandAll`. `ExpandAll` still reads
  every reachable node, which on a disk or an API can be slow. The README says so.
- **Keeping the selection:** after the rows are computed, if `Selected` isn't among them, the selection moves to
  its nearest ancestor that is (remembered from the row's parents at the last walk), or else the row at its old
  index, clamped. So collapsing a node, from code or with ←, selects the node, and deleting the selected node
  selects its parent or neighbour. This doesn't raise `SelectionChanged`. With no rows, `Selected` is null.
- `Offset` is fitted as a list's is (`SelectionState.FitOffset`), so the selected row stays visible with as
  little scrolling as possible. Expanding a node near the bottom doesn't scroll on its own.

### 11.5 Drawing

- **Block, title and scrollbar** as in `ListBuilder`: a single-line border by default (focused styles with
  `Focused`), the title with `[n]` or `[a-b of n]` counting rows, and a scrollbar on the right border when the
  rows overflow.
- **Each row:** the indentation, then the expander, then a space, then the node (`Format`, or its text). It's
  truncated at the right edge as list items are. There's no horizontal scrolling.
  - The expander is `▾` (expanded) or `▸` (collapsed) for a node with children and a space for a leaf, so labels
    at one depth line up. In basic glyph mode (conhost and similar, `Glyphs.Basic`) they're `-` and `+`.
  - Without guides, the indentation is `Depth × Indent` spaces.
  - With `Guides()`, a row at depth d draws, for each level from 1 to d − 1, `│ ` if a later row continues that
    level (the ancestor at that depth has a sibling below it) or two spaces, then `├─`, or `└─` for a last child.
    Roots draw no connector, so a parent's children hang from its expander. It reads like the `tree` command:
    ```
    ▾ src
    ├─▾ MiniTui
    │ ├─  App.cs
    │ └─▸ Widgets
    └─▸ MiniTui.Example
    ▸ tests
    ```
    The exact column layout is settled by the snapshot tests. Box-drawing characters are used in every glyph
    mode, as borders are.
- **Selection:** the selected row's full width gets `Theme.Selection` (focused) or `Theme.SelectionUnfocused`,
  patched with `HighlightStyle`, after the row is drawn. `HighlightSymbol` is drawn before the indentation, and
  other rows are indented to match. The expander and guides use `Theme.TextMuted` under the highlight.

### 11.6 Mouse

- The wheel moves the selection by one row, as in a list (§4.8).
- A left press on a row selects it. On the row's expander cell, it also toggles the node. Clicking a selected
  leaf doesn't raise `Activated`: terminals don't report double clicks (§10.12), and activating on a single
  click would make selecting with the mouse impossible. Apps that want to open on click do it in their own
  `HandleMouse`.
- Positions come from the last draw (`Track`, as `SelectionState` does). Drags aren't handled.
- Each change raises the same events as the keys.

### 11.7 Lazy and async children

There's no async API: `children` is synchronous, because it runs inside a frame. A slow source is loaded by the
app:
1. `HasChildren` answers from what's known without loading (a directory, a node with a `HasMore` flag).
2. `Expanded` starts the load (`Task.Run`, an HTTP call). Until it finishes, `children` returns what's loaded so
   far, or one placeholder node ("Loading…") of the app's own type.
3. When the load finishes on the UI thread (MiniTui's synchronization context), the app stores the children and
   the next frame shows them.

The README shows this with a directory tree, and the example (§11.8) uses it.

### 11.8 Example

A "Tree" demo after "Tabs" in the menu: the file system from the current directory.
- Directories expand lazily, with `HasChildren` true for any directory and the children read on `Expanded`.
  Unreadable directories show one "(access denied)" leaf.
- A detail pane on the right shows the selected path, its size and its modified time.
- Enter on a file shows a toast with its path (`Activated`). Alt+G toggles guides, Alt+E and Alt+C expand and
  collapse everything under the selected node (`ExpandAll`/`CollapseAll` are whole-tree, so the demo walks the subtree
  itself), and the footer lists the keys.

### 11.9 Tests and acceptance

Automated (all platforms):
- **Rows:** roots only, after expanding at several depths, after collapsing a parent with expanded children
  (they stay expanded inside it), `Parent`/`Depth`, `children` not called for collapsed nodes (a counting
  function), `HasChildren` used when set, cycles shown as leaves, `ExpandAll` on a cycle terminates.
- **Expanded set and comparer:** a rebuilt model of equal records keeps its expanded nodes; a comparer by key;
  `Roots =` keeps it; `CollapseAll` clears it; `Expand` on a leaf does nothing; each event once per change.
- **Selection:** null selects the first row; collapsing an ancestor selects it; removing the selected node
  selects its parent, then the old index; the empty tree; `SelectionChanged` only for user moves.
- **Keys:** every row in §11.3, at the first and last rows and on roots and leaves; Enter toggling versus
  `Activated`; type-ahead (prefix, repeated character, wrap, no match, reset after 1 s with fake time, never
  expanding); keys that aren't handled.
- **Mouse:** wheel, row press, expander press, a press below the last row or on the border, after scrolling.
- **Snapshots:** indentation only and guides (middle and last children, deep trees, a root with no children),
  expanders in Unicode and basic glyph mode, the selection focused and unfocused with a highlight symbol,
  truncation, the scroll info and scrollbar, monochrome.

Manual QA (results in the PR): the Tree demo on Linux (tmux) and Windows (Windows Terminal and conhost). Browse
the repository with every key and the mouse; check the guides and glyphs in conhost's basic mode; open a large
directory (`/usr/lib` or `C:\Windows\System32`) to check speed; check an access-denied directory.

### 11.10 Out of scope

- Multi-select, checkboxes in the tree, and tri-state parents.
- Drag and drop, renaming or editing nodes in place, and sorting (the app orders `children`).
- A filter box, or search that finds collapsed nodes and expands to them. Type-ahead sees only the rows.
- Revealing a node by path (expanding its ancestors and selecting it). The state has no parent function. Apps
  can `Expand` each ancestor themselves and set `Selected`.
- A tree table (columns beside the tree), icons, and per-node expander glyphs.
- Horizontal scrolling for deep trees.
- An async `children` and a built-in loading indicator (§11.7).
- Caching rows between frames.
- `*` (expand the subtree) and `-`/`+` keys, as in Windows' tree view.

### 11.11 Implementation order

One PR (17), released as 1.6.0:
1. `TreeState<T>`, `TreeRow<T>` and `TreeNodeEventArgs<T>`: rows, the expanded set, selection keeping and
   events, with their tests.
2. Keys, type-ahead and the mouse.
3. `TreeBuilder<T>` and `Widget.Tree`: drawing, guides, glyph modes and snapshots.
4. The example demo, README ("### Trees", with the lazy-loading example), CHANGELOG, TODO, manual QA.

### 11.12 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | The model: a generic `TreeState<T>` over the app's own objects with a `children` function, or a concrete `TreeNode` class (text, children list, expanded flag, `Data`) that the app builds? | **Generic over the app's objects** (§11.2). Nothing is copied, laziness is natural, and an app with no model uses a small record. |
| 2 | Enter: toggle a parent and activate a leaf, or always raise `Activated` (and expand only with → / click)? | **Toggle a parent, activate a leaf** (§11.3), as file managers do. |
| 3 | Letters: type-ahead (`PopupMenu`, Windows tree view), or vim keys `h j k l` (`SelectionState`'s `j`/`k`)? | **Type-ahead** (§11.3). ← → ↑ ↓ already cover movement, and trees are searched by name. |
| 4 | Guide lines on or off by default? | **Off**, with `Guides()` (§11.5), as Ratatui's tree widgets. Textual draws them by default. |

---

## 12. Terminal queries and richer keyboard input (Phase 18)

### 12.1 Purpose

The legacy keyboard encoding that every terminal speaks can't tell Ctrl+Enter from Enter, Shift+Enter from
Enter, Ctrl+I from Tab or Ctrl+Tab from Tab, so a text area has no natural submit key and many familiar shortcuts
can't be bound. Modern terminals offer better encodings: the kitty keyboard protocol (kitty, WezTerm, foot,
Ghostty, Alacritty, iTerm2 and others), xterm's `modifyOtherKeys`, and Windows Terminal's `win32-input-mode`. A
terminal that doesn't speak one of them must not receive it, so MiniTui first has to ask the terminal what it
supports. Phase 18 adds those questions (backlog ranks 1 and 6, §1.1):
- **Terminal queries:** asked at startup, with the replies parsed by the input decoder and never leaked as keys.
  They report the background and foreground colors, the keyboard protocols the terminal supports, and its name
  and version.
- **Light and dark themes:** new `Theme.Dark` and `Theme.Light` with their own colors, and `WithTheme(dark,
  light)` to pick one from the background color.
- **Richer keyboard input:** the best protocol the terminal supports is turned on, so keys the legacy encoding
  can't express (Ctrl+Enter, Shift+Enter, Ctrl+Tab, Ctrl+I, Ctrl+digits and punctuation) arrive as themselves.
  Every key the legacy encoding can express still decodes exactly as before.

### 12.2 Public API

```csharp
/// What the terminal said about itself in reply to the startup queries (§12.3). Unknown values are null.
public sealed record TerminalInfo
{
    /// True once the terminal answered (its Primary Device Attributes reply arrived). False before that, with
    /// queries turned off, on the legacy Windows console backend, and when it never answered.
    public bool Answered { get; init; }

    /// The default background and foreground colors (OSC 11 and OSC 10 replies) as RGB colors.
    public Color? Background { get; init; }
    public Color? Foreground { get; init; }

    /// Whether the background is dark: from Background's luminance, or else COLORFGBG; null if neither is known.
    public bool? IsDark { get; init; }

    /// The keyboard protocol in use (§12.5). Legacy until one is detected and turned on.
    public KeyboardProtocol Keyboard { get; init; }

    /// The terminal's name and version from its XTVERSION reply, such as "WezTerm 20240203-110809-5046fc22".
    public string? Name { get; init; }
}

public enum KeyboardProtocol
{
    /// The classic encoding: keys such as Ctrl+Enter arrive as plain Enter.
    Legacy,
    /// The kitty keyboard protocol, with the "disambiguate escape codes" flag.
    Kitty,
    /// xterm's modifyOtherKeys, level 2.
    ModifyOtherKeys,
    /// Windows Terminal's win32-input-mode (?9001).
    Win32InputMode,
}

/// Raised (through OnEvent) when TerminalInfo changes because a reply arrived.
public sealed record TerminalInfoEvent(TerminalInfo Info) : Event;

public partial class App
{
    /// What the terminal has said so far. Updated on the UI thread as replies arrive.
    public TerminalInfo TerminalInfo { get; }

    /// Ask the terminal about itself at startup (§12.3). On by default.
    public App WithTerminalQueries(bool enabled = true);

    /// Turn on the best keyboard protocol the terminal supports (§12.5). On by default; it needs the queries,
    /// except win32-input-mode on Windows.
    public App WithKeyboardProtocol(bool enabled = true);

    /// Use `dark` on a dark background and `light` on a light one (§12.4). `dark` is used until the background
    /// is known, and when it never is.
    public App WithTheme(Theme dark, Theme light);
}

public partial class Theme
{
    /// Themes with their own RGB colors, the same in every terminal whatever its palette, including the text and
    /// background colors; the app fills the screen with the background (§12.4).
    public static Theme Dark { get; }
    public static Theme Light { get; }
}
```

- `WithTheme(Theme)` from 1.0 is unchanged and replaces the pair if called after it (last call wins, as for
  the other `With` options).
- `KeyBinding.Parse` needs no new names: `ctrl+enter`, `shift+enter`, `ctrl+tab`, `ctrl+i`, `ctrl+1` and
  `ctrl+.` already parse. Until now they could only match when a terminal sent them by accident; now they match
  in terminals that support a protocol.

### 12.3 Queries and replies

- **When:** once, right after the terminal is entered (the alternate screen and raw mode), and again after a
  resume from suspend (§5). Each time, the backend writes, in one write and under its lock:
  `CSI ? u` (the kitty keyboard flags), `CSI ? 4 m` (XTQMODKEYS: the modifyOtherKeys level), `OSC 10 ; ? ST`,
  `OSC 11 ; ? ST`, `CSI > 0 q` (XTVERSION), and last `CSI c` (Primary Device Attributes, DA1).
- **DA1 is the sentinel.** Every terminal answers DA1, and terminals answer in order, so when the DA1 reply
  arrives, any query not answered before it never will be. `Answered` turns true then. If DA1 hasn't arrived 1 s
  after the queries were sent (a terminal behind a slow link, or one that doesn't answer at all), MiniTui stops
  waiting. Replies that arrive later are still recognized and applied.
- **The first frame isn't delayed,** except with `WithTheme(dark, light)`: then the loop waits up to 150 ms for
  the DA1 reply (or the OSC 11 one) before drawing, so a fast terminal doesn't flash the wrong theme. A slower
  reply switches the theme and redraws when it arrives.
- **Decoding:** the decoder already reads OSC and DCS strings and private CSI forms whole, and drops them
  (§2.4). It now turns these into internal replies instead of dropping them. Anything else is still dropped:
  - `CSI ? flags u`: the kitty protocol is supported.
  - `CSI > 4 ; value m`: the XTQMODKEYS reply, so modifyOtherKeys is supported.
  - `OSC 10/11 ; rgb:R/G/B` (1–4 hex digits per channel), ended by BEL or ST. The 16-bit form is used by xterm,
    VTE and most others.
  - `DCS > | text ST`: the XTVERSION reply.
  - `CSI ? … c`: the DA1 reply.
- **Never as keys:** a reply split across reads is reassembled like any sequence, and a reply arriving at any
  time (even after the 1 s cut-off) is consumed, never decoded as Esc and characters. That's the guarantee that
  matters most: a query reply typed into a text field is the classic failure of terminal queries.
- **Where queries go:**
  - Linux and macOS: to the terminal on the ANSI backend.
  - Windows: to the console on the VT backend (Windows Terminal, and conhost in VT mode, which answers DA1
    itself).
  - Not on the legacy console backend, nor with `WithBackend` (a test backend or a custom one), where `Answered`
    stays false.
  - Tests drive replies through the decoder and a fake clock.
- **Without queries** (`WithTerminalQueries(false)`): no protocol is detected, so the keyboard stays legacy,
  except win32-input-mode (§12.5). `IsDark` comes only from `COLORFGBG`.

### 12.4 Light and dark

- **`IsDark`:** from `Background`'s relative luminance (sRGB, Rec. 709 weights), dark below 0.5. Without a
  background reply, it comes from `COLORFGBG` (`fg;bg`, set by rxvt, Konsole and others): a bg of 0–6 or 8 is
  dark, and 7 or 9–15 is light. Otherwise it's null.
- **`WithTheme(dark, light)`** uses `light` when `IsDark` is false, and `dark` otherwise (true or unknown). The
  theme is chosen once per run, when the background is first known, not on every reply.
- **Three kinds of theme** (decided in 18a QA: a palette-based light theme looked different in every light
  profile, because light profiles redefine the 16 colors heavily):
  - `Theme.Default` uses the terminal's palette (the 16 named colors) and its default text and background, so it
    fits the user's profile. Unchanged.
  - `Theme.Dark` and `Theme.Light` use their own RGB colors in every slot, including `Text`'s foreground and
    background, so they look the same in every terminal. The usual pair is `WithTheme(Theme.Dark, Theme.Light)`.
  - `Theme.Monochrome` uses only modifiers. Unchanged.
- **Filling the background:** when the theme's `Text` has a background, each frame starts by filling the screen
  with `Text`, and `WidgetText.Clear` (used by dialogs, toasts, buttons, the status bar and the command line)
  clears with `Text` instead of empty cells. Text drawn over it patches its style, so it keeps the background.
  With `Theme.Default`, whose `Text` has no colors, nothing changes.
- **The colors:**
  - Dark: light gray text on a near-black blue background, a blue accent and focus, a yellow focused title, and
    green, yellow, red, cyan and magenta accents.
  - Light: near-black text on off-white, a dark blue accent, focus and selection (white text), light gray bars,
    and dark accents.
  - Text has a contrast of at least 7:1, selections, bars and buttons at least 4.5:1, and accents and chart
    series at least 4:1 against the background. Tests check these.
  - Terminals with 256 colors get the nearest palette entry; with 16, the nearest named color.
- `NO_COLOR` selects Monochrome in place of Default, Dark and Light.

### 12.5 Keyboard protocols

**Choosing one** (with `WithKeyboardProtocol()` on, the default):
1. If the kitty flags reply arrived, push the kitty flags: `CSI > 1 u` ("disambiguate escape codes" only: no
   key release events, alternate keys or text reports, since `ConsoleKeyInfo` can't carry them).
2. Else, if the XTQMODKEYS reply arrived, turn on `modifyOtherKeys` level 2: `CSI > 4 ; 2 m`.
3. On Windows with VT input: `CSI ? 9001 h` (win32-input-mode) as soon as the input mode is set, without waiting
   for a reply. Windows Terminal has no query for it. Consoles that don't support it ignore the private mode,
   and the decoder accepts both encodings. `Keyboard` says `Win32InputMode` once the first such sequence
   arrives, on Windows only (on other platforms a stray win32-input-mode sequence still decodes, but doesn't change
   `Keyboard`).
4. Otherwise, stay legacy.

A protocol is never turned on without a reply that shows the terminal knows it, except the Windows private mode.
Some terminals render unknown `CSI > … m` as SGR (underlined text), which is why modifyOtherKeys waits for
XTQMODKEYS.

**Turning it off:** `CSI < u` (pop the kitty flags), `CSI > 4 ; 0 m`, or `CSI ? 9001 l`, written on every path
that restores the terminal today: leaving, suspend, the exit backstop, and crash restore (§5.4 and the Phase 8
restore paths). After a suspend, the queries are asked again and the protocol is re-enabled from the new
replies, since the user may have come back in a different terminal (after `tmux attach`). Nested apps push and
pop the kitty flags as a stack, which is what the protocol expects.

**Decoding** (always on, whatever the mode, since stray sequences are harmless):
- **Kitty and `formatOtherKeys`:** `CSI code ; modifiers u`, with optional `:`-separated sub-parameters, of
  which only the first is read. `code` is a Unicode code point (letters lowercase), or one of 27 Esc, 13 Enter,
  9 Tab, 127 Backspace, or a kitty private-use code: keypad keys map to their ConsoleKeys, and modifier-only
  keys and media keys are dropped.
- **modifyOtherKeys:** `CSI 27 ; modifiers ; code ~`, with the same meaning.
- **win32-input-mode:** `CSI Vk ; Sc ; Uc ; Kd ; Cs ; Rc _`.
  - Only key downs (`Kd` = 1) are used. `Vk` maps to `ConsoleKey` directly (the values are Windows virtual-key
    codes), and `Uc` is the character.
  - `Cs` is the control key state: Shift 0x10, Left/Right Ctrl 0x08/0x04, Left/Right Alt 0x02/0x01.
  - `Rc` is the repeat count, capped at 32. Modifier-only keys (Shift, Ctrl, Alt, the Windows keys) are dropped.
  - AltGr (Right Alt with Left Ctrl) plus a printable `Uc` is that character without modifiers, as the Phase 10
    translator does for records.
- **Modifiers:** `ModifiersFrom` as today: the bits of m − 1 are Shift 1, Alt 2, Ctrl 4 and Meta 8 (as Alt).
  Kitty's Super 8 and Hyper 16 drop the key (the OS or terminal normally takes those). Caps Lock 64 and Num Lock
  128 are ignored.
- **Same keys as before:** a combination the legacy encoding can express produces the same `ConsoleKeyInfo` as
  it does today:
  - Ctrl+A is `('\x01', A, Control)`, so `KeyInput.IsCtrlC` and Ctrl+Z still work.
  - Alt+x is `('x', X, Alt)`, and Shift+Tab is `(Tab, Shift)`.
  - Esc is Escape: kitty's `CSI 27 u` arrives at once, with no Esc timeout.
  - Ctrl+Backspace is `('\b', Backspace, Control)`, and Ctrl+Space is `('\0', Spacebar, Control)`.
- **New keys:** Ctrl, Shift or Alt with Enter (`'\r'`), Tab (`'\t'`) and Backspace. Ctrl+I, Ctrl+M and Ctrl+[
  as letters and punctuation, not Tab, Enter and Esc. Ctrl and Ctrl+Shift with digits and punctuation, with
  the base character as `KeyChar` and `ConsoleKey.D0`–`D9` or `0` as the key. Ctrl+Shift+letter as
  `(control char, letter, Control | Shift)`.
- **Text:** printable text without modifiers still arrives as UTF-8 bytes in every protocol, so typing,
  non-ASCII input and IMEs are unchanged. Pastes keep using bracketed paste.

**What apps see:** keys that used to arrive as Enter or Tab can now arrive as Ctrl+Enter or Ctrl+Tab. Built-in
handling:
- `TextAreaState` already ignores Enter with Ctrl or Alt, and inserts a line break with Shift+Enter, as chat
  apps do.
- `TextFieldState` and the dialogs treat Shift+Enter as Enter, and ignore Ctrl+Enter.
- Focus treats Ctrl+Tab and Alt+Tab as unhandled, so they aren't Tab.
- `KeyBinding.Matches` stops matching `ctrl+i` and `ctrl+m` to the Tab and Enter keys (its fallback for Ctrl+letter
  as a bare control character now skips those two keys), so a protocol's Ctrl+I and the Tab key stay apart.

This is a "Changed" entry (§0 rule 2): in a terminal with a protocol, Ctrl+Enter no longer submits a form where
Enter does.

### 12.6 Configuration

- `MINITUI_KEYBOARD=legacy` turns off the protocols (as `WithKeyboardProtocol(false)`), and
  `MINITUI_QUERIES=0` turns off the queries, for working around a misbehaving terminal without code changes,
  as `MINITUI_DEBUG` does for the overlay (§9).
- `COLORFGBG` is read for `IsDark` (§12.4).

### 12.7 Debug overlay and example

- The debug overlay's terminal line adds the keyboard protocol, light or dark, and the terminal name when known:
  `120×40  truecolor  braille  mouse  kitty  dark  WezTerm 20240203`.
- **Input demo:**
  - Shows `TerminalInfo` (every field, and whether DA1 was answered).
  - Lists Ctrl+Enter, Shift+Enter and Ctrl+Tab in its "try these" line, so a terminal is quick to check.
  - Shows the raw sequence beside each decoded key, as it does now.
- The example app uses `WithTheme(Theme.Dark, Theme.Light)`. The Widgets demo's theme toggle cycles auto (the
  pair), Default, Dark, Light and Monochrome.
- The Form demo binds Ctrl+Enter to Register, alongside the existing key, and says it needs a terminal with a
  keyboard protocol.

### 12.8 Tests and acceptance

Automated (all platforms):
- **Replies:** each reply form parsed (OSC 10/11 with 1-, 2- and 4-digit channels, BEL and ST endings; XTVERSION;
  kitty flags; XTQMODKEYS; DA1), split at every byte across reads, arriving before, among and after keys and
  pastes, and after the 1 s cut-off. None ever produces a key. Unknown replies are still dropped.
- **Sequencing:** the exact query bytes, written once after entering and again after resume, never before
  entering or after leaving. `Answered` on DA1, the cut-off with fake time, and `TerminalInfoEvent` raised on
  the UI thread once per change.
- **Light and dark:** luminance thresholds, `COLORFGBG` forms (`15;0`, `0;15`, `default;default`, three-part
  values), `WithTheme(dark, light)` with a reply before and after the first frame, the 150 ms wait (fake time),
  and `Theme.Light` snapshots of every widget.
- **Protocol choice:** kitty preferred over modifyOtherKeys, nothing without replies, `?9001h` on Windows VT
  only, opt-outs (API and environment), and the exact enable and disable bytes on every restore path and nested
  run.
- **Decoding:** a table of every key in §12.5 under kitty, modifyOtherKeys and win32-input-mode, checked against
  the legacy decoding wherever legacy can express the key (property test: same `ConsoleKeyInfo`). Super and Hyper
  dropped; keypad, AltGr, repeat counts and key ups; Esc without a timeout; Ctrl+C still quits and Ctrl+Z still
  suspends.
- **Widgets:** Shift+Enter and Ctrl+Enter in the text field, text area, command line and dialogs, and Ctrl+Tab
  and focus.

Manual QA (results in the PR): the Input demo and the Form demo in kitty or WezTerm (kitty protocol), xterm
(modifyOtherKeys), GNOME Terminal (legacy, with OSC 11), tmux with `extended-keys on`, Windows Terminal
(win32-input-mode) and conhost (legacy). On each: Ctrl+Enter, Shift+Enter, Ctrl+Tab and Ctrl+I, Esc, Alt
combinations, Ctrl+C and Ctrl+Z, a paste, and suspending and resuming. Check `WithTheme(dark, light)` on a light
profile, and that no reply ever appears as typed text over SSH.

### 12.9 Migration impact

- Additive API: `TerminalInfo`, `KeyboardProtocol`, `TerminalInfoEvent`, `App.TerminalInfo`,
  `WithTerminalQueries`, `WithKeyboardProtocol`, `WithTheme(dark, light)`, `Theme.Dark` and `Theme.Light`.
- "Changed": queries are written at startup and after resume; a protocol is turned on where supported, so
  Ctrl/Shift+Enter, Ctrl+Tab and Ctrl+I can arrive as themselves; apps with their own `OnEvent` see
  `TerminalInfoEvent`.

### 12.10 Out of scope

- Key release and repeat events, alternate (shifted and base layout) keys, and associated text: kitty flags 2, 4,
  8 and 16. `ConsoleKeyInfo` has nowhere to put them.
- The Super and Hyper modifiers as bindings (`super+s`).
- Cursor position (DSR 6) and other queries without a use here. A cursor position reply looks like Ctrl+F3 in
  the legacy encoding, so it isn't asked for.
- Reading the clipboard with OSC 52 queries (backlog rank 10). The reply parser makes it possible later.
- Changing the theme when the terminal's colors change mid-run (some terminals report this with mode 2031).
- Native `KEY_EVENT_RECORD` reading on Windows, which would bring back the problems that made Phase 10 choose VT
  input.
- Terminal-specific detection by `TERM` or `TERM_PROGRAM` beyond `COLORFGBG`.

### 12.11 Implementation order

Two PRs, like Phases 10 and 16, because the second changes key decoding on every platform:
1. **18a** (queries): the query writes and reply decoding, `TerminalInfo`, `TerminalInfoEvent`, the DA1 sentinel
   and cut-off, `IsDark` and `COLORFGBG`, `WithTheme(dark, light)`, `Theme.Dark` and `Theme.Light`, the debug overlay line and
   the Input demo's info, README ("### Terminal queries and themes"), CHANGELOG, and manual QA of replies on every
   terminal in §12.8.
2. **18b** (keyboard): the three protocol decoders, enabling and restoring, `WithKeyboardProtocol` and
   `MINITUI_KEYBOARD`, the widget handling of new keys, the example's Ctrl+Enter, README ("Keyboard input"
   gains the protocols), CHANGELOG, and the manual QA matrix.

Both go out in one release (1.7.0).

### 12.12 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Keyboard protocols on by default (detected, with opt-outs), or opt-in with `WithKeyboardProtocol()`? | **On by default** (§12.5). Keys legacy can express decode exactly as before, and the point is that apps get Ctrl+Enter without knowing about protocols. `WithKeyboardProtocol(false)` and `MINITUI_KEYBOARD=legacy` opt out. |
| 2 | Turn on modifyOtherKeys only after an XTQMODKEYS reply, or also when `TERM` starts with `xterm` (older xterms and tmux may not answer the query)? | **Only after the reply** (§12.5). Some terminals draw an unknown `CSI > 4;2m` as underlined text, and kitty-protocol terminals cover most users. |
| 3 | With `WithTheme(dark, light)`, wait up to 150 ms before the first frame, or draw at once and switch when the reply arrives? | **Wait up to 150 ms** (§12.3), only when the pair is used. Local terminals answer in a few milliseconds, and a flash of the wrong theme looks broken. |
| 4 | Queries on by default? | **Yes** (§12.3). They're harmless (every terminal must ignore unknown queries and DA1 is universal), and protocol detection needs them. `WithTerminalQueries(false)` or `MINITUI_QUERIES=0` turn them off. |

---

## 13. Command palette and filterable select (Phase 19)

### 13.1 Purpose

In a larger app, nobody remembers every key, and the `?` help overlay only lists them; it can't run them. A
command palette (VS Code's Ctrl+Shift+P, Textual's Ctrl+P, lazygit's `?` menu) fixes that: an overlay with a
text field where typing narrows a list of everything the app can do, and Enter runs the selected item. Phase 19
adds it (backlog rank 2, §1.1) and reuses its filtering list for a second gap left by Phase 13 (§7.12): a
`Select` with dozens of options (countries, time zones, fonts) you can type into to narrow down.
- **`CommandPalette`:** an overlay that fuzzy-filters items as you type and runs the chosen one. By default
  its items are the key bindings in scope (the same entries the help overlay lists, with their keys as hints)
  and, optionally, the commands of a `CommandRegistry`.
- **`Fuzzy.Match`:** the matcher it uses, public so apps can filter their own lists the same way.
- **Filterable select:** `SelectState.Filterable` opens a dropdown with a filter line instead of a plain menu.

### 13.2 Public API

```csharp
/// An item in a CommandPalette (§13.3).
/// <param name="Label">The text that's matched and shown, such as "Refresh".</param>
/// <param name="Run">Called on the UI thread after the palette closes, when the item is chosen.</param>
public sealed record PaletteItem(string Label, Action Run)
{
    /// Muted text shown after the label, not matched: a group ("Files") or a command's description.
    public string? Detail { get; init; }

    /// Display-only text drawn right-aligned and muted, such as a key: "ctrl+s".
    public string? Hint { get; init; }
}

/// A searchable list of things to run (§13.3). Typing filters the items with Fuzzy.Match; ↑ ↓ PgUp PgDn move
/// the selection; Enter or a click runs the selected item; Esc or a click outside closes it.
public sealed class CommandPalette : Overlay
{
    /// Create a palette over `items`. Show it with OverlayStack.Show.
    public CommandPalette(IReadOnlyList<PaletteItem> items);

    /// The chosen item's index in `items`, or null when the palette closed without a choice.
    public Task<int?> Result { get; }

    /// Shown muted in the empty filter line. Default "Type to search".
    public string Placeholder { get; init; }

    /// At most this many item rows are shown; more scroll. Default 12.
    public int MaxRows { get; init; } = 12;

    /// The items for the app's current state: the key bindings in scope, innermost first, then each command of
    /// `commands` (and its subcommands), in add order (§13.4).
    public static IReadOnlyList<PaletteItem> ItemsFor(App app, CommandRegistry? commands = null);
}

public sealed partial class OverlayStack
{
    /// Show a CommandPalette over CommandPalette.ItemsFor(app, commands). Not bound by default:
    /// app.Keys.Add("ctrl+p", "Commands", () => app.Overlays.ShowPalette()).
    public void ShowPalette(CommandRegistry? commands = null);
}

/// Fuzzy matching, as in the command palette (§13.5).
public static class Fuzzy
{
    /// Match `pattern` against `text`: null unless every character of `pattern` (ignoring case and spaces)
    /// appears in `text` in order. An empty pattern matches everything with score 0.
    public static FuzzyMatch? Match(string pattern, string text);
}

/// A successful fuzzy match: higher scores are better matches; Positions are the matched indexes in the text,
/// ascending, for highlighting.
public readonly record struct FuzzyMatch(int Score, IReadOnlyList<int> Positions);

public sealed partial class SelectState
{
    /// Open a dropdown with a filter line instead of a plain menu, and let typing on the closed field open it
    /// (§13.6). Default false.
    public bool Filterable { get; set; }
}
```

- `CommandPalette` is sealed, like `PopupMenu`. Apps that want their own items (recent files, open buffers)
  build a list, optionally starting from `ItemsFor`, and show it with `app.Overlays.Show(new CommandPalette(items))`.
- `SelectBuilder` needs nothing new: the field draws the same whether it's filterable or not.

### 13.3 The palette

- **Placement:** centered horizontally, one row below the top of the screen (two on screens taller than 20
  rows), `max(40, 60%)` of the screen's width (never wider than the screen). Its height fits the filter line, a
  divider and up to `MaxRows` rows, shrinking to the matches and growing back as the filter changes; with no
  matches, one row says "No matches" in `TextMuted`. Framed, with the title " Commands ".
- **The filter line:** a `TextFieldState`, so it has every text field key: ← → Home End, Backspace and Delete,
  Ctrl+←/→ by word, selection, Ctrl+A, copy, cut, paste, and undo. `CapturesText` is true.
- **Keys:** ↑ and ↓ move the selection (wrapping), PgUp and PgDn by a page, Ctrl+Home and Ctrl+End to the first
  and last match. Enter (`KeyInput.IsEnter`) runs the selected item. Esc closes. Tab does nothing. Every other key
  goes to the filter line; the palette swallows everything, as overlays do.
- **Filtering:** at each change of the filter text, every item's `Label` is matched with `Fuzzy.Match`. The
  matches are sorted by score, highest first, with ties in the items' order; an empty filter shows every item in
  order. The selection goes back to the first match whenever the filter changes.
- **Rows:** the label, then the `Detail` in `TextMuted` two columns after it, and the `Hint` right-aligned in
  `TextMuted`, as `PopupMenu` draws hints. The label's matched characters are drawn bold in `Title` (on the
  selected row, bold in `Selection`). Labels, details and hints are sanitized to one line and truncated with `…`,
  the detail first. A scrollbar shows when the matches don't fit.
- **Mouse:** a click on a row runs it, the wheel moves the selection, a click outside closes the palette
  (`CloseOnClickOutside`), and a click in the filter line moves its cursor.
- **Running:** choosing an item closes the palette first, then calls `Run` on the UI thread, so the item runs
  with the focus and overlays as they were before the palette opened (a binding that opens a dialog opens it
  over the screen, not over the palette). An exception from `Run` goes to the app's usual error handling.
  `Result` completes with the item's index before `Run` is called.

### 13.4 The default items

`ItemsFor(app, commands)`, called when the palette opens:
- **Key bindings:** the bindings the help overlay would list (§9.5): the focus chain's key maps innermost first,
  then `App.Keys`, with shadowed bindings left out. One item per `KeyMap` entry: `Label` is its description,
  `Detail` is its group (the component's `KeysGroup`, or "Global"), `Hint` is its first key label that isn't
  shadowed, and `Run` is its handler. Entries with `showInHints: false` are listed too, since the palette is for
  finding what isn't shown. An open overlay's bindings are listed (its chain is the focus chain); the global ones
  aren't, as in help.
- **The binding that opened the palette isn't listed.** `KeyMap` remembers the entry whose handler is running,
  and `ItemsFor` skips it, so "Commands" doesn't appear in its own list, whatever the binding's description is.
- **Commands:** with a `CommandRegistry`, one item per command, then its subcommands, in add order:
  - `Label` is `:name`, or `:name sub` for a subcommand, with the usage after it (`:add <text>`).
  - `Detail` is the description; there's no hint.
  - Choosing a command without a usage runs it as `:name` does in `CommandLineOverlay`: its message becomes a
    toast, and `CommandResult.Quit` quits.
  - Choosing a command with a usage opens a `CommandLineOverlay` for the registry with `name ` already typed,
    so the user adds the arguments.
- Duplicates aren't merged: a command also bound to a key appears twice, once from each source.

### 13.5 Fuzzy matching

`Fuzzy.Match(pattern, text)`, by UTF-16 character, ignoring case with `char.ToUpperInvariant`:
- Spaces in the pattern are skipped, so "new file" matches "New File" and "NewFile". Every other pattern
  character must appear in the text in order; otherwise the result is null.
- **Positions** are the ones with the highest score, found by dynamic programming in O(pattern × text). This
  keeps "nf" on the N and F of "New File" rather than an N and F inside a word.
- **Score:** 0, then per matched character:
  - +10 at a word start: index 0, after a space or punctuation, or an uppercase letter after a lowercase one
    ("Fi" in "OpenFile").
  - +5 right after the previous match (a consecutive run).
  - +3 if the case is the same as the pattern's.
  - −1 for each unmatched character skipped since the previous match (−3 at most per gap).
  - A match of the whole text at the start gets +15, and a prefix of it +10.
  - Ties sort by the shorter text, then the items' order.
- An empty or all-space pattern matches every text with score 0 and no positions.
- The weights aren't a **Decision** and can be tuned: tests pin orderings, not exact scores (a prefix beats a
  word-start match, which beats a scattered match).

### 13.6 Filterable select

With `SelectState.Filterable` true:
- **Opening:** Enter, Space, ↑, ↓, Alt+↓, F4 and a click open it, as today, with an empty filter. A printable
  character without Ctrl or Alt typed on the closed, focused field also opens it, with that character already in
  the filter, so typing "ger" on a country field goes straight to Germany. That's a "Changed" only for fields
  that opt in: a plain select still returns false for letters, so they reach the bindings.
- **The dropdown:** placed as the plain menu is (below the field, as wide as it, above when there's more room),
  with the filter line in its top row, a divider, and the matching options under it, drawn like the palette's
  rows with matched characters highlighted. `MaxRows` is 12.
- **Keys:** as the palette's (§13.3), except that Tab and Esc close it without a choice, as a plain menu.
- **Selection:** with an empty filter the current choice is selected and scrolled into view; once the filter
  changes, the first match is.
- **Choosing** sets `Selected` to the option's index in `Options` (not in the matches) and raises `Changed` if
  it's a different option. The choice is always one of the options: free text isn't accepted.
- Space types a space in the filter once the dropdown is open, so options with spaces can be matched.

### 13.7 Example

- The example app binds Ctrl+P to the palette globally. The Notes screen (the `:` command line demo) binds
  Ctrl+P itself, shadowing the global binding, to `ShowPalette(_commands)`, so its commands are listed too.
- The Controls section of the Form demo gains a filterable "Country" select with about 50 options.
- README: "### Command palette" under the overlays, and the filterable select in the Select paragraph.

### 13.8 Tests and acceptance

Automated:
- **Fuzzy:** matches and non-matches, case, spaces in the pattern, empty patterns, word starts (spaces,
  punctuation, camelCase), the position choice ("nf" in "New File"), and orderings: exact prefix > word starts >
  scattered, shorter text on ties, stable for equal scores. Non-BMP text doesn't throw.
- **Palette:** filtering, ordering and reselecting as the filter changes; every key in §13.3, including
  Ctrl+Home and Ctrl+End and text-field keys reaching the filter; Enter running the item after the palette has
  closed (the focus chain at `Run` is the screen's); `Result`; Esc; the mouse; the "No matches" row; placement
  and shrinking on small screens; snapshots with highlights, details, hints and truncation.
- **Items:** bindings in scope innermost first, shadowing, groups, the opening binding skipped, hidden
  (`showInHints: false`) bindings listed, an open overlay's bindings, registry commands and subcommands, a
  command with a usage opening the command line with its name typed, results as toasts, and `Quit`.
- **Select:** filterable opening keys (and typing on the closed field), the plain select unchanged, choosing an
  option maps to its index in `Options`, `Changed`, Esc and Tab closing without a change, the initial selection,
  and snapshots of the dropdown above and below the field.

Manual QA (results in the PR): the palette and the country select in Windows Terminal, conhost and a Linux
terminal, with the mouse; Ctrl+P in tmux.

### 13.9 Migration impact

- Additive API: `PaletteItem`, `CommandPalette`, `OverlayStack.ShowPalette`, `Fuzzy`, `FuzzyMatch` and
  `SelectState.Filterable`.
- No behavior changes for existing apps: nothing is bound by default, and `Filterable` is off by default.

### 13.10 Out of scope

- Recently used items first, and remembering the filter between openings.
- Item icons, disabled items, separators and nested palettes (a "Go to file…" item opening a second list is an
  app's own `CommandPalette`).
- An editable combo box that accepts text outside the options, multi-select, and `SelectState<T>`.
- Filtering `List`, `Table` and `Tree` as you type (an app can do it with `Fuzzy.Match`); tree filtering is
  backlog rank 8.
- Matching on details or hints, and matching by grapheme cluster or with diacritics folded.
- A default key for the palette (§13.11 question 1).

### 13.11 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Bind Ctrl+P to the palette by default, as `?` is bound to help, or leave it to the app? | **Leave it to the app** (§13.2). Ctrl+P means "previous" or "print" in many apps, and a built-in binding would shadow nothing but still appear in every app's hints. One line binds it. |
| 2 | Default items: key bindings (plus an optional registry), or only items the app passes? | **Key bindings plus an optional registry** (§13.4). Every app gets a useful palette from one line, and the bindings already have descriptions. |
| 3 | Run the chosen item after the palette closes, or while it's still open? | **After it closes** (§13.3), so a binding sees the same focus chain as when its key is pressed, and a dialog it opens isn't stacked over the palette. |
| 4 | Filterable select: open on the first typed letter, or only with the usual open keys? | **Open on the first letter too** (§13.6), as combo boxes do. Only fields that opt in change. |

### 13.12 Implementation order

One PR. The palette and the filterable select share an internal filter list (the filter line, the matches, the
selection and the row drawing), so they're built together:
1. `Fuzzy` and its tests.
2. The internal filter list, then `CommandPalette`, `PaletteItem`, `ItemsFor` and `ShowPalette`.
3. `SelectState.Filterable` and its dropdown, sharing `PopupMenu`'s placement.
4. The example, README, CHANGELOG and the manual QA.

Ships as 1.8.0 when released.

---

## 14. Split panes, scrollbar dragging and sortable tables (Phase 20)

### 14.1 Purpose

Phase 16 added `MouseAction.Drag` (presses, drags and releases, with drags sent to what the press hit), which
makes three common mouse interactions cheap. Phase 20 adds them (backlog rank 3, §1.1):
- **Split panes:** `SplitState` and `Widget.Split()` divide an area into two panes with a one-cell divider that
  the user drags with the mouse or moves with keys. Splits nest, so file managers, log viewers and dashboards
  get resizable layouts.
- **Scrollbar dragging:** the thumbs of the scrollbars that `List`, `Table`, `Tree` and `Paragraph` already draw
  can be dragged, and a press on the track pages towards it.
- **Sortable tables:** a click on a table header cell sets `SelectionState.SortColumn` and flips
  `SortDescending`, and raises an event. The app sorts its data; the table draws the ▲/▼ marker it already has.

### 14.2 Public API

```csharp
/// The divider position of a split (§14.3), drawn with Widget.Split. The panes are sized either by Ratio, so
/// they keep their proportions when the terminal is resized, or with one pane at a fixed size in cells (a
/// sidebar), which keeps its width while the other pane takes the rest.
public sealed class SplitState
{
    /// A proportional split. `direction` is the way the panes are laid out: Horizontal puts them side by side (a
    /// vertical │ divider), Vertical one above the other (a horizontal ─ divider).
    public SplitState(Direction direction, double ratio = 0.5);

    /// A split with `pane` fixed at `cells` (FixedSize and FixedPane set).
    public static SplitState Fixed(Direction direction, int cells, SplitPane pane = SplitPane.First);

    public Direction Direction { get; }

    /// The first pane's share of the space (the area minus the divider), from 0 to 1, when FixedSize is null.
    /// Clamped to 0–1 when set.
    public double Ratio { get; set; }

    /// The fixed pane's size in cells, or null to size by Ratio. Negative values are clamped to 0 when set.
    public int? FixedSize { get; set; }

    /// Which pane FixedSize applies to. Default First.
    public SplitPane FixedPane { get; set; }

    /// The smallest size, in cells, the user can drag each pane to. Default 3 each. If the area is too small for
    /// both, the space is shared by Ratio.
    public int MinFirst { get; set; } = 3;
    public int MinSecond { get; set; } = 3;

    /// True from a press on the divider until the release.
    public bool IsDragging { get; }

    /// Move the divider by `cells` (negative: towards the start), within the minimums, as of the last draw.
    public void MoveBy(int cells);

    /// Alt+Shift+← → (side by side) or Alt+Shift+↑ ↓ (one above the other) move the divider a cell. Returns true
    /// if it moved or was at a limit.
    public bool HandleKey(ConsoleKeyInfo key);

    /// A left press on the divider from the last draw starts a drag; drags move the divider under the pointer;
    /// the release ends it. Returns true while the split handles the event.
    public bool HandleMouse(MouseEvent mouse);

    /// Raised on the UI thread when the user moves the divider (not when code sets Ratio or FixedSize).
    public event EventHandler? Changed;
}

public enum SplitPane
{
    /// The left or top pane.
    First,
    /// The right or bottom pane.
    Second,
}

public sealed class SplitBuilder
{
    /// Draw the divider in the focused border style, as while dragging. Default false.
    public SplitBuilder Focused(bool focused = true);

    /// A style patched over the theme's border style for the divider.
    public SplitBuilder Style(Style style);

    /// Draw the divider and return the two panes, for the app to draw into.
    public (Rect First, Rect Second) Draw(Buffer buf, Rect area);
}

public static partial class Widget
{
    public static SplitBuilder Split(SplitState state);
}

public sealed partial class SelectionState
{
    /// Sort by `column`: ascending if it isn't the sort column yet, else flip SortDescending. Raises SortChanged.
    public void ToggleSort(int column);

    /// Raised on the UI thread when a header click or ToggleSort changes SortColumn or SortDescending.
    public event EventHandler? SortChanged;
}

public sealed partial class TableBuilder
{
    /// Let a click on the header cells of `columns` (all columns when none are given) call ToggleSort (§14.5).
    public TableBuilder Sortable(params int[] columns);
}
```

- `SelectionState.HandleMouse`, `TreeState<T>.HandleMouse` and `ScrollState.HandleMouse` gain the scrollbar
  handling (§14.4) with no signature change.
- `ScrollbarBuilder` is unchanged; the widgets record where they drew the bar.

### 14.3 Split panes

- **Layout:** the divider takes one cell across the area. The space is the area's width (side by side) or height
  minus 1.
  - **By ratio** (`FixedSize` null): the first pane gets `round(Ratio × space)` cells.
  - **Fixed** (`FixedSize` set): the `FixedPane` gets `FixedSize` cells, and the other pane the rest, so a
    sidebar keeps its width when the terminal is resized.
  - Either way the size is clamped so each pane keeps its minimum. If the space is smaller than both minimums,
    the minimums are ignored and the space is shared by `Ratio` (or the fixed pane gets at most the space). An
    area with no space (1 cell or less) gives the first pane everything and draws no divider.
  - Clamping is only for drawing: `Ratio` and `FixedSize` keep their values, so a sidebar squeezed by a small
    terminal comes back to its width when the terminal grows.
- **Drawing:** `│` (side by side) or `─` along the divider in `Border`, or in `BorderFocused` while dragging or
  with `Focused()`. The panes aren't cleared or drawn; the app draws into them, usually with `ctx.Render`.
- **Mouse:** the component that draws the split passes its mouse events to `HandleMouse`. Children drawn into
  the panes don't cover the divider, so a press on it reaches the parent.
  - A left press on the divider starts a drag (`IsDragging`), keeping the offset between the pointer and the
    divider, and returns true.
  - Each drag moves the divider so it stays under the pointer, clamped to the minimums, and stores the new
    position the way the split is sized: `Ratio` from the first pane's size, or `FixedSize` as the fixed pane's
    new size. `Changed` is raised when the size changed. `MoveBy` stores it the same way. Drags go to what the press hit (§10.7),
    so the pointer may leave the area.
  - A release, or a press anywhere else, ends the drag.
  - **Releases go to the press target** (changed from §10.7, where they went to what's under the pointer), as
    drags do, so a drag that ends over another pane still ends. The press target is cleared by the release.
  - Clicks and the wheel elsewhere return false, so they reach the panes' own handling.
- **Keys:** `HandleKey` takes Alt+Shift with the arrows along the split's direction and calls `MoveBy(±1)`. The
  app calls it from the parent component's `HandleKey` (or binds other keys to `MoveBy` in a `KeyMap`). Focused
  children see the keys first, so a text field with Shift+arrow selection keeps them. `SelectionState`,
  `ScrollState` and `TreeState` take the arrows with any modifiers, so a pane drawn with them passes Alt keys on
  (`if ((key.Modifiers & ConsoleModifiers.Alt) != 0) return false;`), as the example's panes do.
- **Nesting:** each split has its own state, and a pane can be split again; each `HandleMouse` only reacts to its
  own divider. When two dividers meet, the outer one is drawn last and wins the crossing cell.

### 14.4 Scrollbar dragging

Applies to the scrollbars of `List` and `Table` (`SelectionState`), `Tree` (`TreeState<T>`) and `Paragraph`
(`ScrollState`). Each widget records the scrollbar's rect, the content length and the viewport while drawing.
- **The thumb:** a left press on it starts a drag, keeping where on the thumb it was pressed. Each drag moves the
  thumb so that point stays under the pointer: the new offset is the one whose thumb starts there
  (`ScrollbarBuilder.Thumb` inverted, rounded to the nearest), clamped to the content.
- **The track:** a left press above or below the thumb scrolls one page towards it, as GUI scrollbars do. It
  doesn't repeat while held.
- **The selection stays put** (`SelectionState`, `TreeState<T>`; changed in Phase 20 QA): the scrollbar and the
  wheel scroll the view, and the selection keeps its item even when it scrolls out of sight. The widgets bring
  the selection back into view only when it moves: a key, a click on a row, or code setting `Selected`. The
  wheel scrolls lists, tables and trees one row per event (Phase 10 decision 5 kept the step; it used to move
  the selection by one).
- **Release** ends the drag. The press focuses the widget's component, as any press does (§4.7).
- Wheel scrolling and row clicks are unchanged. A press on the scrollbar never selects a row.
- **Scrollbars on the border** (a block with a right border) are handled the same way: the recorded rect is
  where the bar was drawn.

### 14.5 Sortable tables

- `Sortable(columns)` makes the table record each sortable header cell's rect while drawing. Without it, header
  clicks do nothing, as before, so apps that don't sort don't get a ▲ they don't honor.
- A left press on a sortable header cell calls `ToggleSort(column)`: a new column sorts ascending, the same
  column flips the direction. `SortChanged` lets the app re-sort its items; the table shows the marker from
  `SortColumn` and `SortDescending` as it does today.
- Selection after a sort is the app's: `Selected` is an index, so an app that wants the same item selected finds
  its new index in `SortChanged`.
- Keys: none built in. Apps bind keys to `ToggleSort` (the example binds `s` to cycle columns).

### 14.6 Example

- A new **Panes** demo: a fixed-width list of log sources on the left (`SplitState.Fixed`, 24 cells), and on the right a sortable table of log entries
  (about 200 rows) above a paragraph showing the selected entry, in two nested splits. Its hint line lists
  dragging the dividers and scrollbars, Alt+Shift+arrows, clicking the headers and `s` to sort.
- README: "### Split panes" under Layout, the scrollbar dragging in the mouse section, and `Sortable` in the
  table reference.

### 14.7 Tests and acceptance

Automated:
- **Split layout:** ratios, rounding, fixed first and second panes (kept across resizes, restored after a
  squeeze), minimums, too-small areas (minimums ignored, 1-cell areas), both directions, and divider snapshots
  in `Border` and `BorderFocused`.
- **Split mouse and keys:** press on the divider, drags inside and outside the area, clamping, `Ratio` or
  `FixedSize` updated by mode, and `Changed` (once per size change, not when code sets `Ratio`), release and a press elsewhere ending the drag,
  presses off the divider returning false, nested splits, and Alt+Shift+arrows only along the direction.
- **Scrollbars:** for each widget, dragging the thumb from its top, middle and bottom to every position, past
  both ends, a track press paging each way, the selection kept while scrolling and revealed by a key (no
  `SelectionChanged` for trees), the
  scrollbar drawn inside and on a border, and wheel and row clicks unchanged.
- **Sorting:** header clicks on sortable and unsortable columns, `Sortable()` with no columns, toggling,
  `SortChanged`, the marker, and clicks without `Sortable` doing nothing.

Manual QA (results in the PR): the Panes demo with the mouse in Windows Terminal, conhost and a Linux terminal
(dragging both dividers and every scrollbar, header clicks), and in tmux.

### 14.8 Migration impact

- Additive API: `SplitState`, `SplitPane`, `SplitBuilder`, `Widget.Split`, `SelectionState.ToggleSort` and `SortChanged`, and
  `TableBuilder.Sortable`.
- "Changed": a press on a list, table, tree or paragraph scrollbar used to do nothing; it now scrolls. The wheel
  over a list, table or tree scrolls the view instead of moving the selection, and the selection is only scrolled
  back into view when it moves. A mouse release goes to the component the press hit, not the one under the
  pointer. No key changes.

### 14.9 Out of scope

- Splits of more than two panes in one state (nest them instead), collapsing a pane, double-clicking the divider
  to reset it, and saving the ratio between runs.
- Splits as components that own their panes' children; the app draws into the rects.
- Repeating track presses while the button is held, and horizontal scrollbars (no widget draws one).
- Scrollbars of `PopupMenu`, the palette, and `TextArea` (which has none yet; backlog rank 5).
- Multi-column sorting, sorting built into the table, and per-column sort keys.
- Dragging rows, columns or list items, and resizing table columns.
- Hover styles for the divider and thumb (backlog rank 7).

### 14.10 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Store the divider as a ratio (panes scale when the terminal resizes) or as the first pane's size in cells (a sidebar keeps its width)? | **Decided: both** (§14.2, §14.3). `Ratio` by default; `SplitState.Fixed(direction, cells, pane)` or `FixedSize` keeps either pane at a size in cells, and dragging updates whichever the split uses. |
| 2 | Keys to move the divider: Alt+Shift+arrows built into `HandleKey`, or no built-in keys (only `MoveBy`)? | **Decided: Alt+Shift+arrows** (§14.3). They're rarely bound, and every terminal and conhost report them; apps can still bind others to `MoveBy`. |
| 3 | A press on the scrollbar track: page towards it, or jump the thumb there? | **Decided: page towards it** (§14.4), as Windows and most GUIs do; dragging covers jumping. |
| 4 | Header clicks: opt in with `Sortable()`, or on for every table with a `SelectionState`? | **Decided: opt in** (§14.5), so a table whose app doesn't sort never shows a marker it doesn't honor. |

### 14.11 Implementation order

One PR:
1. Scrollbar dragging in `SelectionState`, `TreeState<T>` and `ScrollState`, with the widgets recording the bar.
2. `ToggleSort`, `SortChanged` and `TableBuilder.Sortable`.
3. `SplitState` (ratio and fixed), `SplitPane`, `SplitBuilder` and `Widget.Split`.
4. The Panes demo, README, CHANGELOG and the manual QA.

Ships as 1.9.0 when released.

---

## 15. Menu bar and richer menus (Phase 21)

### 15.1 Purpose

Apps in the style of classic desktop TUIs (Turbo Vision, Midnight Commander, Norton Commander, edit.com) put
their commands in a menu bar along the top: F10 or Alt+letter opens a menu, the arrows move between menus,
submenus group related commands, check and radio items show settings, and an underlined letter chooses an item
without moving to it. `PopupMenu` (Phase 13) has none of that. Phase 21 adds it (backlog rank 4, §1.1):
- **Richer menu items,** in `PopupMenu` and so in context menus too: mnemonics (`&File`), check and radio
  marks, submenus, and an action to run when the item is chosen.
- **`MenuBar`:** a row of menu titles at the top of the screen, opened with F10, Alt+mnemonic or a click.
- **The palette lists the menu bar's items,** so every menu command is also searchable (§13).

### 15.2 Public API

```csharp
public sealed record MenuItem(string Label)
{
    // Existing: Hint, Enabled, IsSeparator, Separator

    /// Run on the UI thread after the menu (and every menu it was opened from) closes, when the item is chosen.
    public Action? Run { get; init; }

    /// A check mark (✓) when true, an empty mark column when false; null for an item without one. Default null.
    public bool? Checked { get; init; }

    /// Draw Checked as a radio mark (● / ○) instead of a check. Default false.
    public bool Radio { get; init; }

    /// The items of a submenu, opened to the side with → or Enter or a click (§15.4). Null for none. An item
    /// with a submenu is never chosen itself, and its Hint isn't drawn (▸ is).
    public IReadOnlyList<MenuItem>? Submenu { get; init; }

    /// The mnemonic: the character after the first single & in Label (lowercase), or null. "&&" is a literal &,
    /// and an & before a space or at the end is literal too.
    public char? Mnemonic { get; }
}

public sealed class PopupMenu : Overlay
{
    // Existing: Result (the chosen top-level index), MinWidth, MaxRows

    /// The chosen item, at any depth of submenus, or null when the menu was dismissed. Completes with Result.
    public Task<MenuItem?> Chosen { get; }
}

/// A menu in a MenuBar.
public sealed class Menu
{
    /// A menu with fixed items.
    public Menu(string title, IReadOnlyList<MenuItem> items);

    /// A menu whose items are built each time it opens, so Checked and Enabled can follow the app's state.
    public Menu(string title, Func<IReadOnlyList<MenuItem>> items);

    /// The title, with an optional & mnemonic: "&File".
    public string Title { get; }

    /// The items, built now.
    public IReadOnlyList<MenuItem> Items();
}

/// A row of menus along the top of the screen (§15.5). Set it with App.WithMenuBar.
public sealed class MenuBar
{
    public MenuBar(params Menu[] menus);

    public IReadOnlyList<Menu> Menus { get; }

    /// True while one of its menus is open.
    public bool IsOpen { get; }

    /// Open the menu at `index` (as F10 opens the first). Does nothing while one is open.
    public void Open(int index = 0);
}

public partial class App
{
    /// Show `menuBar` on the top row of the screen, above the screens (§15.5). Null removes it.
    public App WithMenuBar(MenuBar? menuBar);

    /// The menu bar, or null.
    public MenuBar? MenuBar { get; }
}
```

- **Mnemonics** are parsed from the label, so existing code needs no new property. `MenuItem.Label` keeps the
  `&` as written; the menus draw it without the `&`.
- `Run` is optional: an app can still await `Result` or `Chosen` instead, or both.
- `Menu` is a class rather than a record because it holds a factory.

### 15.3 Item marks, mnemonics and drawing

- **Rows** keep the Phase 13 layout (§7.4) and gain two optional columns:
  - **A mark column** at the left, two cells wide, drawn only when some item in the menu has `Checked` set:
    `✓ ` for a checked item, `● ` or `○ ` for a radio item, and blanks otherwise. Basic glyphs (conhost, §9):
    `√`, `*` and `o`.
  - **A submenu arrow** at the right, `▸` (basic: `►`), in place of the hint, for items with a `Submenu`.
- **Mnemonics** are drawn underlined (`Modifiers.Underlined`, patched over the row's style, including the
  selection). The legacy console backend can't draw underlines, so there the mnemonic still works but isn't
  marked (§15.9).
- **Width** counts the mark column and the arrow.

### 15.4 Keys, submenus and choosing

In a `PopupMenu` (and every menu of a `MenuBar`):
- **A mnemonic** (a character typed without Ctrl, matching an enabled item's `Mnemonic`, ignoring case) chooses
  that item at once, or opens its submenu. If several enabled items share it, the first press selects the next
  one and doesn't choose, as Windows does. A character that's no item's mnemonic falls back to type-ahead
  (§7.3). Alt+mnemonic works the same, so users who hold Alt from the menu bar still choose.
- **→ or Enter** on an item with a submenu opens it as a `PopupMenu` beside the item: anchored to the item's row,
  to the right of the menu, or to the left when it doesn't fit on the right. Its first enabled item is selected.
- **← or Esc** in a submenu closes it, back to the parent menu. In a top-level popup they behave as before (Esc
  dismisses, ← does nothing).
- **Mouse:** a click on an item with a submenu opens it; a click on another item of the parent closes the
  submenu first. A click outside every open menu closes them all.
- **Choosing** an item (Enter, its mnemonic or a click) closes the menu and every menu it was opened from, then
  completes `Result` (the top-level index: the index of the top-level item whose submenu holds the choice) and
  `Chosen` (the item), then calls the item's `Run`, on the UI thread. As with the palette (§13.3), `Run` sees
  the focus and overlays from before the menu opened.
- **Check and radio items** don't toggle themselves (items are immutable records): `Run` changes the app's state,
  and the next opening shows it (a `Menu` built by a factory, or a new item list for a context menu).
- Submenus nest to any depth; each is its own overlay, so the keys go to the deepest one.

### 15.5 The menu bar

- **Where:** the top row of the screen, above the top screen, which gets the rows below it (the status bar
  stays at the bottom). Overlays are placed in the same body, so a dialog never covers the menu bar.
- **Drawing:** the row in `StatusBar`, then each title as ` Title ` from the left, with its mnemonic underlined.
  The open menu's title is drawn in `Selection`. Titles that don't fit are left out (no scrolling).
- **Opening:**
  - **F10** opens the first menu.
  - **Alt+mnemonic** opens the menu with that title mnemonic.
  - **A click** on a title opens its menu, or closes it if it's the open one.
  - These are checked after the focus chain and `App.Keys` (§9.4 step 4), so a focused component and the app's
    own bindings keep their Alt+letter keys (the Tree demo's Alt+G). `App.Keys` gets an `f10` "Menu" entry
    (hidden from the hints, listed in help and the palette).
  - Opening does nothing while an overlay other than the bar's own menus is open, since overlays are modal.
- **An open menu** is a `PopupMenu` of `Menu.Items()` anchored under its title.
  - **← and →** in a top-level bar menu open the previous or next menu, wrapping. In a submenu, ← closes it (as
    in §15.4), and → opens the selected item's submenu, or, on an item without one, moves to the next menu.
  - **F10 or Esc** closes the open menus. A click on the bar row while a menu is open goes to the bar (switching
    or closing), not to "outside".
  - `IsOpen` is true while any of its menus is open.
- **Hints and focus:** the status bar shows the open menu's hints, as for any overlay. The screen's focus doesn't
  change, so closing a menu returns to where the user was.
- **Resizing** keeps the open menu under its title.

### 15.6 The palette

`CommandPalette.ItemsFor` (§13.4) adds the menu bar's items after the key bindings and before the registry's
commands: every enabled item with a `Run` and without a submenu, at any depth, in menu order. `Label` is the
item's label without its `&`, `Detail` is its path (`File › Export`), and `Hint` is its `Hint`. Items are built
when the palette opens, so factories run then. The help overlay doesn't list menus.

### 15.7 Example

- The example app gets a menu bar: **File** (Open demo, a submenu of the demos, and Quit), **View** (Theme as radio
  items: Auto, Default, Dark, Light, Monochrome; a "Debug overlay" check item), and **Help** (Keys, About).
  Its items run the same code as the existing keys.
- The Command Registry demo's right-click menu gains a "Sort" submenu with radio items and mnemonics.
- README: "### Menu bar" under overlays, and the new `MenuItem` properties in the menu paragraph.

### 15.8 Tests and acceptance

Automated:
- **Items:** mnemonic parsing (`&&`, `& `, trailing `&`, several `&`, non-ASCII), drawing without `&` and with
  the underline, the mark column (only when some item has `Checked`), check and radio glyphs and their basic
  forms, the submenu arrow replacing the hint, and widths.
- **Keys:** mnemonics choosing, opening submenus, duplicates cycling, falling back to type-ahead, Alt+mnemonic;
  →, Enter, ← and Esc with submenus at two depths; placement right and left; `Result`, `Chosen` and `Run`
  ordering (after every menu closed, with the screen's focus chain); the mouse on submenus and outside.
- **Menu bar:** layout (the screen's area starts one row lower, overlays below the bar), drawing and the open
  title, F10, Alt+mnemonic after the chain and `App.Keys` (and not over a modal overlay), clicks on titles
  (open, switch, close), ← → between menus and from submenus, factories run at each opening, `IsOpen`, resizing,
  and `WithMenuBar(null)`.
- **Palette:** menu items with paths and hints, disabled and submenu items left out, factories at opening.

Manual QA (results in the PR): the example's menus with keys and the mouse in Windows Terminal, conhost and a
Linux terminal, and in tmux; Alt+letters in each (some terminals send Alt as Esc-prefixed, and conhost needs
"Use Alt as Meta" off for Alt+letter to arrive).

### 15.9 Out of scope

- Hover (a menu following the pointer, or a submenu opening on hover): no motion reports (backlog rank 7).
- Showing mnemonics only while Alt is held, and Alt alone activating the bar: terminals don't report a lone Alt.
- Underlined mnemonics on the legacy console backend.
- Menus that scroll horizontally, a bar on another edge, and toolbars.
- Keyboard accelerators bound from `Hint` (`Hint` stays display-only; the app binds keys in a `KeyMap`).
- Mnemonics on buttons, tabs and labels.
- Tear-off menus and menus that stay open after choosing a check item.

### 15.10 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | F10: open the first menu at once, or only highlight the bar (Enter or ↓ opens), as Windows does? | **Open the first menu** (§15.5), as Turbo Vision and Midnight Commander do; it's one key fewer and needs no "bar focused" state. |
| 2 | Alt+mnemonic priority: after the focus chain and `App.Keys` (the app's Alt bindings win), or before them (the menu bar wins)? | **After** (§15.5). Existing apps keep their Alt+letter keys, and a menu title can still be reached with F10. |
| 3 | Mnemonic marker: `&` (Windows, WinForms, Terminal.Gui) or `~` (Turbo Vision's `~F~ile`)? | **`&`** (§15.2), the .NET convention. |
| 4 | List the menu bar's items in the palette? | **Yes** (§15.6). It's what makes menu commands searchable, and costs nothing for apps without a bar. |

### 15.11 Implementation order

Two PRs, like Phase 18, because the first is useful on its own (context menus) and the second changes the screen
layout:
1. **21a** (richer menus): `Run`, `Checked`, `Radio`, `Submenu`, `Mnemonic` and `Chosen`; drawing, keys,
   submenus and the mouse in `PopupMenu`; the Command Registry demo's Sort submenu; README; CHANGELOG.
2. **21b** (menu bar): `Menu`, `MenuBar`, `WithMenuBar` and `App.MenuBar`; the layout, F10, Alt+mnemonics,
   clicks and ← →; the palette items; the example's menu bar; README; CHANGELOG; the manual QA.

Both go out in one release (1.10.0).

---

## 16. Text area as an editor (Phase 22)

### 16.1 Purpose

`TextArea` (Phase 12, with selection, the clipboard and undo from Phase 16) is good for notes and messages, but
not for editing a config file or a script. It has no indent key, each new line starts at column 0, nothing shows
where the cursor is in a long file, and there's no way to find text. Phase 22 adds the missing parts (backlog
rank 5, §1.1):
- **Indentation:** an opt-in Tab-to-indent mode (Tab and Shift+Tab indent and outdent, and Backspace deletes a
  level), opt-in auto-indent on Enter, and a configurable indent size.
- **Line numbers:** an optional gutter.
- **A scrollbar** on the block's border, which can be dragged like the others (§14.4).
- **Find:** Ctrl+F opens a find bar at the bottom of the text area. Matches are found as you type and
  highlighted, and Enter, F3 and Shift+F3 move between them.

Syntax highlighting stays out: it needs a tokenizer (§16.10).

### 16.2 Public API

```csharp
public class TextAreaState
{
    // Existing members unchanged

    /// Tab and Shift+Tab indent and outdent instead of moving focus, and Backspace in a line's indentation
    /// deletes back to the previous indent stop (§16.3). Off by default.
    public bool TabIndents { get; set; }

    /// Enter starts the new line with the current line's indentation (§16.3). Off by default.
    public bool AutoIndent { get; set; }

    /// The indent stop width: what Tab inserts, Shift+Tab removes, and how far tabs in typed, set or pasted text
    /// expand (§6.4). 4 by default; ArgumentOutOfRangeException outside 1–8. Changing it doesn't touch the text.
    public int IndentSize { get; set; }

    /// Ctrl+F opens the find bar and F3 / Shift+F3 find again (§16.5). On by default; turn it off to leave
    /// those keys to the app's bindings.
    public bool FindEnabled { get; set; }

    /// True while the find bar is open.
    public bool IsFinding { get; }

    /// The text being found. Setting it searches again, from the cursor.
    public string FindText { get; set; }

    /// The number of matches of FindText in the text, and the current one's index (−1 when none is current).
    public int MatchCount { get; }
    public int MatchIndex { get; }

    /// Open the find bar (as Ctrl+F does) or close it (as Esc does), keeping the current match selected.
    public void OpenFind();
    public void CloseFind();

    /// Select the next or previous match, wrapping around the text. False when there are no matches.
    public bool FindNext();
    public bool FindPrevious();
}

public sealed class TextAreaBuilder
{
    // Existing members unchanged

    /// Show line numbers in a gutter on the left (§16.4).
    public TextAreaBuilder LineNumbers(bool show = true);
}

public sealed record Theme
{
    // Existing members unchanged

    /// Matches of the find text other than the current one (the current one is the selection). Every built-in
    /// theme sets it; a theme made with `with` from one of them inherits it.
    public Style SearchMatch { get; init; }
}
```

- Nothing changes for an app that sets none of these, except the scrollbar (§16.4) and Ctrl+F / F3 (§16.5),
  which are "Changed" entries.
- The find bar's text field isn't public. Its keys are the text field keys (§8.4, §10.3), and `FindText` reads
  and sets its text.

### 16.3 Indentation

Indent stops are every `IndentSize` display columns. The text only ever holds spaces (§6.4), so indenting means
inserting and removing spaces.

| Key (with `TabIndents`) | Effect |
|---|---|
| Tab, no selection or a selection within one line | Replace the selection (if any) with spaces up to the next indent stop. |
| Tab, a selection over several lines | Indent every line the selection touches by `IndentSize` spaces. A line the selection ends at column 0 of isn't touched, and empty lines aren't indented. |
| Shift+Tab | Outdent the current line, or every line the selection touches, by up to `IndentSize` leading spaces (removing what's there, to the previous stop). |
| Backspace, no selection, only spaces before the cursor | Delete back to the previous indent stop (one space when that's all there is). |

- **Auto-indent** (`AutoIndent`): Enter inserts the line break and then the leading spaces of the line it was
  pressed in, but no more of them than there were before the cursor (Enter inside the indentation doesn't add
  any). It works without `TabIndents`. Paste and `Insert` never auto-indent.
- **The selection after Tab and Shift+Tab** over several lines keeps covering the same text: the anchor and the
  cursor move with the spaces added or removed on their lines.
- **Undo:** each Tab, Shift+Tab and indent-stop Backspace is its own group (§10.6). Enter with auto-indent is one
  group, as Enter is.
- **Focus.** With `TabIndents`, Tab and Shift+Tab never reach the focus chain from a focused text area, so they
  can't move focus out of it. Clicking another component still does, and so do the app's own keys. Apps that
  turn it on say so in their help, as editors do. Ctrl+Tab isn't handled, so it reaches the bindings where the
  terminal reports it (§12).
- Without `TabIndents`, Tab and Shift+Tab are unhandled as before, and Backspace deletes one cluster.

### 16.4 Line numbers and the scrollbar

- **The gutter** (`LineNumbers()`): line numbers right-aligned in a column as wide as the last line's number (at
  least 2 digits), then one space. The wrapped rows after a line's first row have a blank gutter. The cursor
  line's number uses `Theme.Text` and the others `Theme.TextMuted`. The text is laid out and wrapped in the width
  left over. With `WrapMode.None`, the gutter stays put while the text scrolls horizontally.
- When the area is too narrow for the gutter and one column of text, the gutter isn't drawn.
- A left press in the gutter puts the cursor at the start of that row, and a drag from there extends the
  selection as in the text (§10.7).
- **The scrollbar:** with a block that has a right border, when the rows don't fit, a vertical scrollbar is drawn on
  that border, as for lists and paragraphs (§14.4). Pressing and dragging its thumb scrolls, and a press on its track
  pages, without moving the cursor (as the wheel does, §6.6). The view stays where it was left until the cursor
  moves again. Without a block there's no scrollbar, so the text area doesn't lose a column.
- `WrapMode.None` gets no horizontal scrollbar.

### 16.5 Find

**Opening and closing.** Ctrl+F opens the find bar. It's the last row of the text area (inside the block), so
the text gets one row less. The bar shows `Find:`, the find field, and `3 of 12`, `12 matches` (when the
selection isn't one of them), `No matches`, or nothing while the field is empty, right-aligned in
`Theme.TextMuted`. While the bar is open, the text's cursor isn't drawn: the terminal cursor is in the find field,
and pastes go to it.
- The find field starts with the selection when there's one within a line, or else the last find text. All of
  it is selected, so typing replaces it.
- **While the bar is open, keys go to it:**

| Key | Effect |
|---|---|
| Typing, paste, the text field keys | Edit the find text. After each change, the first match at or after where the search started (the selection's start when the bar opened) is selected, wrapping around the text. |
| Enter, ↓, F3 | The next match, wrapping. |
| Shift+Enter (where reported, §12), ↑, Shift+F3 | The previous match, wrapping. |
| Ctrl+F | Select all of the find text. |
| Esc | Close the bar. The current match stays selected, with the cursor at its end. |

- Other keys the find field doesn't take (Tab, Ctrl+S) aren't handled, so they reach the focus chain and the
  bindings as before. The bar stays open when focus moves away; it's part of the state.
- **With the bar closed,** F3 and Shift+F3 find the next and previous match of the last find text, and Esc is
  unhandled as before.
- **The mouse:** a press on the bar's row places the find field's cursor. A press in the text places the text's
  cursor as usual and leaves the bar open; the next key still goes to the bar.

**Matching.**
- Matches are found within lines; the find field is one line, so a match never spans a line break. They don't
  overlap and are found left to right.
- **Smart case:** a find text with no uppercase letter matches case-insensitively (ordinal, ignoring case); one
  with any uppercase letter matches exactly. This is less's and vim's `smartcase`.
- A match that would start or end inside a grapheme cluster is skipped.
- The matches are found again whenever the find text or the text changes. That's linear in the text, like the
  layout (§6.5).

**Drawing.** The current match is the selection, so it's drawn with `Theme.Selection`. The other visible matches
are patched with `Theme.SearchMatch`. They're shown while the bar is open, and cleared when it closes.

**Built-in themes:** `SearchMatch` is a yellow background with black text in the default and dark themes (the
usual find highlight), a light yellow background in the light theme, and underlined in monochrome.

### 16.6 Configuration

None beyond the properties in §16.2.

### 16.7 Example

A new **Editor** demo: a text area in a block titled with a file name and `Ln 3, Col 5` (from `Cursor`), with
`TabIndents`, `AutoIndent`, `IndentSize = 2`, line numbers and find. It starts with a sample config file long
enough to scroll. The footer lists Tab / Shift+Tab, Ctrl+F, F3 and Ctrl+S (a toast that says "Saved", nothing
written). Esc pops the screen only while the find bar is closed.

### 16.8 Tests and acceptance

Automated (all platforms):
- **Indentation:** Tab at several columns, over a one-line selection and a multi-line one (the end-at-column-0
  line and empty lines left alone), Shift+Tab with fewer than `IndentSize` spaces and none, the selection after
  each, Backspace to the previous stop and as usual elsewhere, `IndentSize` limits and tab expansion with it,
  auto-indent (in, after and inside the indentation, with and without `TabIndents`), undo of each, and Tab and
  Shift+Tab unhandled without `TabIndents`.
- **Focus:** with `TabIndents` in a form (`ComponentHarness`), Tab stays in the text area; without it, it moves
  on.
- **Gutter:** widths at 9, 10, 99 and 100 lines, the cursor line's style, blank continuation rows, wrapping in
  the narrower width, `WrapMode.None` scrolling under a fixed gutter, too narrow to draw, and clicks and drags in
  it.
- **Scrollbar:** drawn only with a block and only when the rows don't fit; thumb drags and track presses scroll
  without moving the cursor, and the next move brings the cursor back into view.
- **Find:** open with and without a selection and with a previous find text; incremental search from the start
  point, wrapping; next and previous by each key; Esc keeps the match selected; smart case; overlapping and
  cluster-splitting candidates; matches updated after a text change from code; `MatchCount`, `MatchIndex` and the
  bar's count text; F3 with the bar closed; `FindEnabled = false` leaves Ctrl+F and F3 unhandled; keys the bar
  doesn't take; mouse presses on the bar and in the text.
- **Snapshots:** the gutter, the scrollbar, the find bar with matches highlighted, and monochrome.

Manual QA (results in the PR): the Editor demo in Windows Terminal, conhost and a Linux terminal, and in tmux:
indent and outdent blocks, auto-indent, scroll with the bar, find with each key. Shift+F3 and Shift+Enter are the
keys most likely to differ between terminals.

### 16.9 Migration impact

- New public API (§16.2) in `PublicAPI.Unshipped.txt`.
- **Changed:** a text area with a block shows a scrollbar on its right border when its text doesn't fit. Ctrl+F
  and F3 in a focused text area open find and find again, instead of reaching the bindings (set `FindEnabled =
  false` to keep them there).
- README: the editor features under `TextArea`, and `SearchMatch` in the theme list.

### 16.10 Out of scope

- Syntax highlighting, bracket matching, and code folding.
- Replace (Ctrl+H), regular expressions, whole-word matching, and a case toggle.
- Matches across lines (a multi-line find text).
- Hard tabs (`\t` kept in the text): the text only holds spaces (§6.4).
- Smarter auto-indent: indenting after `{` or `:`, outdenting on `}`, and trimming whitespace-only lines.
- Go to line (Ctrl+G), a status line built into the widget, and a horizontal scrollbar.
- A scrollbar without a block.
- Large documents: layout and matching stay linear in the text per frame or edit (§6.5).

### 16.11 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Where the find bar goes: the text area's last row (part of the state, works without an `App`), or an overlay above it? | **The last row** (§16.5): it's self-contained, and doesn't cover the text being searched. |
| 2 | Ctrl+F and F3 on by default (a "Changed" entry), or only when an app opts in? | **On, with `FindEnabled` to turn them off** (§16.2): find is useful in any long text, and nobody uses MiniTui yet. |
| 3 | The scrollbar: automatic with a block, as lists and paragraphs have it (a "Changed" entry), or opt-in? | **Automatic with a block** (§16.4), for consistency with the other scrolling widgets. |
| 4 | Case: smart case, always ignore case, or a toggle key? | **Smart case** (§16.5): no extra key or UI. |
| 5 | With `TabIndents`, should some key still move focus out of the text area (Esc then Tab, as VS Code's Ctrl+M toggle allows)? | **No** (§16.3): focus leaves by the mouse or the app's keys, and the app decides. It's opt-in, so forms keep Tab. |

### 16.12 Implementation order

Two PRs, like Phase 21:
1. **22a** (indentation, line numbers, scrollbar): `TabIndents`, `AutoIndent`, `IndentSize`, the indent keys and
   their undo groups; `LineNumbers()` and the gutter in layout, drawing and the mouse; the scrollbar and its
   dragging; the Editor demo without find; README; CHANGELOG.
2. **22b** (find): `FindEnabled`, the find bar, matching and its keys and mouse; `Theme.SearchMatch` in every
   built-in theme; the demo's find; README; CHANGELOG; the manual QA.

Both go out in one release (1.11.0).

---

## 17. Double-click and hover (Phase 23)

### 17.1 Purpose

Clicking in a MiniTui app works, but it doesn't feel native yet:
- **Double-click.** Nothing reacts to one. A double-click on a word doesn't select it, and one on a list row or a
  tree leaf doesn't open it. Terminals report presses, not clicks, so the app has to time them itself.
- **Hover.** Nothing shows what's under the pointer. GUI menus follow the pointer, and buttons and rows light up
  under it. That needs all-motion reports (`?1003`), which the app doesn't ask for today (§10.7).

Phase 23 adds both (backlog rank 7, §1.1). Double-click works for every app with `WithMouse`. Hover is opt-in,
because it means an event, and usually a frame, for every cell the pointer crosses.

### 17.2 Public API

```csharp
public sealed record MouseEvent(...)
{
    // Existing members unchanged

    /// For a press: 1, or 2 and 3 for the second and third press of a double- or triple-click (§17.3).
    /// 0 for every other action.
    public int ClickCount { get; init; }
}

public enum MouseAction
{
    // Existing members unchanged

    /// The pointer moved with no button held. Only while App.WithHover is on (§17.5).
    Move,
}

public class App
{
    /// The longest time between the presses of a double-click. 500 ms by default; ArgumentOutOfRangeException
    /// outside 100 ms to 2 s.
    public App WithDoubleClickTime(TimeSpan interval);

    /// Ask for all-motion reports (?1003) and show hover (§17.5). Turns the mouse on too. Off by default.
    public App WithHover(bool enabled = true);

    /// Where the pointer was last reported, or null before the first report.
    public Vec2? PointerPosition { get; }
}

public sealed class RenderContext
{
    // Existing members unchanged

    /// The pointer, while hover is on and the pointer is over this component (§17.6). Null otherwise.
    public Vec2? Hover { get; }
}

public class SelectionState
{
    /// A row was double-clicked (§17.4). The row is Selected.
    public event EventHandler? Activated;
}

// List, Table, Tree, Button, Checkbox and TabBar builders:
//     public XBuilder Hover(Vec2? position);   // usually ctx.Hover (§17.6)

public sealed record Theme
{
    /// What's under the pointer: rows, buttons, checkboxes and tabs (§17.6). Every built-in theme sets it.
    public Style Hover { get; init; }
}
```

- `RenderContext.Hover` and `App.PointerPosition` aren't called `Pointer`: the analyzers reject a member named
  after a type (CA1720).

### 17.3 Counting clicks

- The app counts presses before routing them, so `App.OnMouse`, `App.OnEvent` and every component see the same
  `ClickCount`. A press counts on from the last one when it's the same button, in the same cell, and within the
  double-click time of it; otherwise it's 1. After 3 the count starts at 1 again, so a fourth quick press is a
  new single click.
- Times come from the app's `TimeProvider`, so tests can step the clock.
- Windows marks the second press with `DOUBLE_CLICK`, with its own timing (§10.12). It's treated as a plain press and
  counted the same way, so all platforms agree.
- Presses still do what they did. A double-click is a press with count 1 followed by a press with count 2, so a
  component that reacts to presses reacts to both, as GUIs do. A double-click acts on top of what the first press
  did (it selected the row, then the second press opens it).
- Presses injected through `ComponentHarness.Click` and `AppHarness` are counted too. `ComponentHarness`
  gets `DoubleClick(x, y)`, two presses and releases in a row without moving the clock.

### 17.4 What double-click does

| Where | Count 2 | Count 3 |
|---|---|---|
| Text field, text area, command line | Select the word under the pointer: a run of letters, digits and `_`, or of other non-space characters, or of spaces. Clusters are never split. | Select the line (text area: the line, without its line break) or all the text (text field). |
| List and table rows (`SelectionState`) | Raise `Activated` for the row. | As count 2 (not raised again). |
| Tree rows | A leaf raises `Activated`; a parent expands or collapses, as Enter does. A double-click on the expander toggles it only once. | Nothing more. |

- Dragging after a double- or triple-click extends the selection by characters, as after a single press. Extending
  by words and lines is out of scope (§17.10).
- The demos: a double-click on a file in the Tree demo shows its path (the demo's `Activated` handler, which Enter
  already ran), one on a log row in the Panes demo moves focus to its details, and one on a word in the Form demo
  selects it. The menu screen already opens a demo on a single click.

### 17.5 Hover: the reports

- `WithHover` turns on `?1003h` (any-event tracking) instead of `?1002h` on Unix and in Windows Terminal, and
  `?1003l` on every restore path. In conhost, `MOUSE_MOVED` records without a button become `Move` events instead of
  being dropped.
- The SGR and X10 decoders turn a motion report with no button (button 3 + 32) into `MouseAction.Move`, with
  `MouseButton.None`. Without `WithHover` they're still dropped, so a terminal that sends them anyway changes
  nothing.
- Every report (presses, drags and moves) sets `App.PointerPosition`. Terminals don't report the pointer leaving the window,
  so hover stays where the pointer last was until it comes back. Focus reports (`?1004`) to clear it are out of
  scope.
- **Routing.** A move goes to `App.OnEvent` and `App.OnMouse` first, then to the component under the pointer and
  its ancestors, without click to focus. Built-in components use moves only as §17.7 says; others ignore them,
  as they ignore drags.
- **Frames.** A move requests a redraw only when the hovered component changes or that component is hover-aware
  (it called `ctx.Hover` in its last render). Moves already waiting in the queue are coalesced: only the last one
  is routed. The frame diff then sends only the cells that changed.

### 17.6 Hover: drawing

- `ctx.Hover` is the pointer's position for the component under the pointer in the last frame's hit list (the
  topmost one, as for clicks) and for each of its ancestors, and null for everything else. An overlay above a
  component hides the hover from it. It's null without `WithHover`.
- Builders take it with `.Hover(ctx.Hover)` and decide what's under it:
  - **List, Table and Tree:** the row under the pointer is patched with `Theme.Hover`, under the selection, so the
    selected row stays the selection color.
  - **Button and Checkbox:** the whole widget, when the pointer is inside its area.
  - **TabBar:** the tab under the pointer, unless it's the active one.
- **Built-in themes:** `Hover` is a subtle background in the dark (`#2A303B`) and light (`#E8ECF0`) themes, and
  underlined in the default theme (it uses the terminal's 16 colors, and none is a subtle background) and in
  monochrome.

### 17.7 Hover in menus and the menu bar

- **Popup menus and submenus:** moving over an enabled item selects it, as in GUI menus, so Enter or a click
  chooses it. Moving over a submenu item opens nothing; Enter, → or a click does (§15.4). Moving off the menu
  leaves the selection where it was.
- **Menu bar:** while one of its menus is open, moving over another title switches to that menu, as F10's ← → do.
  With no menu open, a title under the pointer gets `Theme.Hover`.
- **The palette and filterable select** (`FilterList`): moving over a row selects it, as in the menus.

### 17.8 Tests and acceptance

Automated:
- **Counting:** counts 1, 2 and 3 and back to 1; a different cell, a different button, and a gap longer than the
  double-click time each reset it; `WithDoubleClickTime` limits; `ClickCount` 0 on non-press actions; Windows
  `DOUBLE_CLICK` records counted like any press; the count seen by `App.OnMouse` and by components.
- **Text states:** word selection over letters, punctuation, spaces, surrogate pairs and combining marks, at a
  line's end and on an empty line; triple-click lines and all; a drag after a double-click.
- **Lists, tables and trees:** `Activated` once per double-click and not on single clicks; tree leaves and parents;
  the expander toggled once.
- **Hover decoding:** SGR and X10 motion without a button with and without hover; conhost `MOUSE_MOVED`; `?1003h`
  and `?1003l` in the mode strings and every restore path.
- **Hover routing:** `ctx.Hover` for the target and its ancestors, null under an overlay and without hover;
  redraws only when needed; coalesced moves.
- **Drawing:** the hover row in a list, table and tree under and beside the selection; buttons, checkboxes and tabs;
  snapshots in each built-in theme.
- **Menus:** moving selects items, skipping disabled ones and separators; the bar switches menus while one is open
  and shows hover while none is; the palette's rows.

Manual QA (results in the PR): in Windows Terminal, conhost, a Linux terminal and tmux, double-click a word in the
Form demo, double-click demos and tree leaves, and with hover on in the example, move over the menus, the menu bar,
the demo list and the buttons in the Widgets demo.

### 17.9 Migration impact

- New public API (§17.2) in `PublicAPI.Unshipped.txt`.
- **Changed:** a double-click in a text field or text area selects a word (and a triple-click the line), where the
  second press used to just place the cursor again. Apps with their own `OnMouse` see `ClickCount` on presses, and,
  with `WithHover`, `Move` events.
- README: double-click, `WithHover`, `ctx.Hover` and `.Hover()`, and `Theme.Hover`.

### 17.10 Out of scope

- Extending a selection by words or lines when dragging after a double- or triple-click.
- Hover tooltips, and hover effects in widgets other than those in §17.6 and §17.7 (paragraphs, charts, the
  split divider).
- Mouse cursor shapes (OSC 22).
- Focus reports (`?1004`) and clearing hover when the pointer leaves the window.
- Reading the system's double-click time (Windows `GetDoubleClickTime`, desktop settings on Linux).
- Double-click on buttons, checkboxes and tabs (a second press acts as a second click, as before).

### 17.11 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Hover opt-in (`WithHover`) or on whenever the mouse is? | **Opt-in** (§17.5): moves cost an event each, tmux and SSH links carry them all, and many apps don't need hover. |
| 2 | Does hover in a menu move the selection (GUI menus) or only highlight (keeping the keyboard selection)? | **Move it** (§17.7): one highlight, and Enter does what the pointer shows. |
| 3 | Lists and tables: a double-click raises a new `SelectionState.Activated`, or the app reads `ClickCount` itself? | **`Activated`** (§17.4), like `TreeState.Activated`; `ClickCount` is there for anything else. |
| 4 | The default theme's hover: underline, or the `DarkGray` background (which `SelectionUnfocused` already uses)? | **Underline** (§17.6), so hover is never mistaken for a selection. |

### 17.12 Implementation order

Two PRs, like Phase 22:
1. **23a** (double-click): `ClickCount` and the counting in the app, `WithDoubleClickTime`, `ComponentHarness.DoubleClick`;
   word and line selection in the text states; `SelectionState.Activated` and the tree's double-click; the demos;
   README; CHANGELOG.
2. **23b** (hover): `MouseAction.Move`, `?1003`, the decoders and conhost; `WithHover`, `App.PointerPosition`, routing,
   coalescing and redraws; `ctx.Hover`, `.Hover()` and `Theme.Hover`; menus, the menu bar and the palette;
   the example turns hover on; README; CHANGELOG; the manual QA.

Both go out in one release (1.12.0).

---

## 18. Tree extras (Phase 24)

### 18.1 Purpose

The 1.6 tree (§11) browses a hierarchy, but file pickers, settings trees and anything with more than a screenful
of nodes need more. Today an app can't jump to a node it knows the path of, can't narrow a big tree to what
matches, can't let the user pick several nodes, and has to write its own loading logic for slow children (§11.7).
Phase 24 adds these (backlog rank 8, §1.1):
- **Reveal:** expand the ancestors of a node and select it, from its path.
- **Filter:** show only the nodes that match a predicate and their ancestors, opened to show them.
- **Checkboxes:** a check mark on each row, toggled with Space or a click, with tri-state parents.
- **Async children:** a loader that returns a `Task`, with "Loading…" shown while it runs and errors shown on the
  row.

### 18.2 Public API

```csharp
public sealed class TreeState<T> where T : class
{
    // Existing members unchanged

    /// Expand each node of path but the last, select the last and scroll it into view (§18.3).
    /// path starts at a root. Returns false, changing nothing, when it doesn't match the tree.
    public bool Reveal(IReadOnlyList<T> path);

    /// Reveal, after loading the children of each node on the path that aren't loaded (§18.3, §18.6).
    public Task<bool> RevealAsync(IReadOnlyList<T> path, CancellationToken cancellationToken = default);

    /// Show only nodes that match and their ancestors (§18.4). Null shows everything.
    public Func<T, bool>? Filter { get; set; }

    /// Draw a check box on each row; Space and a click on it toggle it (§18.5). Off by default.
    public bool Checkboxes { get; set; }

    /// Whether a node is checked: true, false, or null for a parent with some descendants checked (§18.5).
    public bool? IsChecked(T node);

    /// Check or uncheck a node and its descendants.
    public void SetChecked(T node, bool isChecked);

    /// The checked nodes: every checked leaf, and every parent whose descendants are all checked.
    public IReadOnlyList<T> CheckedNodes { get; }

    /// Raised when the user (Space, a click) or SetChecked changes a check.
    public event EventHandler<TreeNodeEventArgs<T>>? CheckedChanged;

    /// Forget a node's loaded children, so the next expand loads them again (§18.6).
    public void Reload(T node);
}

public static class TreeState
{
    /// A state whose children are loaded asynchronously (§18.6), instead of by a children function.
    public static TreeState<T> Async<T>(Func<T, CancellationToken, Task<IReadOnlyList<T>>> loadChildren,
        Func<T, string> text, IEqualityComparer<T>? comparer = null) where T : class;
}
```

- The async state comes from a factory, not a second constructor: the analyzers reject two overloads with
  optional parameters (RS0026, RS0027), and a static member on the generic type (CA1000).

- Everything is opt-in: a tree that uses none of these behaves as in 1.6.
- No builder changes: the filter, the check boxes and the loading text are drawn by `TreeBuilder<T>` from the
  state.

### 18.3 Reveal

- `Reveal(path)` checks that `path[0]` is a root and each later node is among the previous node's children (with
  the state's comparer, reading children as the walk does). If one isn't, it returns false and changes nothing.
- Otherwise it expands every node but the last (raising `Expanded` for each one that wasn't), selects the last,
  raises `SelectionChanged` if it moved, and scrolls the selection into view at the next draw, as a key move does.
- With a lazy source (§11.7), a node's children may not be known yet. Reveal walks what's known, so the app loads
  the path first. With async children (§18.6), `RevealAsync` loads each node on the path that isn't loaded (sharing
  a load already running), then reveals; the app names the nodes before they load with a comparer by key (a path).
  It was added during 24b, when the demo showed an app has no other way to wait for the state's loads.
- With a `Filter` set, revealing a node the filter hides returns false.

### 18.4 Filter

- **Rows while filtering:** a node is shown if `Filter` matches it or any of its descendants. The walk reads the
  children of every node, expanded or not, so that collapsed parents can be searched. With a lazy source it
  searches what `children` returns now (what's loaded). Cycles are cut as in §11.4.
- **Open and closed while filtering:** every shown node with shown children is drawn open, whatever the expanded
  set says, so the matches are visible. ← and a click on the expander can still close one while the filter is
  set. That's kept in a separate set, cleared whenever `Filter` changes, so the expanded set is never touched.
- **Clearing the filter** (`Filter = null`) returns to the expanded set. If the selected node isn't visible
  any more, its nearest visible ancestor is selected (§11.4), so the user lands where they were.
- **Type-ahead** and the keys work on the filtered rows. The title's `[n]` counts the filtered rows.
- **Matching text:** the filter is a predicate, so the app decides how to match. The README shows a text field
  driving `Filter = n => n.Name.Contains(text, StringComparison.OrdinalIgnoreCase)`, and `Fuzzy.Match` (§13)
  works too. Highlighting the matched characters is out of scope.
- **Cost:** the walk is linear in the whole tree (not just the open part) each time the rows are computed. That's
  fine for thousands of nodes. The README says to keep filtered trees in memory.

### 18.5 Checkboxes

- **Drawing:** with `Checkboxes`, each row gets `[✓] `, `[ ] ` or `[-] ` (partly checked) after the expander,
  before the label. In basic glyph mode (§11.5) the check is `x`. The mark uses `Theme.Success`, as `Checkbox` does.
- **State:** the state keeps the set of checked nodes. A parent's mark is computed from its children: all checked
  is checked, none is unchecked, anything else is partly checked. A parent with no children (or none loaded) is a
  leaf for this. So the marks read children of collapsed parents, as the filter does.
- **Toggling:** Space on the selected row, or a left press on its check box, checks the node if it was unchecked
  or partly checked, and unchecks it if it was checked. Checking or unchecking a node applies to it and all its
  descendants. `CheckedChanged` is raised once, for the node toggled.
- **`CheckedNodes`** lists, in tree order, the checked leaves and the parents that are fully checked, so an app
  can read "these folders and these files" without listing everything under a folder. Nodes outside the tree
  (removed since they were checked) are dropped from it.
- Space is handled only with `Checkboxes` on. Without it, Space still reaches the bindings (§11.3).
- Filtering doesn't change checks: checking a parent while filtering checks all its descendants, shown or not.

### 18.6 Async children

- **The loader:** with `TreeState.Async`, a node's children come from `loadChildren(node, token)`. They're loaded
  when the node is first expanded (by any means: a key, a click, `Expand`, `Reveal`, `ExpandAll`) and cached.
  Until it finishes, the node has no child rows, and its label is followed by `Loading…` in `Theme.TextMuted`.
  When it finishes, the next frame shows the children.
- **`HasChildren`** defaults to true for unloaded nodes with a loader (it can't be known without loading), and to
  whether the loaded children are empty afterwards. An app that knows better (files are leaves) sets `HasChildren`.
- **Errors:** a load that throws shows the exception's message after the label in `Theme.Error`, and the node
  collapses. Expanding it again retries. A cancelled load shows nothing.
- **Threads:** loads run through `App.RunTask` when an app is running (so `ComponentHarness.RunUntilIdle` waits for
  them, and they're cancelled when the app stops), and their results are stored on the UI thread. Without an app,
  the task is awaited on the caller's context.
- **`Reload(node)`** drops the cached children of a node (not its descendants' caches, which stay until they're
  reloaded or the node is reached again), cancels its load if one is running, and loads again at the next expand,
  or at once if the node is expanded.
- **Filters, checks and reveal** see only loaded children (§18.3 – §18.5). `ExpandAll` with a loader expands what's
  loaded and starts loads for the expanded nodes that aren't. It doesn't wait for them, or expand what they bring.
- A parent checked before its children load checks them when they arrive.

### 18.7 Example

The Tree demo:
- A **Filter** field above the tree (Tab moves between them). Typing shows the loaded files and folders whose
  names contain the text.
- **Ctrl+G** ("go to") reveals `src/MiniTui/App/App.cs` when the demo runs from the repository, loading the folders on the
  way, and shows a toast otherwise.
- **Check boxes** with Alt+X, and the footer shows how many files and folders are checked.
- Directories load with the async loader, with a short delay so `Loading…` can be seen.

### 18.8 Tests and acceptance

Automated:
- **Reveal:** a deep path expands its ancestors (events once each) and selects and scrolls to it; a path that
  doesn't match, a wrong root and an empty path return false with no change; under a filter.
- **Filter:** matches at several depths with their ancestors open; collapsed parents searched; the expanded set
  untouched; closing a parent while filtering, and that set cleared on a new filter; clearing the filter keeps the
  selection or selects the nearest ancestor; type-ahead, keys and the title count on filtered rows; cycles.
- **Checkboxes:** Space and clicks; parents checked, unchecked and partly checked; toggling a partly checked parent
  checks it; `CheckedNodes` in tree order with full parents and without their descendants; `CheckedChanged` once;
  Space unhandled without `Checkboxes`; drawing in both glyph modes and with a highlight symbol.
- **Async:** `Loading…` while a load runs and the children after (through `ComponentHarness`); one load per node;
  errors shown and retried; cancellation when the app stops; `Reload` while collapsed and while expanded;
  `HasChildren` defaults; `ExpandAll` starting loads.

Manual QA (results in the PR): the Tree demo on Linux and Windows: filter, reveal, check boxes with keys and the
mouse, and loading a large directory.

### 18.9 Migration impact

- New public API (§18.2) in `PublicAPI.Unshipped.txt`. No behavior changes for existing trees.
- README: "### Trees" gets reveal, the filter with a text field, check boxes and async loading (replacing the
  hand-written loader in §11.7's example).

### 18.10 Out of scope

- Multi-select by Shift and Ctrl clicks or keys (check boxes are the multi-select).
- Highlighting the matched text in a filtered row.
- A filter box drawn by the tree itself.
- Drag and drop, editing, sorting, a tree table, icons (§11.10).
- Paging or a limit for the filter's walk over huge trees.
- Persisting the expanded and checked sets.

### 18.11 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Reveal: from a path of nodes, or from a node with a `Parent` function the app supplies? | **A path** (§18.3): the state has no parent function, and apps with paths (files, settings keys) have them already. |
| 2 | Filter: a predicate, or a string the tree matches (substring or fuzzy) and can highlight? | **A predicate** (§18.4): apps match on what they like (names, tags, paths). Highlighting is out of scope. |
| 3 | Checkboxes: tri-state parents computed from children, or independent checks per node? | **Tri-state** (§18.5), as file pickers and installers do. |
| 4 | Async loading: show `Loading…` after the parent's label, or as a placeholder row under it? | **After the label** (§18.6): no row that can't be selected, and nothing moves when it's replaced. |

### 18.12 Implementation order

Two PRs, like Phase 23:
1. **24a** (reveal and filter): `Reveal`, `Filter` and the filtered rows; the demo's filter field and Ctrl+G; README;
   CHANGELOG.
2. **24b** (checkboxes and async): `Checkboxes`, `IsChecked`, `SetChecked`, `CheckedNodes`, `CheckedChanged`; the
   async constructor, loading, errors and `Reload`; the demo's check boxes and async loading; README; CHANGELOG; the
   manual QA.

Both go out in one release (1.13.0).

---

## 19. Chart extras (Phase 25)

### 19.1 Purpose

The 1.4 charts (§8) cover a single series of bars and line or scatter plots with three numeric labels per axis.
Dashboards want more (backlog rank 9, §1.1):
- **Grouped and stacked bars:** several values per category, side by side or on top of each other, with a legend.
- **Time axes:** an X axis of timestamps or durations, labelled `12:05:30` or `-30s` instead of raw numbers.
- **Tick marks and grid lines:** marks on the axes at the labels, and faint lines across the plot behind the data.
- **Area charts:** a line with the space under it filled.

### 19.2 Public API

```csharp
/// Several bars under one label: side by side, or stacked into one bar (§19.3).
public sealed record BarGroup(string Label, IReadOnlyList<Bar> Bars);

public sealed class BarChartBuilder
{
    // Existing members unchanged

    /// Draw groups instead of single bars. Each group's bars are its series: bar i of every group is series i.
    public BarChartBuilder Groups(IReadOnlyList<BarGroup> groups);
    public BarChartBuilder Groups(params BarGroup[] groups);

    /// Stack each group's bars into one bar instead of drawing them side by side. Off by default.
    public BarChartBuilder Stacked(bool stacked = true);

    /// The space between groups, in cells. Default 2. BarGap stays the space between bars in a group.
    public BarChartBuilder GroupGap(int gap);

    /// Where the series legend goes, for groups. Default TopRight; None hides it.
    public BarChartBuilder Legend(LegendPosition position);
}

public enum GraphType
{
    Line,
    Scatter,
    /// A line with the dots under it filled down to the X axis (§19.6).
    Area,
}

public sealed record Axis
{
    // Existing members unchanged

    /// How many labels to spread along the axis when Labels is null. Default 3; at least 2.
    public int LabelCount { get; init; }

    /// Formats a label's value, instead of LabelFormat. Null uses LabelFormat.
    public Func<double, string>? Formatter { get; init; }

    /// Draw a tick mark on the axis line at each label (§19.5). Off by default.
    public bool Ticks { get; init; }

    /// Draw a grid line across the plot at each label, behind the data (§19.5). Off by default.
    public bool Grid { get; init; }

    /// An axis whose values are times: seconds since the Unix epoch (ChartTime), labelled in local time with a
    /// DateTime format (§19.4).
    public static Axis Time(string format = "HH:mm:ss");

    /// An axis whose values are durations in seconds, labelled with a TimeSpan format, with "-" for negatives (§19.4).
    public static Axis Duration(string format = @"m\:ss");
}

/// Converting times to and from chart values: seconds since the Unix epoch (§19.4).
public static class ChartTime
{
    public static double ToValue(DateTimeOffset time);
    public static DateTimeOffset FromValue(double value);
    public static double ToValue(TimeSpan duration);    // seconds
}
```

- Everything is additive: a chart or bar chart that sets none of these looks as it does in 1.4.
- `Bars` and `Groups` replace each other: the last one set is drawn.

### 19.3 Grouped and stacked bars

**Grouped (side by side):**
- Each group is its bars `BarWidth` wide with `BarGap` between them, and `GroupGap` between groups. The group's
  label is centered under the group (truncated with `…` to the group's width). Bar labels aren't drawn under the
  bars: they name the series, in the legend.
- A bar without its own `Style` takes `Theme.ChartSeries[i]`, its index in the group (wrapping around), so series
  have the same color in every group. Value texts are drawn as for single bars, when they fit.
- **Horizontal:** each group is its bars on consecutive rows, `GroupGap` empty rows between groups; the group's
  label is on the first bar's row, in the label column.
- `Max` (or the largest bar in any group) is a full bar.

**Stacked:**
- Each group is one bar, `BarWidth` wide, made of its bars' segments from the bottom up (from the left when
  horizontal), in order. `Max` defaults to the largest group total. Stacked bars are spaced as single bars are,
  by `BarGap`, and labelled as single bars are, with the group's label.
- **Segment boundaries:** each segment ends at the nearest whole cell (by its cumulative total), so a cell is
  never split between two colors; only the top of the stack uses eighths. A segment that rounds to zero cells
  isn't drawn, but still counts in the total.
- The value text is the group's total (formatted with `ValueFormat`) in the top segment's color reversed, in the
  bar's top cell when the bar is at least one cell tall, and when it fits. Negative and NaN values count as 0.

**The legend** (groups only): a bordered box in the chosen corner, as `Chart`'s (§8.4), one row per series: `█`
in the series' style, then the label of that bar in the first group. It's drawn over the bars, and left out
when it would cover more than half the area's width or height, or when no first-group bar has a label.

### 19.4 Time axes

- **Values:** chart data stays `double`. `ChartTime.ToValue` turns a `DateTimeOffset` into seconds since the Unix
  epoch, so a dataset of timestamps is `(ChartTime.ToValue(t), value)`. Durations are seconds
  (`ChartTime.ToValue(span)` or a plain number), which suits sliding windows ("the last 60 s" as −60 .. 0).
- **`Axis.Time(format)`** sets `Formatter` to the local time of the value, formatted with `format` (invariant
  culture). **`Axis.Duration(format)`** formats `TimeSpan.FromSeconds(|v|)` with `format`, with `-` before
  negatives. Both are ordinary `Axis` values, so `with { Title = "Time", Min = ... }` works on them.
- **Labels** are still spread evenly between the bounds (`LabelCount` of them). Rounding them to "nice" times
  (whole minutes) is out of scope (§19.9); an app that wants that sets `Labels` itself.
- The Y label column's width and the X labels' spreading use the formatted text, as now.

### 19.5 Tick marks and grid lines

- **Positions:** at each label position, as `Chart` spreads them now (§8.4): with `Labels` set, one per label; with
  `LabelCount`, that many.
- **Ticks** (`Ticks`): on the X axis line, `┬` at each tick's column (`┴` would point into the plot); on the Y axis
  line, `┤` at each tick's row. The corner stays `└`. In `Theme.Border`.
- **Grid** (`Grid`): across the plot at each tick, `┈` rows for a Y axis grid and `┊` columns for an X axis grid,
  `┼` where they cross, in `Theme.Border` patched with `Theme.TextMuted`. Grid lines are drawn before the datasets,
  and a cell with any dot shows the dot, so the data is always on top. The first tick's grid line on each axis
  isn't drawn: it's beside the other axis line (the plot's first column, or its bottom row).
- Basic glyph mode keeps the box-drawing characters, as borders do.

### 19.6 Area charts

- `GraphType.Area` draws the line as `Line` does, then fills every dot column under it down to the bottom of the
  plot (the Y axis' minimum), with the same marker and style. Between two points, the line's height at each dot
  column is interpolated, so the fill follows the line.
- Several area datasets draw in order, so later ones cover earlier ones; drawing the larger series first keeps
  both visible. The README says so.
- Points outside the bounds clip the fill at the plot's edges; NaN and infinite points break the area, as they
  break a line.
- The legend's sample for an area dataset is `█` (`⣿` for braille), as for a line.

### 19.7 Example

The Charts demo:
- **Line chart:** the X axis becomes a duration axis (`Axis.Duration()`, −10 s .. 0) with ticks and a Y grid;
  `a` toggles the datasets between `Line` and `Area` (as `s` toggles `Scatter`).
- **Bar chart:** `g` cycles single, grouped and stacked: requests per service split into GET, POST and DELETE as
  groups, with a legend.
- The footer lists the new keys.

### 19.8 Tests and acceptance

Automated (buffer snapshots):
- **Grouped:** layout with `BarWidth`, `BarGap` and `GroupGap`; group labels; series colors from the theme and a
  bar's own style; value texts; horizontal; groups that don't fit are dropped; `Max`.
- **Stacked:** segments rounding to whole cells, the top in eighths, zero-cell segments, totals as value text,
  `Max` from the totals, negatives and NaN as 0, horizontal.
- **Legend:** each corner, `None`, too big, no labels.
- **Axes:** `LabelCount`; `Formatter` over `LabelFormat`; `Axis.Time` and `Axis.Duration` (negatives, hours) with
  a fixed time zone; `ChartTime` round trips.
- **Ticks and grid:** positions with `Labels` and with `LabelCount`; the corner; grid under the data; crossings;
  the edges left to the axes.
- **Area:** the fill under a rising and a falling line, interpolation between points, clipping at the edges, NaN
  gaps, two overlapping areas, each marker.
- No widget throws for any area size, with and without groups.

Manual QA (results in the PR): the Charts demo in Windows Terminal, conhost (half blocks) and a Linux terminal.

### 19.9 Out of scope

- Negative bars below a baseline, and bars on a canvas.
- "Nice" tick steps (round numbers, whole minutes) and logarithmic or category axes.
- A second Y axis, and annotations (markers, bands).
- Filling between two datasets, or down to a baseline other than the axis minimum.
- Hover and interaction (a cursor, tooltips), and caching between frames (§8.11).

### 19.10 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Time values: keep `double` data with `ChartTime` to convert (Unix seconds), or add a `DateTime` dataset type? | **`double` with `ChartTime`** (§19.4): one data type, and durations and times share an axis kind. |
| 2 | Stacked segment boundaries: whole cells (never two colors in a cell), or eighths with the cell's background in the next segment's color? | **Whole cells** (§19.3): simpler, and correct in every terminal; the top still uses eighths. |
| 3 | Grid lines: dotted box-drawing (`┈ ┊`) behind the data, or braille dots on the canvas? | **Box-drawing behind the data** (§19.5): readable in every glyph mode, and never mixed into a series' dots. |
| 4 | Grouped bars' colors: by series index from `Theme.ChartSeries`, or `Theme.GaugeFilled` as single bars use? | **By series** (§19.3): otherwise the series can't be told apart. Single bars keep `GaugeFilled`. |

### 19.11 Implementation order

Two PRs:
1. **25a** (bars): `BarGroup`, `Groups`, `Stacked`, `GroupGap` and the bar chart legend, both directions; the demo's
   `g`; README; CHANGELOG.
2. **25b** (axes and areas): `LabelCount`, `Formatter`, `Axis.Time`, `Axis.Duration`, `ChartTime`, `Ticks`, `Grid`
   and `GraphType.Area`; the demo's duration axis, grid and `a`; README; CHANGELOG; the manual QA.

Both go out in one release (1.14.0).

---

## 20. Clipboard and selection extras (Phase 26)

### 20.1 Purpose

Phase 16 (§10) left a few clipboard and selection cases out (§10.12), and they're the last item on the backlog
(rank 10, §1.1):
- **Reading the system clipboard on Unix.** Ctrl+V pastes only what the app itself copied, because reading the
  system clipboard needs an OSC 52 query and a reply parser. Phase 18 (§12) built the parser.
- **The Insert keys:** Ctrl+Insert (copy), Shift+Insert (paste) and Shift+Delete (cut), the IBM CUA keys that
  Windows and many terminal users still reach for.
- **Selecting text that isn't editable:** paragraphs, such as a log or a detail pane. Terminals' own Shift+drag
  selects screen text, but it takes borders and neighbouring panes with it.

### 20.2 Public API

```csharp
public static class Clipboard
{
    // Existing members unchanged

    /// The system clipboard's text where it can be read (§20.3): on Windows the Win32 clipboard; on Unix, with
    /// App.WithClipboardRead, an OSC 52 query to the running app's terminal. Otherwise, or when the terminal doesn't
    /// answer in time, GetText().
    public static Task<string?> GetTextAsync(CancellationToken cancellationToken = default);
}

public class App
{
    /// Ask the terminal for its clipboard (OSC 52) when Ctrl+V or Shift+Insert pastes on Linux and macOS (§20.3).
    /// Off by default: many terminals refuse, and some ask the user each time.
    public App WithClipboardRead(bool enabled = true);
}

public abstract class Component
{
    /// True while the component has something selected to copy. Ctrl+C and Ctrl+Insert go to its HandleKey first,
    /// as they do for a component that captures text (§10.4, §20.5).
    public virtual bool HasSelection => false;
}

public class ScrollState
{
    // Existing members unchanged

    /// Let the mouse select the text: a press and drag select, a double-click a word, a triple-click a row (§20.5).
    /// Off by default.
    public bool Selectable { get; set; }

    public bool HasSelection { get; }
    public string SelectedText { get; }      // the selected part of the rows, a line break between rows
    public void SelectAll();
    public void ClearSelection();

    /// Copy the selection to the Clipboard. False, doing nothing, without one.
    public bool Copy();
}
```

- `ScrollState.HandleKey` also takes Ctrl+C and Ctrl+Insert (copy, only with a selection) and Ctrl+A (select all)
  when `Selectable`, and Esc clears a selection (unhandled without one, so it still goes back).
- `ParagraphBuilder` draws the selection with `Theme.Selection` (or `SelectionUnfocused`) when its state has one.

### 20.3 Reading the clipboard on Unix

- **The query:** with `WithClipboardRead`, `GetTextAsync` writes `ESC ] 52 ; c ; ? BEL` to the running app's
  terminal (under the backend's lock, as writes are, §10.5), then waits for the reply.
- **The reply:** `ESC ] 52 ; c ; <base64> BEL` (or ST). The decoder turns it into an internal reply, never keys,
  like the Phase 18 replies (§12.3). The base64 is decoded as UTF-8; the text is returned as is (a paste then
  normalizes it).
- **When it fails:** an empty reply (some terminals answer that way when they refuse), invalid base64, or no reply
  within 500 ms all give `GetText()`, the in-app text. After a timeout, the app stops asking for the rest of the run,
  so a terminal that ignores the query costs one delay, not one per paste. A refusal is answered quickly, so it
  doesn't stop the asking.
- **Where it works:** xterm with `allowWindowOps` (or `disallowedWindowOps` without `GetSelection`), kitty (asks the
  user unless `clipboard_control` allows `read-clipboard`), WezTerm, foot, iTerm2 (with "Applications in terminal
  may access clipboard"), and tmux 3.3+ with `set -s set-clipboard on`. GNOME Terminal and Windows Terminal (through
  WSL) don't answer. The README lists these.
- **Ctrl+V and Shift+Insert in the text states:** when the app reads the clipboard asynchronously, the key starts
  the read (through `App.RunTask`) and returns true; the paste happens when the text arrives, at the cursor and
  selection as they are then. Otherwise it pastes at once, as today.
- **Windows:** `GetTextAsync` is the Win32 read, as `GetText` is; `WithClipboardRead` changes nothing there.
- **Without an app** (a state used in a test): `GetTextAsync` is `GetText()`.

### 20.4 The Insert keys

| Key | In a text field, text area or command line | In a selectable paragraph |
|---|---|---|
| Ctrl+Insert | Copy the selection (as Ctrl+C; unhandled without one, but it never quits) | Copy the selection |
| Shift+Insert | Paste (as Ctrl+V) | — |
| Shift+Delete | Cut the selection; without one, delete the next cluster, as Delete | — |

- The decoder already reports Insert and Delete with modifiers (`CSI 2 ; m ~`, `CSI 3 ; m ~`, §2.5), and Windows
  reports them as keys. Many terminals take Shift+Insert for their own paste (xterm pastes the primary selection,
  Windows Terminal its clipboard): then it arrives as a paste, which the states handle the same way.
- These are a "Changed" entry: Ctrl+Insert, Shift+Insert and Shift+Delete used to be unhandled by the text states
  (Shift+Delete deleted a cluster).

### 20.5 Selecting paragraph text

- **Positions** are cells of the rows the paragraph drew last (after wrapping): a row and a display column.
  Wide characters are selected whole.
- **The mouse,** with `Selectable`: a left press in the text clears the selection and anchors a new one; a drag
  extends it to the pointer, scrolling a row at a time past the top or bottom, as in a text area (§10.7). A
  double-click selects the word under the pointer (`TextFieldState`'s word rule, §17.4), and a triple-click the row.
  The wheel and the scrollbar still scroll, and keep the selection.
- **`SelectedText`** is the selected part of each row, with `\n` between rows: the text as it's shown, so a
  wrapped line copies as several. Trailing spaces of a row are kept.
- **Drawing:** the selected cells get `Theme.Selection` when the paragraph's block is focused, or when it has no
  block (as in the dialogs, whose border is the overlay's); otherwise `Theme.SelectionUnfocused`, patched over the text.
- **When the text changes** (a log grows): the selection is kept by row and column, so it stays where it was on
  screen relative to the text, until the next press. If the rows it covered are gone, it's cleared.
- **Ctrl+C:** the §10.4 step now sends Ctrl+C (and Ctrl+Insert) to the focused component's `HandleKey` when it
  `CapturesText` or `HasSelection`. A component around a selectable `ScrollState` overrides
  `HasSelection => State.HasSelection`. With nothing selected, Ctrl+C still quits.
- **Built in:** the message and help dialogs' text is selectable, so an error message can be copied.
- **Esc in the text states:** as in a paragraph, Esc clears a selection in a text field, text area or command
  line (and the command line overlay), and goes on without one (§10.3). A "Changed" entry: Esc used to go on
  with the selection left in place.

### 20.6 Example

- The Dashboard demo's log panel becomes a selectable component: drag to select lines, Ctrl+C to copy, Ctrl+A for
  all, Esc to clear.
- The Form demo's footer mentions Shift+Insert and Ctrl+Insert.
- The example turns on `WithClipboardRead`.

### 20.7 Tests and acceptance

Automated:
- **OSC 52 replies:** decoding with BEL and ST, split across reads, invalid base64, empty, and never as keys.
- **`GetTextAsync`:** the query bytes; a reply resolves it; an empty reply and a timeout fall back to the in-app text;
  no more queries after a timeout; cancellation; without `WithClipboardRead`; without an app; Windows through the
  reader hook.
- **Ctrl+V and Shift+Insert with an async read** through `AppHarness`: the paste lands at the cursor when the reply
  arrives, and a selection made meanwhile is replaced.
- **Insert keys** in the three text states: copy without quitting, paste, cut, Shift+Delete without a selection.
- **Paragraph selection:** press, drag both ways, past the top and bottom, double- and triple-click, wide
  characters, `SelectedText` across rows, `SelectAll`, Esc, the wheel keeping it, text that grows and shrinks,
  drawing focused and unfocused, `Selectable` off.
- **Ctrl+C routing:** a component with `HasSelection` copies; without a selection the app quits; Ctrl+Insert never
  quits; the dialogs.

Manual QA (results in the PR): Ctrl+V reading the clipboard in kitty, WezTerm or foot, and in tmux with
`set-clipboard on`; the fallback in GNOME Terminal; the Insert keys in Windows Terminal and conhost; selecting the
Dashboard log with the mouse and pasting it elsewhere.

### 20.8 Migration impact

- New public API (§20.2) in `PublicAPI.Unshipped.txt`.
- **Changed:** the Insert keys in text states (§20.4); Ctrl+C reaches a focused component with `HasSelection`; the
  message and help dialogs handle a drag and a double-click (selecting) where they ignored them.
- README: `WithClipboardRead` and the terminals that support it, the Insert keys, and selectable paragraphs.

### 20.9 Out of scope

- X11's primary selection (select to copy, middle-click to paste), and copying on select.
- Keyboard selection in paragraphs (Shift+arrows need a cursor the paragraph doesn't have).
- Selecting across several components or the whole screen, and in lists, tables and trees.
- Copying a wrapped line as one line (the paragraph doesn't keep where it wrapped).
- Rich (HTML) or image clipboard content.

### 20.10 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | OSC 52 reads: opt-in (`WithClipboardRead`) or on by default? | **Opt-in** (§20.3): kitty and iTerm2 prompt the user, and terminals that ignore it cost a 500 ms delay once. The example turns it on. |
| 2 | How long to wait for the reply before falling back? | **500 ms**, and stop asking after one timeout. |
| 3 | Paragraph selection: copy rows as shown (a line break per row), or rejoin wrapped lines? | **As shown** (§20.5), as terminals copy; the paragraph doesn't keep where it wrapped. |
| 4 | Make the message and help dialogs selectable? | **Yes** (§20.5): copying an error message is the most common case. |

### 20.11 Implementation order

Two PRs:
1. **26a** (clipboard): the OSC 52 reply, `GetTextAsync`, `WithClipboardRead`, the async paste in the text states,
   the Insert keys; the example's `WithClipboardRead` and footer; README; CHANGELOG.
2. **26b** (selection): `Component.HasSelection` and the Ctrl+C step; `ScrollState` selection, keys and mouse;
   `ParagraphBuilder` drawing; the dialogs; the Dashboard log; README; CHANGELOG; the manual QA.

Both go out in one release (1.15.0).

---

## 21. Website (Phase 27)

### 21.1 Purpose

MiniTui's documentation is one 1,000-line README, which is also the package page on nuget.org. That's hard to
navigate, and there's no API reference outside IntelliSense. Phase 27 builds **minitui.org**: a landing page that
shows the library working, the README's content as guide pages, and an API reference generated from the XML doc
comments. It's built with DocFX and hosted on GitHub Pages. No library code changes, so no package release.

### 21.2 Layout

```
site/
  docfx.json              # metadata from src/MiniTui/MiniTui.csproj, build, the modern template
  index.md                # the landing page
  toc.yml                 # top navigation: Guides, API, Changelog, GitHub
  guides/
    toc.yml
    getting-started.md    # Install, Quick Start, A small app
    async.md              # Async data and HTTP
    screens.md            # Screens and components
    layout.md             # Layout, Split panes
    widgets.md            # Widgets, Box styles
    keys-and-state.md     # Keys and widget state, Trees, Selection and the clipboard
    theming.md
    drawing.md            # Drawing on the Buffer; Color, Style and text
    terminal.md           # The terminal: queries, keyboard, mouse, running programs, debugging, backends
    testing.md
    app-and-helpers.md    # The README's "Reference": App's options, Rect, ParsedCommand, TimedMessage, CommandRegistry
    extending.md
  changelog.md            # includes ../CHANGELOG.md
  images/                 # the logo and favicon
  media/                  # generated recordings and screenshots (§21.4), not committed
  template/public/main.css # the modern template's colors, and the landing page
tools/SiteMedia/          # the console app that records the demos (§21.4)
dotnet-tools.json         # docfx, pinned
```

- **The API reference** comes from `src/MiniTui/MiniTui.csproj` (`metadata` in `docfx.json`), so it has every
  public type with its XML docs; `<see cref>` links resolve to its pages. Internal types aren't shown.
- **Guides link to the API** with `<xref:MiniTui.App>`-style references, and the build fails on a broken one.
- **The guides are the README's sections**, split as above and edited where a page needs an introduction. The
  "Reference" section becomes "The app and helpers", since it has examples the API pages don't. "Upgrading from
  0.7" is `docs/MIGRATION-0.7-to-1.0.md` itself, published as `docs/` on the site, so the changelog's links to it
  work. "Project Structure" and "Building and Testing" stay in the README only (they're for contributors).
- **Search:** the modern template's built-in search (client-side, no service).
- The specs (`docs/SPEC*.md`) aren't published.

### 21.3 The landing page

- **Hero:** "MiniTui — terminal UIs for .NET", one sentence on what it is, `dotnet add package MiniTui`, and
  buttons to Get started, the API reference and GitHub.
- **A recording** of the example app (§21.4) under the hero: the menu, the Dashboard, the Editor, the Tree.
- **Features**, a short grid with a screenshot each: widgets (lists, tables, trees, charts, text areas),
  screens and focus, layout and split panes, the mouse and the clipboard, theming and light/dark detection,
  testing with `ComponentHarness`, and "no dependencies, Windows, Linux and macOS".
- **The smallest app**, the Quick Start's code, with a screenshot of what it draws.
- The footer: MIT license, NuGet, GitHub, the changelog.

### 21.4 Recordings and screenshots

Generated from the example app, not captured by hand, so they never drift from the code:

- **`tools/SiteMedia`** runs example screens on an in-memory ANSI backend (the real encoder, a fixed size, the
  default dark theme, `TimeProvider` stepped by the tool) and feeds them scripted input.
- **Recordings** are asciicast v2 files (`.cast`): the encoder's output with each frame's time. The site plays
  them with asciinema-player (from jsDelivr, a pinned version), so text stays sharp and selectable.
- **Screenshots** are SVGs drawn from the final buffer: one `<text>` run per style, a monospace font stack, the
  theme's colors.
- The scripts live in the tool (one per recording or screenshot). The site build runs the tool first, into
  `site/media/`.
- Randomness in the demos (the Dashboard's log and values) is seeded for the recordings, so the output is the same
  every run. The example takes its clock, random numbers and time of day from a `Demo` class the tool replaces, and
  its animations are timers on that clock rather than app tasks (the harness waits for app tasks to finish).
- The tool reaches MiniTui's ANSI encoder and the harness's backend (for the cursor) through `InternalsVisibleTo`,
  so the library's public API doesn't change.
- **What's recorded:** `tour.cast` (the menu, the Dashboard, the Editor's find bar, the Tree and the Widgets with a
  theme switch); a screenshot for each feature card except "Testable", which shows a test instead; and the Quick
  Start's counter, drawn by its render code into a buffer, without an app around it.

### 21.5 Building and deploying

- **`.github/workflows/site.yml`:** checkout, .NET 10, `dotnet tool restore`, build the library, run
  `tools/SiteMedia`, `docfx site/docfx.json --warningsAsErrors`, then `actions/upload-pages-artifact` and
  `actions/deploy-pages`.
- **When it deploys:** on a `v*` tag (so the API reference matches the released package) and on
  `workflow_dispatch` (for a guide fix between releases, built from `main`).
- **CI:** `ci.yml` builds the site on pull requests, without deploying, so a broken link or xref fails the PR.
- **The domain** is set in the repository's Pages settings (`minitui.org`, HTTPS enforced), with the source set to
  GitHub Actions.

### 21.6 What the maintainer sets up (outside the repository)

The repository moved from `discover-edge/minitui` to `minitui-org/minitui` before the site's first deploy, so the
Pages hostname is `minitui-org.github.io`. Published packages up to 1.15.0 link to the old address, which GitHub
redirects; nuget.org's trusted publishing policy names the new owner.

At the registrar for minitui.org:

| Type | Name | Value |
|---|---|---|
| A | `@` | `185.199.108.153`, `185.199.109.153`, `185.199.110.153`, `185.199.111.153` |
| AAAA | `@` | `2606:50c0:8000::153`, `2606:50c0:8001::153`, `2606:50c0:8002::153`, `2606:50c0:8003::153` |
| CNAME | `www` | `minitui-org.github.io` |
| TXT | `_github-pages-challenge-minitui-org` | the value GitHub shows when verifying the domain |

- **Verify the domain** for the minitui-org organization (Settings → Pages → Add a domain) first, so no other
  repository can claim it.
- In the repository: Settings → Pages → Source: GitHub Actions; Custom domain `minitui.org`; Enforce HTTPS once
  the certificate is issued (it can take up to an hour after DNS resolves).
- Settings → Environments → `github-pages` → Deployment branches and tags: add the tag rule `v*`, so a release
  tag can deploy (by default only `main` can).
- The repository's "Website" field: `https://minitui.org`. The package's `PackageProjectUrl` becomes the site.

### 21.7 README

The README shrinks to what a nuget.org or GitHub visitor needs first: the one-line pitch, install,
the Quick Start, a feature list linking to the guides, "Project Structure", "Building and Testing", and the
license. Everything else moves to the guides (§21.2), so it's written once. The package gets its own, shorter readme
(`src/MiniTui/PACKAGE.md`): the pitch, the Quick Start and links to the site, without the contributor sections. No
screenshot: the generated ones are
SVGs, which nuget.org doesn't show from other sites; the site has them.

### 21.8 Tests and acceptance

Automated:
- The site builds with `--warningsAsErrors`: no broken links, xrefs or includes.
- `tools/SiteMedia` produces the same files twice in a row (deterministic output).
- Every public type has an API page (DocFX's metadata covers the assembly's public surface).

Manual QA (results in the PR): the site served locally (`docfx serve`) in a desktop and a phone-width browser,
light and dark; the recording plays; search finds `ComponentHarness`; after DNS, `https://minitui.org` and
`https://www.minitui.org` both load with a valid certificate.

### 21.9 Migration impact

- No library changes. `PackageProjectUrl` changes in the next package release.
- The README shrinks (§21.7); its content moves to the site. The nuget.org page shows the shorter README from the
  next release.

### 21.10 Out of scope

- Versioned docs (a version picker): the site shows the latest release.
- A blog, a custom search service, analytics or cookies.
- Translating the docs.
- Running MiniTui in the browser (a WebAssembly terminal).

### 21.11 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Recordings: generated asciicasts, or GIFs recorded with a tool such as VHS? | **Generated asciicasts** (§21.4): sharp, selectable, small, and reproducible in CI without ffmpeg or a browser. |
| 2 | Deploy on every push to `main`, or on release tags? | **Release tags, plus manual runs** (§21.5), so the API pages match the package people install. |
| 3 | Shrink the README to a summary, or keep it whole alongside the site? | **Shrink it** (§21.7): one copy of each guide. Nobody depends on the README's anchors yet. |
| 4 | `www.minitui.org` or the bare domain as the main address? | **The bare domain**, `minitui.org`; `www` redirects to it (GitHub Pages does this when both are set up). |

### 21.12 Implementation order

Two PRs:
1. **27a** (the site): DocFX setup, the guides from the README, the landing page and template overrides, the
   changelog page, `site.yml` and the CI build, the shorter README, `PackageProjectUrl`.
2. **27b** (media): `tools/SiteMedia`, the recordings and screenshots, and the landing page using them.

After 27a merges, the maintainer sets up DNS and Pages (§21.6) and runs the workflow by hand; 27b deploys with
the next manual run or tag.

---

## 22. Fixes from building apps from the docs (Phase 28)

### 22.1 Purpose

Four agents with no context built apps from minitui.org alone against 1.15.1: a todo manager, a log viewer, a
system monitor and a settings wizard. All four worked. The doc problems they found are fixed on the site (PR #59).
This phase fixes the library problems, each checked against the source, and renames `Buffer`, which every agent
tripped over first. The rename breaks every `Render` signature, so this is **2.0.0**.

### 22.2 Renaming `Buffer` to `CellBuffer`

- **The problem:** `dotnet new console` turns on implicit usings, which import `System`, so `Buffer` in a
  `Render(Buffer buf, Rect area, RenderContext ctx)` override is ambiguous with `System.Buffer` (`CS0104`, then
  `CS0534` because the override no longer matches). Every sample that names the type fails to compile until the user
  finds `using Buffer = MiniTui.Buffer;`.
- **The change:** `MiniTui.Buffer` becomes `MiniTui.CellBuffer`, unchanged otherwise. No `[Obsolete]` alias: a type
  named `Buffer` would keep the clash.
- **Everything that names it changes:** `Component.Render`, every builder's `Draw`, `RenderContext.Render`,
  `App.Render`'s callback (`Action<CellBuffer, Rect>`), `CanvasPainter`, `ComponentHarness`, and the example, tests,
  tools, guides and README. The "Drawing on the Buffer" guide becomes "Drawing on the CellBuffer".
- **Migration:** `docs/MIGRATION-1-to-2.md`: replace `Buffer` with `CellBuffer` and delete the alias, with a
  one-line `sed`. The CHANGELOG's 2.0.0 entry is under "Changed" (breaking).

### 22.3 Fixes

| # | Problem (found by) | Fix |
|---|---|---|
| 1 | A focused tree takes every printable key as type-ahead, so `?` (help) never reaches the app (log viewer). | Type-ahead handles a key only when it moves to a match (or extends a match in progress); a key that matches no row is unhandled and goes on to the bindings. |
| 2 | Two quick clicks on a tree's expander open the node and don't close it: the second click is a double-click, which the expander ignores (log viewer). | Every click on the expander toggles, whatever its `ClickCount`. A double-click on the label still acts as Enter. |
| 3 | A gauge's label over the empty part is drawn in the terminal's default color, not the theme's, so it's nearly invisible with `Theme.Light` on a dark terminal (system monitor). | The label takes `Theme.Text` over the empty part (reversed over the filled part, as now). |
| 4 | Text fields, text areas and the command line take Alt+Shift+arrows as Shift+arrows, so a split's keys don't work while one is focused (log viewer). | The text states leave arrows, Home and End with Alt unhandled, as the list, scroll and tree states do with modifiers they don't use. |
| 5 | The palette lists an action twice when it's both a key binding and a menu item (settings wizard). | An item whose action is the same delegate (`Delegate.Equals`: the same method and target) as an earlier one is left out, so the binding's entry, with its key, stays. Lambdas wrapping the same call are different delegates; the guides say so. |
| 6 | Toasts are drawn over the menu bar (settings wizard). | With a menu bar, toasts start on the row below it. |
| 7 | A long breadcrumb pushes the key hints out of the status bar (settings wizard). | The hints get the space first; the breadcrumb takes what's left, truncated from the left, down to nothing. |
| 8 | The hints never show that Esc goes back on a pushed screen (todo manager). | When a screen below the top one exists, the top screen's `PopOnEscape` is true and nothing binds Esc, the chrome shows `esc Back` after the screen's own hints. |
| 9 | A custom overlay's `Title` is drawn against the border (`┌Add todo──`), while the built-in ones pass padded titles (todo manager). | The overlay frame trims the title and draws it with a space on each side, so `" Keys "` and `"Keys"` look the same. |
| 10 | `ComponentHarness` times out when a test awaits a custom dialog: only the built-in dialogs tell the app they're waiting for the user (found while writing the docs). | Every open overlay counts as waiting for the user, so a `RunTask` awaiting any dialog doesn't keep the harness busy. |

### 22.4 Small additions

- **`TextFieldState.Changed`**, an event raised after the text changes (typing, paste, cut, undo, setting `Text`),
  as `SelectState.Changed` is. The settings wizard and the log viewer had to compare the text on every render.
- **`ConfirmAsync`'s default button:** `OverlayStack.ConfirmAsync(string message, string yes = "Yes", string no =
  "No", bool yesFirst = true)` and the same parameter on `ConfirmDialog`. With `yesFirst: false`, No is selected
  when the dialog opens, for destructive actions. (A new optional parameter is a signature change, allowed in 2.0.)

### 22.5 Tests

One test per fix and addition in §22.3–§22.4, through the states or `ComponentHarness`, plus the existing suite
with the rename. The four agents' apps are rebuilt against 2.0.0 (after replacing `Buffer`) as a manual check, and
the Windows exes are rebuilt for the maintainer's testing.

### 22.6 Migration impact

- **Breaking:** the rename (§22.2) and `ConfirmAsync`'s new parameter (binary-breaking; source-compatible).
- **Behavior changes ("Changed"):** fixes 1, 2, 4, 5, 7, 8 and 9 change what keys, clicks or the chrome do.
- `PublicAPI.Shipped.txt`: the `Buffer` entries are removed and the `CellBuffer` ones added (in Unshipped until
  the release).

### 22.7 Out of scope (backlog)

- Horizontal scrolling in paragraphs (`WrapMode.None` cuts long lines with `…`).
- `CheckboxState` and `ButtonState`, so the controls handle their own keys and clicks.
- Bar chart values when bars touch (`BarGap(0)`) or a value is wider than its bar.

### 22.8 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | The new name for `Buffer`? | **`CellBuffer`**: says what it holds, and doesn't clash with anything in `System`. |
| 2 | `ConfirmAsync`: keep Yes selected first, or switch the default to No? | **Keep Yes** (`yesFirst = true`), so existing confirmations behave the same; destructive ones pass `false`. |
| 3 | The `esc Back` hint (fix 8): always, or only with `PopOnEscape`? | **Only when Esc would pop**: `PopOnEscape` true, a screen below, and Esc not bound. |

### 22.9 Implementation order

Two PRs, then one release:
1. **28a** (rename): `Buffer` → `CellBuffer` everywhere, the migration guide, the guides and samples.
2. **28b** (fixes): §22.3 and §22.4, with their tests.

Both go out as **2.0.0**.

---

## 23. Bar values, horizontal paragraph scrolling, and checkbox and button states (Phase 29)

### 23.1 Purpose

Hand-testing the doc-validation apps on 2.0.0 found one problem, and Phase 28 left two features on its backlog
(§22.7). All three come from apps built from the docs:
- **Bar values split short bars.** In the system monitor's grouped bar chart, a value over a bar shorter than one
  cell is drawn across the bar's partial glyph and cuts it in two (`▂8▂`).
- **Long lines can't be read in a paragraph.** With `WrapMode.None`, a long line is cut with `…` and there's no way
  to see the rest (the log viewer).
- **Checkboxes and buttons only draw.** Every form app writes the same component to hold a checkbox's value and
  handle Space, Enter and clicks (the settings wizard).

This is **2.1.0**: one fix and two additions, no breaking changes.

### 23.2 Bar values (fix)

Vertical bars and stacks, where the value is drawn:

| The bar | Its value |
|---|---|
| One whole cell or taller | In its bottom row, reversed in the bar's color (unchanged). A stack's total stays in its top whole cell. |
| Shorter than one cell (a partial glyph) | On the row above the bar, in the bar's color, so the glyph stays whole. Not drawn when there's no row above. |
| Empty (zero, negative, NaN) | In its bottom row, in the bar's color. |

- **The row above** is shared by a short bar's neighbours, so a value there is drawn only if it doesn't touch a value
  already drawn on that row (one column apart at least). The first, leftmost, wins.
- **A value wider than its bar** is still left out. With `BarGap(0)`, values reversed inside neighbouring bars
  read apart by their colors, so they don't change either.
- Horizontal bars, whose values go after the bar, don't change.

### 23.3 Horizontal scrolling in paragraphs (addition)

```csharp
public sealed class ScrollState
{
    // Existing members unchanged

    /// Let ← → and the horizontal wheel (or Shift+wheel or Alt+wheel) scroll unwrapped text sideways. Off by default.
    public bool ScrollsHorizontally { get; set; }

    /// The first visible column of unwrapped text. Clamped when drawn.
    public int HorizontalOffset { get; set; }

    /// How many columns were visible, and the widest row, at the last draw.
    public int ViewportWidth { get; }
    public int ContentWidth { get; }
}
```

- **When it applies:** a `ParagraphBuilder` with `WrapMode.None`, a `State(...)` whose `ScrollsHorizontally` is
  true. Rows wider than the view are drawn from `HorizontalOffset`, clipped at the edges instead of cut with `…`.
- **Scrollbar** (amended at review): when a row is wider than the view, a horizontal scrollbar is shown, like the
  vertical one is when rows overflow. It goes on the bottom border when the block has one without a bottom
  title, and otherwise takes the last text row. Its thumb drags and its track pages by a view's width (§14.4).
  With both bars and no borders, the corner cell is left blank. Where there's no room for it (no free bottom
  border and a single row), a row continuing past the right edge shows `…` in the last column instead, and one
  scrolled past its start `…` in the first.
- **Keys:** ← and → scroll by 8 columns (not handled at the edge, so they go on); `HandleKey`'s Home and End also
  go to the first and last column. Alignment applies only to rows narrower than the view.
- **Mouse:** `ScrollLeft`/`ScrollRight` (a horizontal wheel or trackpad), and Shift with the vertical wheel, scroll
  by 8 columns, and so does Alt with the wheel (amended at review): terminals that keep Shift for bypassing mouse
  mode (Windows Terminal, WezTerm, foot) don't send Shift+wheel to the app, but Windows Terminal sends Alt+wheel.
  Ctrl+wheel is left alone, being zoom in most apps.
- **Selection** (§20.5) works in the scrolled view: positions are columns of the row's text, so a selection keeps
  its text when scrolled, and dragging past the left or right edge scrolls by a column.
- **Off by default,** so screens that use ← → for something else keep them. Without it, nothing changes.
- **Laid out again** (amended at review, for toggling wrapping): a paragraph's selection is kept by its place in
  the text (a line and a column of it), not by row, so it survives the rows being laid out again; it's cleared
  only when its lines are gone. When the width or the wrap mode changes and the offset wasn't moved since the last
  draw, the line at the top stays at the top (the row holding the old top's place), and a view at the end stays at
  the end. This applies to every paragraph with a state, wrapped or not, so a resize keeps its place too.
  Toggling wrapping is left to the app (a binding that switches `Wrap(...)`); the example's Dashboard log binds `w`.

### 23.4 Checkbox and button states (addition)

```csharp
public sealed class CheckboxState
{
    public bool Checked { get; set; }
    public void Toggle();
    /// Raised when the user toggles it (Space, or a click on the box or its label); not when code sets Checked.
    public event EventHandler? Changed;
    public bool HandleKey(ConsoleKeyInfo key);    // Space
    public bool HandleMouse(MouseEvent mouse);    // a left click where it was drawn
}

public sealed class ButtonState
{
    /// Raised when the user presses it: Enter or Space, or a left click released over it.
    public event EventHandler? Pressed;
    /// True while the left button is held down on it, so it draws pressed.
    public bool IsDown { get; }
    public bool HandleKey(ConsoleKeyInfo key);    // Enter, Space
    public bool HandleMouse(MouseEvent mouse);    // press, drag, release where it was drawn
}

public sealed class CheckboxBuilder { public CheckboxBuilder State(CheckboxState state); /* existing */ }
public sealed class ButtonBuilder   { public ButtonBuilder State(ButtonState state);     /* existing */ }
```

- **With a state,** the builder draws `Checked` (or `IsDown` as pressed) from it and records where it drew, for the
  mouse. `Checked(...)` and `Pressed(...)` still work without one.
- **A button fires on release**, as GUI buttons do: pressing and dragging off it cancels. Keys fire at once.
- The Forms guide replaces its hand-written checkbox component with the state, and the example's form uses them.

### 23.5 Tests

- **Bars:** a value over a short bar goes above it and leaves the glyph whole; whole-cell bars unchanged; an empty
  bar's value; two short neighbours whose values would touch; no row above; stacks.
- **Paragraphs:** offsets and clamping, ← → at the edges, Home and End, the edge `…`s, the wheel, Shift+wheel and Alt+wheel,
  alignment of short rows, selection while scrolled and dragging past the side edges, `ScrollsHorizontally` off.
- **States:** Space, clicks on the box and the label, `Changed` only from the user; button Enter, Space, a click,
  a press dragged off and released (no `Pressed`), `IsDown` while held.

Manual QA: the monitor's chart (rebuilt against the fix), a long-line paragraph in the example, and the form demo.

### 23.6 Migration impact

- New API (§23.3, §23.4) in `PublicAPI.Unshipped.txt`. Nothing removed or changed.
- **Changed:** where a short bar's value is drawn.

### 23.7 Out of scope

- Horizontal scrolling of wrapped text, lists or tables.
- Radio buttons and checkbox groups (`SelectState` covers a choice of one).

### 23.8 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | How far ← → and the wheel scroll sideways? | **8 columns**: a few words per step, as `less` steps by half a screen and editors by a few columns. |
| 2 | Horizontal scrolling opt-in, or on whenever the text is wider than the view? | **Opt-in** (`ScrollsHorizontally`): ← → shouldn't change meaning with the content's width. |
| 3 | A button fires on press or on release? | **On release over it**, so a mis-click can be dragged off. |

### 23.9 Implementation order

Three PRs, one release (2.1.0):
1. **29a** (bar values): §23.2.
2. **29b** (paragraphs): §23.3, the example's long-line demo, the Widgets guide.
3. **29c** (states): §23.4, the Forms guide, the example's form.

## 24. Padding, selection by key, and wide bar values (Phase 30)

### 24.1 Purpose

The rest of the hand-testing notes, done before 2.1.0 is released, so 2.1.0 carries Phases 29 and 30:
- **Content touches the borders.** In the system monitor, every panel's content starts in the column next to its
  border: labels, gauges, the services table's first column, the leftmost bar, the chart legend. `Block().Padding`
  exists, but nothing uses it unless the app asks, and the docs' samples don't.
- **A table's selection is by index.** `Rows(items, format)` reads the app's list and `SelectionState.Selected` is a
  position, so re-sorting, filtering, or a row inserted above (a refresh of live data) leaves the highlight on the
  same row number with another item in it. Every app with live data has to find the item again itself.
- **Wide bar values vanish, and touching ones run together.** A value wider than its bar is dropped without a trace
  (`BarWidth(1)` shows no multi-digit values at all), and with `BarGap(0)` and values as wide as their bars,
  neighbouring values read as one number (`3040`). Left as-is by §23.2; the notes asked for it.

### 24.2 Padding (change)

```csharp
public sealed class BlockBuilder
{
    /// Space inside the borders. By default, one column inside each side border (left, right) and none at the
    /// top and bottom; Padding(...) replaces it, and Padding(new Margin(0)) removes it.
    public BlockBuilder Padding(Margin padding);   // existing; only the default changes
}
```

- **The default:** a column inside a left border and a column inside a right border. A side without a border gets
  none, so `Borders.None` and `Borders.Top` blocks are unchanged, and so is the top and bottom.
- **Small blocks:** when the inside would be narrower than 3 columns with it, there's no default padding.
- **Row widgets** (List, Table, Tree, and the Select and filter menus): rows are laid out in the padded area, and the
  selected or hovered row's highlight is drawn across the padding too, border to border, so it doesn't look cut.
- **Scrollbars** stay on the right border. Where a widget puts its scrollbar inside (no right border), there's no
  right border and so no right padding.
- **Built-in overlays, toasts, dialogs and menus** already set their padding and look the same.
- **The text inputs** (TextField, TextArea, CommandLine, Select) with a block move their text, cursor and mouse
  mapping in by the padding; the mouse mapping is computed from the same inner rect, so nothing else changes.
- The guides' samples and the example drop the `Padding(new Margin(1, 0))` they set by hand where it's now the
  default.

### 24.3 Selection by key (addition)

```csharp
public sealed class TableBuilder
{
    /// As Rows(items, format), and the selection follows the item with the selected key when the items change.
    public TableBuilder Rows<T>(IReadOnlyList<T> items, Func<T, Row> format, Func<T, object> key);
}

public sealed class ListBuilder
{
    public ListBuilder Items<T>(IReadOnlyList<T> items, Func<T, Line> format, Func<T, object> key);
}

public sealed class SelectionState
{
    /// The selected item's key at the last draw with a key; null before, or without one.
    public object? SelectedKey { get; }

    /// Select the item with this key at the next draw (for example, a row just added); ignored if none has it.
    public void SelectKey(object key);
}
```

- **Following the item:** at each draw with a key, if the selection wasn't moved since the last draw (by a key, the
  mouse, `Selected` or `SelectKey`) and the item now at `Selected` doesn't have `SelectedKey`, the selection moves
  to the item that has it, and `Offset` moves by as many rows, so the item stays on the same screen row where it
  can (then clamped and kept visible). Keys compare with `Equals`.
- **The item is gone:** the selection keeps its index, clamped, so the next item takes its place.
- **Moved since the last draw:** the move wins, as the paragraph's offset does in §23.3.
- **Cost:** the items are read once only when the item at `Selected` has a different key, so a draw costs the same
  as now while nothing moves; finding the item is one pass over the list.
- Without a key, nothing changes. `Activated`, sorting (`SortColumn`, `ToggleSort`, `SortChanged`) and the
  `HandleKey`/`HandleMouse(count)` calls are unchanged; after the app re-sorts in `SortChanged`, the selection stays
  on its item.

### 24.4 Bar values (fix)

Extends §23.2 for vertical bars and stacks:

- **Whole or not at all:** a value is never cut (`…`); it's drawn whole or left out.
- **A value wider than its bar** goes on the row above the bar (above the stack, for a total), centred on the bar,
  in the bar's color, spreading into the gaps and over neighbouring columns where every cell it needs on that row
  is empty. A bar that reaches the top of the chart has no row above, so its wide value is left out.
- **Values never touch:** on any row, a value is left out when it would sit next to another value with no column
  between them; the leftmost wins. This covers values in the row above (as §23.2) and values reversed in bars' bottom
  rows, so with `BarGap(0)` and values as wide as their bars, `3040` becomes `30` and the next value along. A bar's
  own cells between two values keep them apart.
- Horizontal bars, whose values go after the bar, don't change.

### 24.5 Tests

- **Padding:** a bordered block's inner rect is a column in on each bordered side; `Borders.None`, `Borders.Top` and
  a narrow block get none; `Padding(...)` and `Padding(new Margin(0))` override it; a list, table and tree highlight
  spans border to border; the text inputs' cursor and clicks with a padded block; the existing snapshot tests
  updated to the new default.
- **Selection by key:** a row inserted above, a re-sort, a filter that keeps the item, one that removes it, a key
  move and a mouse click in the same frame as a change, `SelectKey`, `SelectedKey`, the screen row kept, lists and
  tables, no key unchanged.
- **Bar values:** wide values above short and tall bars, into the gaps, left out against a neighbour's cells or the
  top; touching values in the row above and in bottom rows with `BarGap(0)`; never `…`.

Manual QA: the monitor and the four doc-testing apps against the new defaults; the example's screens; the site's
recordings.

### 24.6 Migration impact

- **Changed (visual):** bordered blocks pad one column inside side borders by default. Apps that relied on the
  content starting next to the border, or that set `Padding(new Margin(1, 0))` themselves, see either one more
  column of space or none; `Padding(new Margin(0))` restores the old look.
- New API (§24.3) in `PublicAPI.Unshipped.txt`. Nothing removed.

### 24.7 Out of scope

- A theme-wide padding setting (the default is the block's).
- Compact number formats for values that don't fit (`1.2k`); `ValueFormat` does that already.
- Keyed selection for trees (`TreeState<T>` already keeps its selection by node), for selects and tabs.

### 24.8 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Pad by default, or keep no padding and only document `Padding`? | **Pad by default**, one column on bordered sides: the cramped look came from apps that followed the docs. |
| 2 | Should a list's highlight span the padding? | **Yes, border to border**: a highlight a column short of each border looks cut. |
| 3 | Touching values reversed in bars of different colors (grouped series): drop one, or keep both since the colors part them? | **Drop one** (leftmost wins): one rule, and colors alone don't part them for color-blind users or `Monochrome`. |
| 4 | A key by overload (`Rows(items, format, key)`) or a separate `.Key(...)` call? | **Overloads**: the key needs the item type, which only `Rows` and `Items` know. |
| 5 | Release Phases 29 and 30 together? | **Yes, as 2.1.0**, as asked: no release until the notes are done. |

### 24.9 Implementation order

Three PRs, one release (2.1.0, with Phase 29):
1. **30a** (padding): §24.2, the widgets' highlight, the snapshot tests, the guides and the example.
2. **30b** (selection by key): §24.3, the Widgets and Keys guides, the example's Services screen.
3. **30c** (bar values): §24.4.

---

## 25. Gaps found building MiniTui Desk apps (Phase 31)

### 25.1 Purpose

MiniTui Desk ([minitui-org/desk](https://github.com/minitui-org/desk)) builds its apps on the published package,
and lists what each one needed from MiniTui and didn't find as an issue here. Notes, the first app, found six
(#74–#79). Each is a general gap, not something Desk needs alone: an editor that autosaves, an app with unsaved work
when the terminal closes, a dialog asking for a name, a list narrowed by typing, highlighted text in an editor, and
a message longer than a toast. Nothing here knows about Desk.

All of it is additive, so this is **2.2.0**.

### 25.2 `TextAreaState.Changed` (#74)

```csharp
public class TextAreaState
{
    /// Raised after the text changes: typing, deleting, pasting, cutting, indenting, undo and redo, or setting Text
    /// to something else. Moving the cursor or the selection doesn't raise it.
    public event EventHandler? Changed;
}
```

- **As `TextFieldState.Changed`** (§22.4): once per change, after the state is consistent, so a handler can read
  `Text`, `Cursor` and `CanUndo`.
- **Setting `Text`** to the same text doesn't raise it (it still clears the history and moves the cursor). Comparing
  is cheap there; the editing paths know whether they changed anything without comparing.
- **Cost:** an edit already bumps the state's internal version; `Changed` is raised when the version moved, so a
  key that changes nothing (an arrow, Backspace at the start) raises nothing.

### 25.3 Asking before quitting (#75)

Today Ctrl+C calls `Quit()` unless the focused text component copies with it (§10.4, §20.5), and SIGHUP and SIGTERM
restore the terminal and let the process end. An app with unsaved work can't save it or ask first, short of taking
Ctrl+C in every focusable component and registering its own signal handlers.

```csharp
public enum QuitReason
{
    /// Ctrl+C, or RequestQuit: the app can say no.
    Requested,
    /// The terminal went away: SIGHUP on Unix, the console window closing on Windows.
    Hangup,
    /// SIGTERM on Unix; logoff or shutdown on Windows.
    Terminated,
}

public sealed class App
{
    /// Run on the UI thread before the app quits. For Requested, return false to keep running (to ask first, say no
    /// and call Quit() once the user has answered). For Hangup and Terminated the process ends whatever it returns,
    /// and the handler has 2 seconds.
    public App OnQuitting(Func<QuitReason, bool> quitting);

    /// Quit as Ctrl+C does: OnQuitting (with Requested) decides. Thread-safe.
    public void RequestQuit();

    public void Quit();   // unchanged: quits without asking
}
```

- **Ctrl+C:** as now, the focused text component copies with it if it can; otherwise it calls `RequestQuit()`
  instead of `Quit()`. The built-in `ctrl+c` entry in `App.Keys` (shown in help) does the same.
- **`Quit()` doesn't ask**, so an app's own quit command can save and call it, or call `RequestQuit()` to go through
  the same handler. Quitting when the last screen is popped and cancelling `RunAsync`'s token don't ask either: the
  first is the app's own doing, the second its host's.
- **Without a handler** nothing changes: `RequestQuit()` is `Quit()`.
- **A handler that throws** goes to `OnError` as any callback's exception does (by default `Run()` rethrows it). It
  isn't taken as a yes, so with an `OnError` that continues, the app keeps running; for the signals the process
  ends as it would have.
- **Signals:** the signal thread posts the handler to the UI thread and waits for it, at most 2 seconds, then
  restores the terminal and lets the process end as now (so code after `Run()` still doesn't run, and the exit
  status still shows the signal). After the handler the loop draws nothing more, so nothing lands on the shell's
  screen. A loop that's blocked (a handler stuck, or the app busy in its own code) times out and
  the process ends as before.
- **The terminal closing** sends SIGCONT with SIGHUP, and redrawing for the SIGCONT can fail with an I/O error
  before the SIGHUP is handled; until now that crashed the app with the unhandled `IOException`. An I/O error while
  the console has hung up (`tcgetattr` fails with `EIO`) is now a hangup: the handlers run there and then, and the
  SIGHUP that follows doesn't wait for them again. Apps without a handler no longer crash on a hangup either.
- **While ending,** an exception from a handler or anything else goes to `OnError` but doesn't quit the loop or
  rethrow from `Run()`, so the process ends by the signal, not by a crash.
- **Nested apps** on the same console all get the signal, innermost first, in the same 2 seconds. An outer app whose
  loop is waiting on a nested one's `Run()` on the same thread has its handler run by the nested loop after the
  nested one's, since its own loop can't.
- **While suspended** (`App.Suspend`, Ctrl+Z), a signal doesn't run the handler, since the loop is waiting on the
  program it runs; the process ends at once, as now.
- **SIGQUIT and SIGINT** (Ctrl+\ and a Ctrl+C sent from outside) are unchanged: SIGQUIT is for ending at once, and
  Ctrl+C typed in the app arrives as a key, not a signal.
- `ComponentHarness` gets the same behavior for `RequestQuit` and Ctrl+C; a test can call the handler's signal path
  with `harness.Signal(QuitReason.Hangup)`.

### 25.4 A text prompt (#76)

```csharp
public sealed class OverlayStack
{
    /// Ask for a line of text. Enter answers the text, Esc and closing the dialog answer null. validate, given the
    /// text on Enter, returns a message to show under the field (keeping the dialog open), or null to accept it.
    /// Awaited on the UI thread, the continuation runs there.
    public Task<string?> PromptAsync(string title, string text = "", Func<string, string?>? validate = null,
        string? placeholder = null);
}

public sealed class PromptDialog : Overlay   // public, as ConfirmDialog is
{
    public PromptDialog(string title, string text = "", Func<string, string?>? validate = null, string? placeholder = null);
    public Task<string?> Result { get; }
}
```

- **Looks like the confirm dialog:** a modal box with the title, a text field the width of the box (40 columns, or
  the screen less 4 when narrower) with the cursor at the end of `text`, and `enter OK  esc Cancel` as hints.
- **Validation:** the message is drawn under the field in `Theme.Error`, the box growing a row for it (wrapping to
  at most 3 rows), and it goes when the text changes. `validate` runs only on Enter, so it can be slow-ish (a file
  system check), and its exceptions go to `OnError` with the dialog left open.
- The text is single-line; a paste with line breaks has them replaced by spaces, as `TextFieldState` does.

### 25.5 A public filterable list (#77)

The command palette and the filterable select share `FilterList` (§13.3, §13.6), internal. It's made public as a
state and a widget, for use in a screen as well as an overlay.

```csharp
/// A row: the label the filter matches, muted text after it, a right-aligned hint, and a key that identifies it.
public readonly record struct FilterRow(string Label, string? Detail = null, string? Hint = null, object? Key = null);

public sealed class FilterListState
{
    public FilterListState(IEnumerable<FilterRow>? rows = null, string filter = "");

    /// The filter line. Its text filters the rows.
    public TextFieldState Filter { get; }

    /// Replace the rows, keeping the selection on the row with the same Key if it still matches (otherwise the
    /// best match), and filtering again.
    public void SetRows(IEnumerable<FilterRow> rows);
    public IReadOnlyList<FilterRow> Rows { get; }

    /// Indexes into Rows that match, best first; all of them in order with an empty filter.
    public IReadOnlyList<int> Matches { get; }

    /// The selected row's index into Rows, or null when nothing matches.
    public int? Selected { get; }
    public void Select(int row);   // ignored unless it matches

    /// ↑ ↓ PgUp PgDn and Ctrl+Home Ctrl+End move the selection; Esc clears a non-empty filter; any other key goes to
    /// the filter line. Enter, Tab and Esc with an empty filter are left to the caller.
    public bool HandleKey(ConsoleKeyInfo key);
    public bool HandlePaste(string text);
    /// Wheel, a click to select (raising Activated on a double-click), clicks and drags in the filter line.
    public bool HandleMouse(MouseEvent mouse);

    /// ↑ at the first row goes to the last, and ↓ at the last to the first. Off by default; the palette sets it.
    public bool Wraps { get; set; }

    /// Raised when the selection moves to another row, by a key, the mouse, filtering or SetRows.
    public event EventHandler? SelectionChanged;
    /// Raised on a double-click on a row (Enter is the caller's, as above).
    public event EventHandler? Activated;
}

public static partial class Widget
{
    public static FilterListBuilder FilterList(FilterListState state);
}

public sealed class FilterListBuilder
{
    public FilterListBuilder Block(BlockBuilder? block);   // the divider under the filter line joins its borders
    public FilterListBuilder Placeholder(string? text);    // in the empty filter line
    public FilterListBuilder Empty(string text);           // when nothing matches; "No matches" by default
    public FilterListBuilder Focused(bool focused = true); // the filter line's cursor, and the selection's style
    public void Draw(CellBuffer buf, Rect area);
}
```

- **The same matching and drawing as the palette** (fuzzy match, best score first, matched characters in bold, the
  detail and hint muted, a scrollbar on the right border), so the palette and the filterable select become users of
  the public type and look the same as now.
- **Empty filter:** every row in its own order, not by score, so a list sorted by the app (newest first, say) stays
  sorted until the user types.
- **Home and End** go to the filter line, as in the palette; Ctrl+Home and Ctrl+End move the selection.
- **The rows are the state's**, set with `SetRows` when the app's data changes, not passed at each draw, because
  keys between draws filter them. With keys, a refresh keeps the selection on its item (as §24.3 does for lists).
- `Sanitizer.SingleLine` applies to labels, details and hints, as now.

### 25.6 Styled text in a text area (#78)

```csharp
/// A styled range on one line: from Start, Length UTF-16 code units, patched with Style.
public readonly record struct TextSpan(int Start, int Length, Style Style);

public sealed class TextAreaBuilder
{
    /// Style parts of each line, given its index and text. Called for the visible lines at each draw.
    public TextAreaBuilder Highlight(Func<int, string, IEnumerable<TextSpan>> highlight);
}
```

- **Drawn under** the search matches, the selection and the cursor, and over `Style(...)`, by patching the cell
  styles (§4.3), so a span setting only a foreground color keeps the selection's background.
- **Ranges are clamped** to the line and snapped outward to cluster boundaries; one that's off the line or empty is
  ignored, so a highlighter that's a frame behind an edit can't throw or draw on the wrong cells of a wide character.
- **Per line, not per text:** the callback gets one line at a time, so it can't see multi-line constructs (a fenced
  code block) without its own state. It can keep that state from `TextAreaState.Changed` (§25.2) and the line index,
  and must be fast: the guides show caching per line.
- No built-in highlighters (Markdown, C#); the guides have a small Markdown one as the example.

### 25.7 Long toasts wrap (#79)

- **Wrapped:** a toast's text wraps at word boundaries (as `Paragraph` does) within the 60-column box, up to 3 lines,
  the box growing to fit; only a message longer than 3 lines is cut, with `…` at the end of the third.
- **Narrower than 60:** the box is as wide as the longest wrapped line, so a short toast looks as now.
- **Line breaks** in the text start a new line, where they're now replaced by spaces.
- Toasts stack as now, each as tall as its text; one that doesn't fit below those above it waits.

### 25.8 Tests

- **`Changed`:** raised once for typing, Backspace, Delete, Enter, paste, cut, indent and outdent, undo, redo and
  `Text`; not for moves, selection, a no-op Backspace or the same `Text`.
- **Quitting:** Ctrl+C and `RequestQuit` call the handler and stay on false; copying with a selection doesn't call
  it; `Quit()`, popping the last screen and `RunAsync`'s token don't; a throwing handler goes to `OnError` and keeps
  the app; `harness.Signal` runs it with the reason and ends the loop whatever it returns. A child process gets
  through the signal path in-process: the handler runs on the UI thread and nothing is drawn after it, a stuck
  handler holds the signal 2 seconds at most, apps without a handler don't hold it, nested apps' handlers run
  innermost first, and an I/O error after a hangup is one. Real signals by hand, below: SIGTERM, SIGHUP
  and closing the terminal, with the exit status.
- **Prompt:** Enter answers the text, Esc null; a validation message keeps it open and clears on typing; snapshots
  at 80 columns and at a narrow width.
- **Filter list:** the palette's and select's existing tests unchanged; empty filter keeps order; `SetRows` keeps
  the selection by key and falls back to the best match; `Wraps`; `SelectionChanged` and `Activated`; snapshots
  with and without a block, focused and not.
- **Highlight:** spans patched under selection and matches; clamped and off-line spans; wide characters.
- **Toasts:** one, two and three lines, over three cut, a line break, stacking with different heights.

Manual QA: a small app whose handler writes a file, in tmux: `kill -TERM` (exit 143), `kill -HUP` (129), a
stuck handler, Ctrl+C refused then allowed, and closing the session (the real hangup); Notes in MiniTui Desk against a local 2.2.0 package, with its workarounds removed (§25.11); the example's
screens; Windows Terminal and conhost for the console window closing (Windows-only code, listed in the PR).

### 25.9 Migration impact

- **Changed:** Ctrl+C calls `OnQuitting` first, but only for apps that set one. Toasts longer than a line wrap
  instead of being cut, and a line break in one starts a new line. `FilterList`'s move to public changes nothing
  in the palette or select.
- New API in `PublicAPI.Unshipped.txt`. Nothing removed.

### 25.10 Out of scope

- Cancelling SIGTERM or a hangup (keeping the process alive): the terminal is gone for a hangup, and a service
  manager expects SIGTERM to end the process.
- Multi-line prompts, a prompt with a list of suggestions (a `FilterList` in an overlay does that), and async
  validation.
- Built-in syntax highlighters; folding; multi-line spans.
- Toasts with actions or that stay until dismissed.

### 25.11 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| 1 | Make Ctrl+C an ordinary key binding that screens can override, instead of a handler? | **A handler**: bindings would cover Ctrl+C but not the signals, and an app would still need one place to save from both. |
| 2 | On a hangup or SIGTERM, end the process after the handler as now, or let `Run()` return? | **End it as now**, so the exit status shows the signal and nothing runs against a terminal that's gone. |
| 3 | The time limit for the handler on a signal: fixed, or set by the app? | **Fixed at 2 seconds**: Windows allows about 5 for a closing console, and the limit only stops a stuck handler from keeping a closed window's process alive. |
| 4 | `FilterListState`'s rows: owned by the state (`SetRows`), or passed at each draw as lists are? | **Owned by the state**: keys filter between draws, and a refresh with keys keeps the selection. |
| 5 | How many lines can a toast take? | **3**: a long message is readable, and a toast still doesn't cover much of the screen. |
| 6 | Release when? | **2.2.0 after 31d**; then Desk moves its apps to it and removes the workarounds. |

### 25.12 Implementation order

Four PRs, one release (2.2.0):
1. **31a** (quitting and editor changes): §25.2 and §25.3, with this spec.
2. **31b** (prompt and toasts): §25.4 and §25.7.
3. **31c** (filter list): §25.5, the palette and select moved onto it.
4. **31d** (highlighting): §25.6, with a Markdown example in the guides.
