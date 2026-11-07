# The terminal

What MiniTui does with the terminal: queries, keyboard protocols, the mouse, running other programs, debugging and backends.

Manages the actual console. Double-buffers for performance: each frame, only the cells that changed
are sent to the backend, as one write.

```csharp
using var terminal = new Terminal();
terminal.Draw(buf => { /* render here */ });
terminal.Invalidate(); // Force full redraw next frame
```

MiniTui runs on the terminal's alternate screen, so your shell's screen and scrollback are left
untouched. The terminal is restored on normal exit, Ctrl+C, unhandled exceptions, and
SIGTERM/SIGHUP; `App.OnQuitting` runs first, so an app can save (see
[App](app-and-helpers.md)). Ctrl+C is delivered as a key and quits `App.Run`. An app started from
inside another app shares its terminal, and the outer app redraws itself when the inner one exits,
so no `Invalidate()` is needed.

Colors are downsampled to what the terminal supports (`NO_COLOR`, `COLORTERM`, `TERM`, ...; see
`ColorCapability.Detect`). Stdin and stdout must be a terminal: if either is redirected,
[`TerminalNotSupportedException`](xref:MiniTui.TerminalNotSupportedException) is thrown.

## Terminal queries

At startup and after a resume, the app asks the terminal about itself: its default colors (OSC 10 and 11), its
name and version (XTVERSION), the keyboard protocols it supports, and last Primary Device Attributes, which
every terminal answers, so its reply means no more are coming. The replies are read by the input decoder and
never arrive as keys, however late they come. What the terminal said is in `app.TerminalInfo`, and each change
raises a [`TerminalInfoEvent`](xref:MiniTui.TerminalInfoEvent):

```csharp
app.OnEvent(e =>
{
    if (e is TerminalInfoEvent { Info: { IsDark: false } }) Log.Info("light background");
});
```

`TerminalInfo.IsDark` comes from the background's luminance, or from `COLORFGBG` when the terminal doesn't
answer. Queries are only sent where their replies are decoded (the default backend and input source in a real
terminal), so they never reach a test backend. `WithTerminalQueries(false)` or `MINITUI_QUERIES=0` turns them off.

## Keyboard input

MiniTui puts the terminal in raw mode (on Windows, the console in VT input mode) and decodes its input
itself ([`TerminalInputSource`](xref:MiniTui.TerminalInputSource), the default). Esc, Alt combinations, function keys and arrows with
Shift/Ctrl/Alt arrive reliably and the same way on every platform, and a fast burst of keys never
produces a stray Esc.

