# Theming

Colors come from a [`Theme`](xref:MiniTui.Theme): pick one, change a few slots, or follow the terminal's light or dark background.

The styles widgets draw with. `Theme.Default` uses the terminal's own palette (the 16 standard colors) and
leaves backgrounds to the terminal except for bars, buttons and selections, so it fits the user's profile. `Theme.Monochrome` uses only bold, underline
and reverse. When the terminal has no colors (`NO_COLOR`), the app uses it in place of `Theme.Default`, `Theme.Dark` and `Theme.Light`,
even if you chose `Theme.Default` explicitly, so the selection stays visible; custom themes are used as is.

```csharp
app.WithTheme(Theme.Default with { Selection = new Style(Color.Black, Color.Yellow) });
```

`Theme.SearchMatch` highlights a text area's find matches other than the current one, which is the selection.

`Theme.ChartSeries` lists the styles of chart datasets that don't set their own (cyan, yellow, magenta,
green, blue, red; Monochrome has one plain style, so datasets differ by marker).

`Theme.Dark` and `Theme.Light` have their own colors instead of the terminal's palette, so they look the
same in every terminal. They set the text and background colors too, and the app fills the screen with the
background. To follow the terminal, pass both: the app asks the terminal for its background color (see
[Terminal queries](terminal.md#terminal-queries)) and picks one, waiting up to 150 ms before the first frame so it doesn't
flash the wrong one. Until the terminal answers, and if it never does, the dark theme is used:

```csharp
app.WithTheme(Theme.Dark, Theme.Light);
```

In a terminal with only the 16 standard colors, their colors map to the nearest palette entries.

`Theme.Current` is the running app's theme on its UI thread. `WithTheme` can be called while running.
Builders take `.Style(...)` (and `HighlightStyle`, `BorderStyle`, …) overrides, patched over the theme.

## Theme slots

| Slot | Used for |
|---|---|
| `Text`, `TextMuted` | Text, and secondary text such as hints and placeholders |
| `Border`, `BorderFocused`, `Title`, `TitleFocused` | Block borders and titles, unfocused and focused |
| `Selection`, `SelectionUnfocused` | Selected rows and text, in a focused and an unfocused widget |
| `SearchMatch`, `Hover` | Find matches in a text area; the row or control under the mouse |
| `TableHeader` | Table headers |
| `Input`, `InputFocused`, `Prompt`, `Cursor` | Text fields, the command line's prompt, and the cursor |
| `StatusBar` | The status bar |
| `Button`, `ButtonFocused` | Buttons |
| `TabActive`, `TabActiveUnfocused`, `TabInactive` | Tab bars |
| `ModalBorder`, `ModalTitle` | Overlays and dialogs |
| `ScrollbarTrack`, `ScrollbarThumb` | Scrollbars |
| `GaugeFilled`, `GaugeEmpty` | Gauges |
| `Success`, `Warning`, `Error`, `Info` | Messages, toasts and your own status colors |
| `Heading`, `Code`, `Link` | Headings, code and links in Markdown |
| `ChartSeries` | Chart datasets and bar series without their own style |

A component reads them from `ctx.Theme`, and code outside one from `Theme.Current`.

## A theme switch

`WithTheme(theme)` replaces the pair, and `WithTheme(dark, light)` goes back to following the terminal:

```csharp
var dark = true;
app.Keys.Add("t", "Theme", () => app.WithTheme((dark = !dark) ? Theme.Dark : Theme.Light));
app.Keys.Add("a", "Follow the terminal", () => app.WithTheme(Theme.Dark, Theme.Light));
```
