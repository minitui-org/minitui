namespace MiniTui;

/// <summary>
/// The styles widgets draw with (§8.1). Backgrounds are left to the terminal except for bars, buttons and
/// selections, so light and custom terminal themes show through.
/// </summary>
/// <remarks>
/// Change a few slots with <c>Theme.Default with { Selection = ... }</c>, and apply a theme with <see cref="App.WithTheme(Theme)"/>.
/// </remarks>
public sealed record Theme
{
    [ThreadStatic] private static Theme? t_current;

    private static readonly IReadOnlyList<Style> DefaultChartSeries =
    [
        new Style(Color.Cyan), new Style(Color.Yellow), new Style(Color.Magenta),
        new Style(Color.Green), new Style(Color.Blue), new Style(Color.Red),
    ];

    /// <summary>Plain text.</summary>
    public Style Text { get; init; }
    /// <summary>Secondary text, such as placeholders and footers.</summary>
    public Style TextMuted { get; init; }
    /// <summary>Borders of unfocused blocks.</summary>
    public Style Border { get; init; }
    /// <summary>Borders of focused blocks.</summary>
    public Style BorderFocused { get; init; }
    /// <summary>Titles of unfocused blocks.</summary>
    public Style Title { get; init; }
    /// <summary>Titles of focused blocks.</summary>
    public Style TitleFocused { get; init; }
    /// <summary>The selected row of a focused list or table.</summary>
    public Style Selection { get; init; }
    /// <summary>The selected row of an unfocused list or table.</summary>
    public Style SelectionUnfocused { get; init; }
    /// <summary>Matches of a text area's find text other than the current one, which is the selection (1.x spec §16.5).</summary>
    public Style SearchMatch { get; init; }
    /// <summary>What's under the pointer, with hover on: rows, buttons, checkboxes and tabs (1.x spec §17.6).</summary>
    public Style Hover { get; init; }
    /// <summary>Table header cells.</summary>
    public Style TableHeader { get; init; }
    /// <summary>Text in an unfocused input.</summary>
    public Style Input { get; init; }
    /// <summary>Text in a focused input.</summary>
    public Style InputFocused { get; init; }
    /// <summary>The prompt of a command line.</summary>
    public Style Prompt { get; init; }
    /// <summary>The cursor cell of a focused input.</summary>
    public Style Cursor { get; init; }
    /// <summary>The status bar row.</summary>
    public Style StatusBar { get; init; }
    /// <summary>An unfocused button.</summary>
    public Style Button { get; init; }
    /// <summary>A focused button.</summary>
    public Style ButtonFocused { get; init; }
    /// <summary>The selected tab of a focused tab bar.</summary>
    public Style TabActive { get; init; }
    /// <summary>The selected tab of an unfocused tab bar.</summary>
    public Style TabActiveUnfocused { get; init; }
    /// <summary>Tabs that aren't selected.</summary>
    public Style TabInactive { get; init; }
    /// <summary>Modal and dialog borders.</summary>
    public Style ModalBorder { get; init; }
    /// <summary>Modal and dialog titles.</summary>
    public Style ModalTitle { get; init; }
    /// <summary>The scrollbar track.</summary>
    public Style ScrollbarTrack { get; init; }
    /// <summary>The scrollbar thumb.</summary>
    public Style ScrollbarThumb { get; init; }
    /// <summary>The filled part of a gauge.</summary>
    public Style GaugeFilled { get; init; }
    /// <summary>The empty part of a gauge.</summary>
    public Style GaugeEmpty { get; init; }
    /// <summary>Success messages and healthy values.</summary>
    public Style Success { get; init; }
    /// <summary>Warnings.</summary>
    public Style Warning { get; init; }
    /// <summary>Errors.</summary>
    public Style Error { get; init; }
    /// <summary>Informational messages.</summary>
    public Style Info { get; init; }
    /// <summary>Headings in Markdown (1.x spec §27.3).</summary>
    public Style Heading { get; init; }
    /// <summary>Inline code and code blocks in Markdown (1.x spec §27.3).</summary>
    public Style Code { get; init; }
    /// <summary>Links in Markdown (1.x spec §27.3).</summary>
    public Style Link { get; init; }
    /// <summary>
    /// The styles of chart datasets without their own, in order, wrapping around (1.x spec §8.2). Default: cyan,
    /// yellow, magenta, green, blue, red.
    /// </summary>
    public IReadOnlyList<Style> ChartSeries { get; init; } = DefaultChartSeries;