- **Keyboard protocols.** The classic encoding can't tell Ctrl+Enter or Shift+Enter from Enter, Ctrl+Tab
  from Tab, or Ctrl+I from Tab. Where the terminal supports one, MiniTui turns on a richer encoding: the
  kitty keyboard protocol (kitty, WezTerm, foot, Ghostty, Alacritty, iTerm2 and others), else xterm's
  modifyOtherKeys, each only after the terminal said it supports it (see [Terminal queries](terminal.md#terminal-queries)),
  or win32-input-mode in Windows Terminal. Those keys then arrive as themselves and bindings such as
  `ctrl+enter`, `shift+enter`, `ctrl+tab`, `ctrl+i` and `ctrl+1` match them; every other key arrives exactly
  as before. In other terminals they arrive without the extra modifier, so give such bindings a fallback
  (`Keys.Add("ctrl+s,ctrl+enter", "Submit", Submit)`). A terminal that sends a line feed for one of them,
  by default or through its own settings, delivers Ctrl+J, not Enter. `app.TerminalInfo.Keyboard` says which protocol is in
  use; `WithKeyboardProtocol(false)` or `MINITUI_KEYBOARD=legacy` turns them off. Built-in widgets treat
  Shift+Enter as Enter (a text area inserts a line break) and leave Ctrl+Enter and Ctrl+Tab to the app.

- **Esc** is recognized 50 ms after it's pressed, because terminals send other keys as sequences that
  start with Esc. Change it with `app.WithInput(new TerminalInputSource(TimeSpan.FromMilliseconds(100)))`.
- **Ctrl+C** quits, unless the focused text field has a selection, which it copies. **Ctrl+Z**, Ctrl+\, Ctrl+S and Ctrl+Q reach the app as keys instead of suspending
  the process or pausing output. To make an unhandled Ctrl+Z suspend to the shell, call
  `app.WithSuspendKey()` (see [Running other programs](terminal.md#running-other-programs)).
- **macOS:** raw input is tested in CI on a pseudo-terminal, but not yet by hand in Terminal.app or
  iTerm2. Terminal.app sends Alt (Option) combinations only with "Use Option as Meta key" turned on.
- **Paste** arrives as one call, not a key per character, so a pasted line break doesn't submit and pasted
  letters don't fire bindings. A component accepts pastes by overriding `HandlePaste`; for a text field,
  `public override bool HandlePaste(string text) => State.Paste(text);` inserts the paste as one line.
  A paste nothing handles is dropped. `App.OnPaste` sees pastes first, and `ComponentHarness.Paste` tests
  them. Conhost (the classic Windows console) can't mark pastes, so there MiniTui pastes the clipboard
  itself on Ctrl+V; right-click paste in conhost still arrives as keys. With `MINITUI_INPUT=console`,
  pasted text arrives as keys, as in 1.0.
- If a terminal's keys decode wrongly, set `MINITUI_INPUT=console` to use the `Console.ReadKey` path,
  and please report it: the Example's **Input** demo shows exactly how each key was decoded.

## Mouse

Call `app.WithMouse()` to have the terminal report mouse presses, releases, drags and the wheel. It's off by
default because, while an app captures the mouse, selecting text in the terminal needs Shift+drag
(Option+drag in iTerm2).

- **Click to focus:** a press focuses the component under the pointer (or its nearest focusable
  ancestor), so clicking a text field focuses it. The wheel and releases don't move focus.
- **Routing:** `App.OnMouse` sees each [`MouseEvent`](xref:MiniTui.MouseEvent) first; then the component under the pointer gets
  `HandleMouse`, and if it returns false, its parent, up to the screen. With a dialog open, only the
  dialog can be clicked. Key bindings never fire from the mouse. `X` and `Y` are 0-based cells.
- **Drags** (`MouseAction.Drag`, the pointer moving with a button held) and the release after them go to
  the component the press hit, even after the pointer leaves it, and are kept inside the screen. They
  don't move focus.
- **Double-click:** the app counts quick presses of the same button in the same cell and sets
  `MouseEvent.ClickCount` to 1, 2 or 3 on each press (`WithDoubleClickTime`, 500 ms by default). A
  double-click is still two presses, so the first one does what a single click does.
- **Hover:** `app.WithHover()` (which turns the mouse on too) asks for every pointer move, delivered as
  `MouseAction.Move`. The component under the pointer and its ancestors get its position as `ctx.Hover`
  (null for the rest, and under an overlay); pass it to `.Hover(ctx.Hover)` on a list, table, tree, button,
  checkbox or tab bar to patch `Theme.Hover` over what's under it. Popup menus, the palette and the menu bar
  follow the pointer. A move only draws a frame when what's hovered changes or a component drew with
  `ctx.Hover`, and queued moves are coalesced. `App.PointerPosition` is the last reported position.
- **Hit testing** uses where components were drawn in the last frame: the top screen, overlays, and
  children rendered with `ctx.Render(child, buf, rect)`, which also sets `child.Area`. A child rendered
  with `child.Render(buf, rect, ctx.For(child))` isn't a target; its clicks go to its parent.
- **Built in:** `SelectionState.HandleMouse(mouse, count)` (lists and tables: the wheel scrolls by a
  row without moving the selection, a click selects a row, a double-click raises `Activated`, and on a
  `Sortable()` table a header click sorts), `TreeState.HandleMouse` (a double-click on a row does what Enter does),
  `ScrollState.HandleMouse` (the wheel scrolls text by three rows), `TabBarState.HandleMouse` (a click
  selects a tab), `TextFieldState.HandleMouse` and `TextAreaState.HandleMouse` (a click places the
  cursor, Shift+click and dragging select, a double-click selects a word and a triple-click the line, and a
  drag past an edge scrolls). In lists, tables, trees and
  paragraphs, dragging the scrollbar's thumb scrolls and a press on its track pages. Confirm dialogs take clicks on Yes and No; message
  and help dialogs scroll with the wheel.
- **Your own controls** handle clicks themselves; `IsClick` is a left-button press:

```csharp
public override bool HandleMouse(MouseEvent mouse)
{
    if (!mouse.IsClick) return false;
    Checked = !Checked;
    return true;
}
```

`ComponentHarness.Click(x, y)`, `DoubleClick(x, y)`, `Scroll(x, y, rows)` and `Mouse(event)` test it, and
`ScriptedInputSource.EnqueueMouse` scripts it for whole apps. In conhost, mouse capture also turns off
Quick Edit while the app runs.

## Running other programs

`app.Suspend(action)` gives the terminal back while `action` runs, so a program such as `$EDITOR` or
`less` can use it: MiniTui stops reading input, turns mouse reporting and bracketed paste off, restores
the terminal mode and leaves the alternate screen. When the action returns (or throws), the app takes the
terminal again and redraws. Call it on the UI thread, for example from a key binding:

```csharp
app.Keys.Add("ctrl+e", "Edit notes", () => app.Suspend(() =>
{
    var editor = Environment.GetEnvironmentVariable("EDITOR") ?? "vi";
    using var process = Process.Start(new ProcessStartInfo(editor) { ArgumentList = { path } })!;
    process.WaitForExit();
}));
```

While the action runs, `Console.Out` is the real console again, and Ctrl+C stops the child program, not
the app.

**Ctrl+Z** (Linux and macOS): `app.SuspendProcess()` stops the process and returns to the shell, with the
terminal restored as `Suspend` does; `fg` resumes the app, which redraws. `app.WithSuspendKey()` makes an
unhandled Ctrl+Z do that. It's off by default, as in most TUI libraries; a component or key binding for
`ctrl+z` still gets it first, and text fields and areas take it for undo. A `SIGTSTP` sent from outside (`kill -TSTP`) is handled the same
way. After any resume, `OnEvent` gets a [`ResumeEvent`](xref:MiniTui.ResumeEvent). Windows has no job control, so there Ctrl+Z is
always a key and `SuspendProcess` returns false.

## Debugging

`app.WithDebugKey()` makes F12 (or the key you pass) toggle a debug overlay; `app.ShowDebug = true` shows it
from code, and `MINITUI_DEBUG=1` turns on both without changing the app. The overlay outlines every component
rendered with `ctx.Render` (the focus chain in magenta, the focused component in yellow) and shows a panel with
the frame number, render and flush times, cells and bytes sent, the terminal's size and capabilities, the
focus chain and the component under the pointer. It takes no input, and clicks go through it.

```csharp
var frame = app.LastFrame;   // FrameStats: Frame, RenderTime, FlushTime, CellsChanged, BytesWritten, FullRedraw
app.Log.Add($"frame {frame.Frame}: {frame.RenderTime.TotalMilliseconds:0.0} ms, {frame.CellsChanged} cells");
```

## Backends

[`Terminal`](xref:MiniTui.Terminal) draws through an [`ITerminalBackend`](xref:MiniTui.ITerminalBackend):

- [`AnsiBackend`](xref:MiniTui.AnsiBackend), the default: ANSI/VT escape sequences, synchronized output, 16/256/truecolor.
- [`LegacyConsoleBackend`](xref:MiniTui.LegacyConsoleBackend): the `System.Console` API, used automatically on Windows consoles without VT support (16 colors).
- [`TestBackend`](xref:MiniTui.TestBackend): an in-memory screen for tests (see [Testing](testing.md)).
