# Getting started

Install MiniTui, run the demos, and write a first app.

## Install

```bash
dotnet add package MiniTui
```

Requires .NET 10. Works in Windows Terminal, conhost (with reduced glyphs), macOS Terminal, iTerm2, tmux,
SSH sessions and Linux terminals. It's trim- and AOT-compatible.

## Run the demos

The repository has an example app with a demo of each feature:

```bash
git clone https://github.com/minitui-org/minitui
cd minitui
dotnet run --project src/MiniTui.Example
```

This launches one app with a stack of screens: a menu, and a demo screen for counters, lists, a form,
progress bars, a dashboard, a command line and tabs, plus:

- **Services**: an async table of 500 services from a fake HTTP API (slow, and failing 20% of the time),
  with refresh and cancellation, a detail screen (Enter), delete with a confirm dialog (`d`) and error toasts.
- **Widgets**: every widget on one screen, a theme toggle (`t`) and a Unicode sample panel.

Esc or `q` goes back, `?` or F1 lists the keys for the current screen, and Ctrl+C quits.

## A small app

A small app is one render callback and a [`KeyMap`](xref:MiniTui.KeyMap):

```csharp
using MiniTui;

var count = 0;
var app = App.Create();
var keys = new KeyMap()
    .Add("k,up", "+1", () => count++)
    .Add("j,down", "-1", () => count--)
    .Add("q", "Quit", app.Quit);

app.OnKey(keys.Handle)
    .Render((buf, screen) =>
    {
        var inner = Widget.Block().Title(" Counter ").Draw(buf, new Rect(2, 1, 30, 5));
        buf.SetString(inner.X + 1, inner.Y + 1, $"Count: {count}", Style.Default.Bold());
        Widget.StatusBar().Left(keys.Hints(screen.Width)).Draw(buf, screen);
    })
    .Run();
```

## Your first screen

Larger apps are built from screens and components ([Screens and components](screens.md)). Each draws itself
into a `CellBuffer`, the grid of cells the app compares with the last frame to send only what changed:

```csharp
using MiniTui;

var app = App.Create();
app.Screens.Push(new CounterScreen());
app.Run();

sealed class CounterScreen : Screen
{
    int count;
    public override string Title => "Counter";

    public CounterScreen() =>
        Keys.Add("k,up", "+1", () => count++).Add("j,down", "-1", () => count--);

    public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
        buf.SetString(area.X + 1, area.Y + 1, $"Count: {count}", ctx.Theme.Text.Bold());
}
```