    /// <summary>The default colors.</summary>
    public static Theme Default { get; } = new()
    {
        Text = Style.Default,
        TextMuted = new Style(Color.DarkGray),
        Border = new Style(Color.DarkGray),
        BorderFocused = new Style(Color.Cyan),
        Title = new Style(Color.Cyan),
        TitleFocused = new Style(Color.Yellow),
        Selection = new Style(Color.Black, Color.Cyan),
        SelectionUnfocused = new Style(Color.Black, Color.DarkGray),
        SearchMatch = new Style(Color.Black, Color.Yellow),
        Hover = Style.Default.Underlined(),
        TableHeader = new Style(Color.Yellow).Bold(),
        Input = Style.Default,
        InputFocused = Style.Default,
        Prompt = new Style(Color.Cyan),
        Cursor = Style.Default.Reversed(),
        StatusBar = new Style(Color.White, Color.DarkGray),
        Button = new Style(Color.White, Color.DarkGray),
        ButtonFocused = new Style(Color.Black, Color.Cyan),
        TabActive = new Style(Color.Black, Color.Cyan),
        TabActiveUnfocused = new Style(Color.Black, Color.DarkGray),
        TabInactive = Style.Default,
        ModalBorder = new Style(Color.DarkGray),
        ModalTitle = new Style(Color.Cyan),
        ScrollbarTrack = new Style(Color.DarkGray),
        ScrollbarThumb = new Style(Color.DarkGray),
        GaugeFilled = new Style(Color.Green),
        GaugeEmpty = new Style(Color.DarkGray),
        Success = new Style(Color.Green),
        Warning = new Style(Color.Yellow),
        Error = new Style(Color.Red),
        Info = new Style(Color.Cyan),
        Heading = new Style(Color.Yellow).Bold(),
        Code = new Style(Color.Green),
        Link = new Style(Color.Cyan).Underlined(),
    };

    /// <summary>
    /// A dark theme with its own colors (1.x spec §12.4): the same in every terminal, whatever its palette. It sets
    /// the text and background colors too, and the app fills the screen with them.
    /// </summary>
    public static Theme Dark { get; } = new()
    {
        Text = new Style(Rgb("#D4D7DD"), Rgb("#1C1F26")),
        TextMuted = new Style(Rgb("#7D8590")),
        Border = new Style(Rgb("#4A5160")),
        BorderFocused = new Style(Rgb("#5FA8FF")),
        Title = new Style(Rgb("#5FA8FF")),
        TitleFocused = new Style(Rgb("#FFC66D")).Bold(),
        Selection = new Style(Rgb("#10131A"), Rgb("#5FA8FF")),
        SelectionUnfocused = new Style(Rgb("#D4D7DD"), Rgb("#353B46")),
        SearchMatch = new Style(Rgb("#10131A"), Rgb("#E5C07B")),
        Hover = new Style(Bg: Rgb("#2A303B")),
        TableHeader = new Style(Rgb("#FFC66D")).Bold(),
        Input = Style.Default,
        InputFocused = Style.Default,
        Prompt = new Style(Rgb("#5FA8FF")),
        Cursor = Style.Default.Reversed(),
        StatusBar = new Style(Rgb("#D4D7DD"), Rgb("#2A2F38")),
        Button = new Style(Rgb("#D4D7DD"), Rgb("#353B46")),
        ButtonFocused = new Style(Rgb("#10131A"), Rgb("#5FA8FF")),
        TabActive = new Style(Rgb("#10131A"), Rgb("#5FA8FF")),
        TabActiveUnfocused = new Style(Rgb("#D4D7DD"), Rgb("#353B46")),
        TabInactive = Style.Default,
        ModalBorder = new Style(Rgb("#5C6370")),
        ModalTitle = new Style(Rgb("#5FA8FF")),
        ScrollbarTrack = new Style(Rgb("#353B46")),
        ScrollbarThumb = new Style(Rgb("#7D8590")),
        GaugeFilled = new Style(Rgb("#7EC699")),
        GaugeEmpty = new Style(Rgb("#353B46")),
        Success = new Style(Rgb("#7EC699")),
        Warning = new Style(Rgb("#FFC66D")),
        Error = new Style(Rgb("#F2777A")),
        Info = new Style(Rgb("#66CCCC")),
        Heading = new Style(Rgb("#FFC66D")).Bold(),
        Code = new Style(Rgb("#66CCCC")),
        Link = new Style(Rgb("#5FA8FF")).Underlined(),
        ChartSeries =
        [
            new Style(Rgb("#66CCCC")), new Style(Rgb("#FFC66D")), new Style(Rgb("#C594C5")),
            new Style(Rgb("#7EC699")), new Style(Rgb("#5FA8FF")), new Style(Rgb("#F2777A")),
        ],
    };

