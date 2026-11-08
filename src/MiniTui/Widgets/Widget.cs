namespace MiniTui;

/// <summary>
/// Factory methods for widget builders.
/// </summary>
public static class Widget
{
    /// <summary>A border with titles and padding.</summary>
    public static BlockBuilder Block() => new();
    /// <summary>Wrapped, aligned, scrollable text.</summary>
    public static ParagraphBuilder Paragraph() => new();
    /// <summary>Create a new list widget builder.</summary>
    public static ListBuilder List() => new();
    /// <summary>A table with constraint-sized columns.</summary>
    public static TableBuilder Table() => new();
    /// <summary>A bordered, scrollable tree over <paramref name="state"/>'s nodes.</summary>
    public static TreeBuilder<T> Tree<T>(TreeState<T> state) where T : class => new(state);
    /// <summary>Two panes with a divider the user can drag, from <paramref name="state"/>.</summary>
    public static SplitBuilder Split(SplitState state) => new(state);
    /// <summary>A scrollbar.</summary>
    public static ScrollbarBuilder Scrollbar() => new();
    /// <summary>A progress bar.</summary>
    public static GaugeBuilder Gauge() => new();
    /// <summary>A bar chart of recent values.</summary>
    public static SparklineBuilder Sparkline() => new();
    /// <summary>A drawing surface with braille dots (1.x spec §8.3).</summary>
    public static CanvasBuilder Canvas() => new();
    /// <summary>A line or scatter plot with axes (1.x spec §8.4).</summary>
    public static ChartBuilder Chart() => new();
    /// <summary>Labelled bars, vertical or horizontal (1.x spec §8.5).</summary>
    public static BarChartBuilder BarChart() => new();
    /// <summary>An activity indicator.</summary>
    public static SpinnerBuilder Spinner() => new();
    /// <summary>Pop-up messages from a <see cref="ToastQueue"/>.</summary>
    public static ToastsBuilder Toasts() => new();
    /// <summary>Create a new status bar widget builder.</summary>
    public static StatusBarBuilder StatusBar() => new();
    /// <summary>A list narrowed by typing: a filter line over the rows of a <see cref="FilterListState"/> that match it (1.x spec §25.5).</summary>
    public static FilterListBuilder FilterList(FilterListState state) => new(state);
    /// <summary>Markdown, formatted (1.x spec §27.3).</summary>
    public static MarkdownBuilder Markdown(MarkdownState state) => new(state);
    /// <summary>A single-line text input.</summary>
    public static TextFieldBuilder TextField() => new();

    /// <summary>A multi-line text input with wrapping and vertical scrolling (1.x spec §6).</summary>
    public static TextAreaBuilder TextArea() => new();

    /// <summary>A dropdown field that opens a menu of its options (1.x spec §7.5).</summary>
    public static SelectBuilder Select() => new();

    /// <summary>A command input with a prompt and history.</summary>
    public static CommandLineBuilder CommandLine() => new();
    /// <summary>A row of tabs.</summary>
    public static TabBarBuilder TabBar() => new();
    /// <summary>A button.</summary>
    public static ButtonBuilder Button() => new();
    /// <summary>A checkbox.</summary>
    public static CheckboxBuilder Checkbox() => new();
}
