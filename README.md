# MiniTui

A minimal, immediate-mode TUI (Text User Interface) framework for .NET, inspired by
[Ratatui](https://ratatui.rs). No runtime dependencies.

- **Immediate mode:** you describe the UI every frame. No retained widget tree, no diffing of components.
- **Fast, flicker-free output:** double-buffered; only changed cells are sent to the terminal, in one write.
- **Correct text:** display-width measuring for CJK and emoji, and no escape-sequence injection from data.
- **Async-first:** HTTP calls never block the UI; results come back on the UI thread.
- **Scales up:** screens, components, focus scopes, overlays and scoped key bindings for larger tools.
- **Safe terminal:** restored on exit, Ctrl+C, crashes, SIGTERM/SIGHUP and window close, with a chance to save
  unsaved work first.

## Install

```bash
dotnet add package MiniTui
```

Requires .NET 10. Works in Windows Terminal, conhost (with reduced glyphs), macOS Terminal, iTerm2, tmux,
SSH sessions and Linux terminals. It's trim- and AOT-compatible.

## A small app

A small app is one render callback and a `KeyMap`:

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

## Documentation

**[minitui.org](https://minitui.org)** has the guides and the API reference:

- [Getting started](https://minitui.org/guides/getting-started.html): install, run the demos, a first app
- [Async data and HTTP](https://minitui.org/guides/async.html): loading without blocking the UI
- [Screens and components](https://minitui.org/guides/screens.html): screens, focus, overlays, dialogs and scoped key bindings
- [Layout](https://minitui.org/guides/layout.html) and split panes
- [Widgets](https://minitui.org/guides/widgets.html): lists, tables, trees, tabs, text fields and editors, charts and more
- [Theming](https://minitui.org/guides/theming.html), including light and dark detection
- [Keys and widget state](https://minitui.org/guides/keys-and-state.html): key bindings, selection and the clipboard
- [Drawing on the CellBuffer](https://minitui.org/guides/drawing.html)
- [The terminal](https://minitui.org/guides/terminal.html): keyboard protocols, the mouse, running other programs, debugging
- [Testing](https://minitui.org/guides/testing.html) with `ComponentHarness`
- [The app and helpers](https://minitui.org/guides/app-and-helpers.html), [Extending](https://minitui.org/guides/extending.html), and
  [upgrading from 0.7](https://minitui.org/docs/MIGRATION-0.7-to-1.0.html)
- The [API reference](https://minitui.org/api/MiniTui.html) and the [changelog](https://minitui.org/changelog.html)

## Run the demos

```bash
dotnet run --project src/MiniTui.Example
```

The example app has a demo of each feature: lists, forms, a dashboard, charts, an editor, trees, split panes, an
async service table and every widget on one screen. Esc or `q` goes back, `?` lists the keys, and Ctrl+C quits.

## Project Structure

```
MiniTui/
├── src/
│   ├── MiniTui.sln
│   ├── MiniTui/
│   │   ├── App/            # App builder and loop, Loadable, LogBuffer, events
│   │   ├── Core/           # Color, Style, Cell, CellBuffer, Rect/Vec2/Margin
│   │   ├── Text/           # Span, Line, Text, sanitizing, Unicode width
│   │   ├── Layout/         # Layout builder, constraints and solver
│   │   ├── Terminal/       # Terminal, backends, ANSI encoder, session, input sources
│   │   ├── Components/     # Component, FocusScope, screens, overlays, dialogs, key routing, ComponentHarness
│   │   ├── Input/          # KeyMap, KeyBinding, commands and the widget state classes
│   │   ├── Widgets/        # Theme, the Widget factory and one file per widget builder
│   │   └── PublicAPI.*.txt # The tracked public API (Microsoft.CodeAnalysis.PublicApiAnalyzers)
│   └── MiniTui.Example/
│       ├── Program.cs      # Creates the app and pushes the menu screen
│       └── Screens/        # One screen per demo, plus shared controls
├── tests/
│   └── MiniTui.Tests/      # xUnit tests
├── docs/                   # SPEC.md (1.0), SPEC-1.x.md (post-1.0 roadmap) and the 0.7 → 1.0 migration guide
├── tools/
│   ├── GenWidthTable.cs    # Generates the Unicode width table
│   └── ssh-test/           # An SSH server in Docker for manual QA over SSH
└── README.md
```

## Building and Testing

```bash
dotnet build src/MiniTui.sln -warnaserror
dotnet test src/MiniTui.sln
```

CI builds and tests on Ubuntu, Windows and macOS and requires at least 85% line coverage of `src/MiniTui`.
A change to the public API fails the build (RS0016/RS0017) until `PublicAPI.Unshipped.txt` is updated.

The Unicode width table (`src/MiniTui/Text/UnicodeWidthTable.g.cs`) is generated from the Unicode
Character Database. To regenerate it, run from the repository root:

```bash
dotnet run tools/GenWidthTable.cs
```

To check input over SSH (with optional network delay), `tools/ssh-test/run.sh` starts an SSH server in Docker
with the Example in it; see [tools/ssh-test/README.md](tools/ssh-test/README.md).

## License

MIT, © 2026 Discover Edge LLC. See [LICENSE](LICENSE).
