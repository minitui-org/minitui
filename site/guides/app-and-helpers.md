# The app and helpers

The [`App`](xref:MiniTui.App) builder's options, and small helpers for geometry, commands and status messages.

## `App`

Fluent builder for TUI applications. It runs an event loop: keys, ticks, resizes and posted actions
go through one queue, and a frame is drawn only when something happened. All callbacks run on the
thread that called `Run()`.

```csharp
App.Create()
    .OnSetup(app => { })                       // Optional: run once before the first frame
    .OnKey(key => false)                       // Optional: see keys first; true = handled
    .WithMouse()                               // Optional: report the mouse (see Mouse)
    .WithSuspendKey()                          // Optional: Ctrl+Z suspends to the shell (Linux, macOS)
    .OnEvent(e => { })                         // Optional: KeyEvent, PasteEvent, MouseEvent, ResizeEvent, ResumeEvent, TickEvent
    .OnUpdate(() => { })                       // Optional: run each loop iteration before render
    .OnError(ex => ErrorAction.Continue)       // Optional: default restores the terminal and rethrows
    .WithTickRate(TimeSpan.FromMilliseconds(100)) // Optional: default 250 ms; null = redraw only on events
    .Render((buf, screen) => { })
    .Run();                                    // Or await .RunAsync()
```

Ctrl+C quits (unless `OnKey` takes it, or the focused text field copies with it). An app uses either a `Render`
callback or `Screens`; `Overlays` (dialogs) and `Toasts` work with both.

An app with unsaved work sets `OnQuitting`, which runs on the UI thread before Ctrl+C or `app.RequestQuit()` quits,
and before the process ends on SIGTERM or when the terminal closes (SIGHUP, or the console window on Windows):

```csharp
app.OnQuitting(reason =>
{
    if (document.TrySave()) return true;
    // Saving failed: stay, unless the process is ending anyway (reason is Hangup or Terminated)
    app.Toasts.Push("Not saved, so still open", ToastLevel.Error);
    return false;
});
```

For Ctrl+C and `RequestQuit` (`QuitReason.Requested`), returning false keeps the app running; to ask first, return
false, show `ConfirmAsync`, and call `app.Quit()` on a yes. For a signal the process ends whatever the handler
returns, and it has 2 seconds. `app.Quit()` never asks, so an app's own quit command can call `RequestQuit()` to go
through the same handler.

From any thread, `app.Post(action)` runs code on the UI thread, `app.RequestRedraw()` asks for a
frame and `app.Quit()` stops the loop. `app.RunTask(ct => WorkAsync(ct))` starts async work whose
`await`s resume on the UI thread; its exceptions go to `OnError`, and `ct` is `app.Stopping`.
`app.Suspend(action)` runs a program in the real terminal (see
[Running other programs](terminal.md#running-other-programs)).

While running, `Console.Out` and `Console.Error` are redirected into `app.Log` (a [`LogBuffer`](xref:MiniTui.LogBuffer) of
the last 1,000 lines) so stray writes can't corrupt the screen. Error lines are written to stderr
on exit. Turn this off with `.CaptureConsoleOutput(false)`.

## [`Rect`](xref:MiniTui.Rect) & [`Vec2`](xref:MiniTui.Vec2)

Geometry helpers with layout utilities.

```csharp
var (top, bottom) = area.SplitHorizontal(10);
var (left, right) = area.SplitVertical(area.Width / 2);
var inner = area.Shrink(2); // 2-cell margin on all sides; never negative
var content = area.Inner(new Margin(Left: 1, Top: 2, Right: 1, Bottom: 0));
var visible = area.Intersect(screen);
foreach (var row in area.Rows()) { /* one Rect per row */ }
```

## [`ParsedCommand`](xref:MiniTui.ParsedCommand)

Parse colon-prefixed commands like `:add person note text`.

```csharp
var cmd = ParsedCommand.Parse(":add john Fixed the bug");
// cmd.Name == "add"
// cmd.GetArg(0) == "john"
// cmd.GetArgsFrom(1) == "Fixed the bug"
```

## [`TimedMessage`](xref:MiniTui.TimedMessage)

Status messages that auto-expire.

```csharp
var status = TimedMessage.Show("Saved!", seconds: 3);

// In render:
Widget.StatusBar()
    .Message(status, "Default status text")
    .Draw(buf, area);
```

## [`CommandRegistry`](xref:MiniTui.CommandRegistry)

Commands with aliases, usage, descriptions and subcommands, for a `:` command line:

```csharp
var commands = CommandRegistry.Create()
    .Add("add", "a").Usage("<text>").Description("Add a note").Handler(cmd => notes.Add(cmd.ArgsAsString))
    .Add("quit", "q").Handler(_ => CommandResult.Quit())
    .Build();

app.Keys.Add(":", "Command", () => app.Overlays.Show(new CommandLineOverlay(commands)));
app.Keys.Add("ctrl+p", "Commands", () => app.Overlays.ShowPalette(commands));   // the commands and the key bindings
```