    /// <summary>
    /// A light theme with its own colors (1.x spec §12.4): the same in every terminal, whatever its palette. It sets
    /// the text and background colors too, and the app fills the screen with them.
    /// </summary>
    public static Theme Light { get; } = new()
    {
        Text = new Style(Rgb("#24292F"), Rgb("#FAFAFA")),
        TextMuted = new Style(Rgb("#6E7781")),
        Border = new Style(Rgb("#B4BBC3")),
        BorderFocused = new Style(Rgb("#0B5CAD")),
        Title = new Style(Rgb("#0B5CAD")),
        TitleFocused = new Style(Rgb("#0B5CAD")).Bold(),
        Selection = new Style(Rgb("#FFFFFF"), Rgb("#0B5CAD")),
        SelectionUnfocused = new Style(Rgb("#24292F"), Rgb("#D8DEE4")),
        SearchMatch = new Style(Rgb("#24292F"), Rgb("#FFF2A8")),
        Hover = new Style(Bg: Rgb("#E8ECF0")),
        TableHeader = new Style(Rgb("#0B5CAD")).Bold(),
        Input = Style.Default,
        InputFocused = Style.Default,
        Prompt = new Style(Rgb("#0B5CAD")),
        Cursor = Style.Default.Reversed(),
        StatusBar = new Style(Rgb("#24292F"), Rgb("#E4E7EB")),
        Button = new Style(Rgb("#24292F"), Rgb("#D8DEE4")),
        ButtonFocused = new Style(Rgb("#FFFFFF"), Rgb("#0B5CAD")),
        TabActive = new Style(Rgb("#FFFFFF"), Rgb("#0B5CAD")),
        TabActiveUnfocused = new Style(Rgb("#24292F"), Rgb("#D8DEE4")),
        TabInactive = Style.Default,
        ModalBorder = new Style(Rgb("#8C959F")),
        ModalTitle = new Style(Rgb("#0B5CAD")),
        ScrollbarTrack = new Style(Rgb("#D8DEE4")),
        ScrollbarThumb = new Style(Rgb("#6E7781")),
        GaugeFilled = new Style(Rgb("#1A7F37")),
        GaugeEmpty = new Style(Rgb("#D8DEE4")),
        Success = new Style(Rgb("#1A7F37")),
        Warning = new Style(Rgb("#9A6700")),
        Error = new Style(Rgb("#CF222E")),
        Info = new Style(Rgb("#1B7C83")),
        Heading = new Style(Rgb("#0B5CAD")).Bold(),
        Code = new Style(Rgb("#1B7C83")),
        Link = new Style(Rgb("#8250DF")).Underlined(),
        ChartSeries =
        [
            new Style(Rgb("#1B7C83")), new Style(Rgb("#9A6700")), new Style(Rgb("#8250DF")),
            new Style(Rgb("#1A7F37")), new Style(Rgb("#0B5CAD")), new Style(Rgb("#CF222E")),
        ],
    };

    /// <summary>No colors, only modifiers. Used automatically when the terminal has no colors (<c>NO_COLOR</c>).</summary>
    public static Theme Monochrome { get; } = new()
    {
        Text = Style.Default,
        TextMuted = Style.Default.Dim(),
        Border = Style.Default,
        BorderFocused = Style.Default.Bold(),
        Title = Style.Default,
        TitleFocused = Style.Default.Bold(),
        Selection = Style.Default.Reversed(),
        SelectionUnfocused = Style.Default.Underlined(),
        SearchMatch = Style.Default.Underlined(),
        Hover = Style.Default.Underlined(),
        TableHeader = Style.Default.Bold(),
        Input = Style.Default,
        InputFocused = Style.Default,
        Prompt = Style.Default.Bold(),
        Cursor = Style.Default.Reversed(),
        StatusBar = Style.Default.Reversed(),
        Button = Style.Default,
        ButtonFocused = Style.Default.Reversed(),
        TabActive = Style.Default.Reversed(),
        TabActiveUnfocused = Style.Default.Underlined(),
        TabInactive = Style.Default,
        ModalBorder = Style.Default,
        ModalTitle = Style.Default.Bold(),
        ScrollbarTrack = Style.Default.Dim(),
        ScrollbarThumb = Style.Default,
        GaugeFilled = Style.Default,
        GaugeEmpty = Style.Default.Dim(),
        Success = Style.Default,
        Warning = Style.Default.Bold(),
        Error = Style.Default.Bold().Underlined(),
        Info = Style.Default,
        Heading = Style.Default.Bold(),
        Code = Style.Default.Dim(),
        Link = Style.Default.Underlined(),
        ChartSeries = [Style.Default],
    };

    private static Color Rgb(string hex) => Color.FromHex(hex);

    /// <summary>
    /// The theme widgets draw with: the running app's theme on its UI thread, otherwise <see cref="Default"/>.
    /// </summary>
    public static Theme Current => t_current ?? Default;

    /// <summary>Change the current theme inside an active <see cref="Use"/> scope, which still restores the previous one.</summary>
    internal static void ReplaceCurrent(Theme theme) => t_current = theme;

    /// <summary>Make <paramref name="theme"/> current on this thread until the scope is disposed.</summary>
    internal static Scope Use(Theme theme) => new(theme);

    internal readonly struct Scope : IDisposable
    {
        private readonly Theme? _previous;

        public Scope(Theme theme)
        {
            _previous = t_current;
            t_current = theme;
        }

        public void Dispose() => t_current = _previous;
    }
}
