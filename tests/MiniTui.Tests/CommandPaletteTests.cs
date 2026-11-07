
namespace MiniTui.Tests;

/// <summary>CommandPalette, its default items and the filterable select (1.x spec §13).</summary>
public class CommandPaletteTests
{
    private sealed class Child(List<string> log) : Component
    {
        public override string KeysGroup => "Files";
        public Child Bind()
        {
            Keys.Add("r", "Rename", () => log.Add("rename"));
            Keys.Add("ctrl+o", "Open File", () => log.Add("open"));
            return this;
        }
        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) { }
    }

    private sealed class Home : Screen
    {
        public Home(List<string> log, CommandRegistry? commands = null)
        {
            Inner = AddChild(new Child(log).Bind());
            Focus.Add(Inner);
            Keys.Add("r", "Refresh", () => log.Add("refresh"));
            Keys.Add("s", "Save", () => log.Add("save"));
            Keys.Add("ctrl+p", "Commands", () => App.Overlays.ShowPalette(commands));
        }

        public Child Inner { get; }
        public override string Title => "Home";
        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) => ctx.Render(Inner, buf, area);
    }

    private static (ComponentHarness Harness, List<string> Log) Start(CommandRegistry? commands = null, int width = 60, int height = 24)
    {
        var log = new List<string>();
        var harness = new ComponentHarness(new Home(log, commands), width, height);
        harness.RunUntilIdle();
        return (harness, log);
    }

    private static CommandPalette Open(ComponentHarness harness)
    {
        harness.Press("ctrl+p");
        return Assert.IsType<CommandPalette>(harness.App.Overlays.Top);
    }

    private static List<string> Labels(CommandPalette palette, IReadOnlyList<PaletteItem> items) =>
        [.. palette.List.Matches.Select(i => items[i].Label)];

    private static int RowOf(ComponentHarness harness, string text) =>
        Array.FindIndex(harness.Lines, l => l.Contains(text, StringComparison.Ordinal));

    // The default items

    [Fact]
    public void ItemsFor_ListsTheBindingsInScope_InnermostFirst_WithGroupsAndKeys()
    {
        var (harness, _) = Start();
        using var _h = harness;

        var items = CommandPalette.ItemsFor(harness.App);

        Assert.Equal(["Rename", "Open File", "Save", "Commands", "Help", "Quit"], items.Select(i => i.Label));
        Assert.Equal(["Files", "Files", "Home", "Home", "Global", "Global"], items.Select(i => i.Detail));
        Assert.Equal(["r", "ctrl+o", "s", "ctrl+p", "?", "ctrl+c"], items.Select(i => i.Hint));
    }

    [Fact]
    public void ShadowedBindings_AreLeftOut_AndTheOpeningOneIsSkipped()
    {
        var (harness, _) = Start();
        using var _h = harness;

        var palette = Open(harness);

        // The screen's "r Refresh" is shadowed by the child's "r Rename"; "Commands" opened the palette
        var labels = palette.List.Matches.Count();
        Assert.Equal(5, labels);
        Assert.Equal(-1, RowOf(harness, "Refresh"));
        Assert.Equal(-1, RowOf(harness, "Commands  "));
        Assert.True(RowOf(harness, "Quit") > 0);
    }

    [Fact]
    public void ARegistry_AddsItsCommandsAndSubcommands_AfterTheBindings()
    {
        var registry = CommandRegistry.Create()
            .Add("clear").Description("Remove every note")
            .Add("add").Usage("<text>").Description("Add a note")
            .Add("tag").SubCommands(s => s.Add("list").Handler(_ => CommandResult.Ok("tags")))
            .Build();
        var (harness, _) = Start(registry);
        using var _h = harness;

        var items = CommandPalette.ItemsFor(harness.App, registry);

        Assert.Equal([":clear", ":add <text>", ":tag", ":tag list"], items.Skip(6).Select(i => i.Label));
        Assert.Equal("Remove every note", items[6].Detail);
        Assert.Null(items[6].Hint);
    }

    [Fact]
    public void ACommandWithoutUsage_Runs_AndItsMessageIsAToast()
    {
        var ran = 0;
        var registry = CommandRegistry.Create()
            .Add("clear").Handler(_ => { ran++; return CommandResult.Ok("Cleared"); })
            .Build();
        var (harness, _) = Start(registry);
        using var _h = harness;

        Open(harness);
        harness.Type(":clear");
        harness.Press("enter");

        Assert.Equal(1, ran);
        Assert.False(harness.App.Overlays.Any);
        Assert.True(RowOf(harness, "Cleared") >= 0);
    }

    [Fact]
    public void ACommandWithUsage_OpensTheCommandLine_WithItsNameTyped()
    {
        string? added = null;
        var registry = CommandRegistry.Create()
            .Add("add").Usage("<text>").Handler(c => { added = c.ArgsAsString; })
            .Build();
        var (harness, _) = Start(registry);
        using var _h = harness;

        Open(harness);
        harness.Type("add");
        harness.Press("enter");

        Assert.IsType<CommandLineOverlay>(harness.App.Overlays.Top);
        harness.Type("milk");
        harness.Press("enter");
        Assert.Equal("milk", added);
    }

    [Fact]
    public void QuitResult_Quits()
    {
        var registry = CommandRegistry.Create().Add("quit").Handler(_ => CommandResult.Quit()).Build();
        var (harness, _) = Start(registry);
        using var _h = harness;

        Open(harness);
        harness.Type(":quit");
        harness.Press("enter");

        Assert.True(harness.App.Stopping.IsCancellationRequested);
    }

    // Filtering and keys

    [Fact]
    public void Typing_FiltersAndOrders_TheBestMatchSelected()
    {
        var log = new List<string>();
        PaletteItem[] items = [new("Rename File", () => { }), new("Refresh", () => { }), new("Save", () => { }), new("Rarefy", () => { })];
        var harness = new ComponentHarness(new Home(log), 60, 24);
        using var _h = harness;
        var palette = new CommandPalette(items);
        harness.App.Overlays.Show(palette);
        harness.RunUntilIdle();

        harness.Type("rf");

        Assert.Equal(["Rename File", "Refresh", "Rarefy"], Labels(palette, items));
        Assert.Equal(0, palette.List.Selected);

        harness.Type("r");
        Assert.Equal(["Refresh"], Labels(palette, items));
        harness.Press("backspace backspace backspace");
        Assert.Equal(["Rename File", "Refresh", "Save", "Rarefy"], Labels(palette, items));
    }

    [Fact]
    public void TheArrowsWrap_PagesAndCtrlHomeEndMove()
    {
        PaletteItem[] items = [.. Enumerable.Range(1, 30).Select(i => new PaletteItem($"Item {i}", () => { }))];
        var harness = new ComponentHarness(new Home([]), 60, 24);
        using var _h = harness;
        var palette = new CommandPalette(items);
        harness.App.Overlays.Show(palette);
        harness.RunUntilIdle();

        harness.Press("up");
        Assert.Equal(29, palette.List.Selected);
        harness.Press("down");
        Assert.Equal(0, palette.List.Selected);
        harness.Press("pgdn");
        Assert.Equal(12, palette.List.Selected);
        harness.Press("ctrl+end");
        Assert.Equal(29, palette.List.Selected);
        harness.Press("ctrl+home");
        Assert.Equal(0, palette.List.Selected);
        harness.Press("pgup");
        Assert.Equal(0, palette.List.Selected);
    }

    [Fact]
    public void TextFieldKeys_ReachTheFilter()
    {
        var (harness, _) = Start();
        using var _h = harness;
        var palette = Open(harness);

        harness.Type("savx");
        harness.Press("backspace home shift+end");
        Assert.True(palette.List.Filter.HasSelection);
        harness.Paste("open");

        Assert.Equal("open", palette.List.Filter.Text);
        Assert.Single(palette.List.Matches);
    }

    [Fact]
    public async Task Enter_RunsTheItem_AfterThePaletteClosed()
    {
        var (harness, log) = Start();
        using var _h = harness;
        var palette = Open(harness);
        var closedWhenRun = false;
        log.Clear();

        harness.Type("open");
        harness.Press("enter");

        Assert.Equal(["open"], log);
        Assert.False(harness.App.Overlays.Any);
        Assert.Equal(1, await palette.Result);

        // An item sees the screen's focus chain, not the palette's
        PaletteItem[] items = [new("Check", () => closedWhenRun = !harness.App.Overlays.Any)];
        harness.App.Overlays.Show(new CommandPalette(items));
        harness.RunUntilIdle();
        harness.Press("enter");
        Assert.True(closedWhenRun);
    }

    [Theory]
    [InlineData("esc")]
    public async Task Esc_Closes_WithoutAChoice(string key)
    {
        var (harness, log) = Start();
        using var _h = harness;
        var palette = Open(harness);

        harness.Press(key);

        Assert.False(harness.App.Overlays.Any);
        Assert.Null(await palette.Result);
        Assert.Empty(log);
    }

    [Fact]
    public void TabAndLetters_StayInThePalette()
    {
        var (harness, log) = Start();
        using var _h = harness;
        Open(harness);

        harness.Press("tab");
        harness.Type("rs");

        Assert.IsType<CommandPalette>(harness.App.Overlays.Top);
        Assert.Empty(log);
    }

    // Drawing and the mouse

    [Fact]
    public void Draws_TheFilterLine_ADivider_AndRows_WithDetailsAndHints()
    {
        var (harness, _) = Start();
        using var _h = harness;
        Open(harness);

        var palette = Assert.IsType<CommandPalette>(harness.App.Overlays.Top);
        Assert.Equal(new Rect(10, 2, 40, 9), palette.Area);
        var lines = harness.Lines.Select(l => l.TrimEnd()).ToArray();
        Assert.Equal("          ┌ Commands ────────────────────────────┐", lines[2]);
        Assert.Equal("          │ Type to search                       │", lines[3]);
        Assert.Equal("          ├──────────────────────────────────────┤", lines[4]);
        Assert.Equal("          │ Rename  Files                      r │", lines[5]);
        Assert.Equal("          │ Open File  Files              ctrl+o │", lines[6]);
        Assert.Equal("          │ Quit  Global                  ctrl+c │", lines[9]);
        Assert.Equal("          └──────────────────────────────────────┘", lines[10]);
    }

    [Fact]
    public void ThePalette_ShrinksToTheMatches_AndSaysWhenThereAreNone()
    {
        var (harness, _) = Start();
        using var _h = harness;
        var palette = Open(harness);

        harness.Type("zzz");

        Assert.Equal(5, palette.Area.Height);
        Assert.Contains("No matches", harness.Lines[5], StringComparison.Ordinal);
        harness.Press("enter");
        Assert.IsType<CommandPalette>(harness.App.Overlays.Top);
    }

    [Fact]
    public void MatchedCharacters_AreBold()
    {
        var (harness, _) = Start();
        using var _h = harness;
        Open(harness);

        harness.Type("of");

        // "Open File" is the only match, on the selected row: O and F are bold
        var row = RowOf(harness, "Open File");
        var x = harness.Lines[row].IndexOf("Open File", StringComparison.Ordinal);
        Assert.True(harness.CellAt(x, row).Modifiers.HasFlag(Modifiers.Bold));
        Assert.False(harness.CellAt(x + 1, row).Modifiers.HasFlag(Modifiers.Bold));
        Assert.True(harness.CellAt(x + 5, row).Modifiers.HasFlag(Modifiers.Bold));
    }

    [Fact]
    public void LongLabels_DropTheDetailFirst_ThenTruncate()
    {
        PaletteItem[] items = [new(new string('a', 60), () => { }) { Detail = "detail", Hint = "ctrl+x" }];
        var harness = new ComponentHarness(new Home([]), 60, 24);
        using var _h = harness;
        harness.App.Overlays.Show(new CommandPalette(items));
        harness.RunUntilIdle();

        var row = harness.Lines[5];
        Assert.Contains("aaa… ", row, StringComparison.Ordinal);
        Assert.DoesNotContain("detail", row, StringComparison.Ordinal);
        Assert.Contains("ctrl+x", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AClickOnARow_RunsIt_AndAClickOutside_Closes()
    {
        var (harness, log) = Start();
        using var _h = harness;
        Open(harness);

        harness.Click(15, 6);
        Assert.Equal(["open"], log);
        Assert.False(harness.App.Overlays.Any);

        var palette = Open(harness);
        harness.Click(1, 20);
        Assert.False(harness.App.Overlays.Any);
        Assert.Null(await palette.Result);
    }

    [Fact]
    public void TheWheel_MovesTheSelection()
    {
        var (harness, _) = Start();
        using var _h = harness;
        var palette = Open(harness);

        harness.Scroll(15, 6, 2);

        Assert.Equal(2, palette.List.Selected);
    }

    [Fact]
    public void OnASmallScreen_ItStartsAtTheTop_AndScrolls()
    {
        PaletteItem[] items = [.. Enumerable.Range(1, 30).Select(i => new PaletteItem($"Item {i}", () => { }))];
        var harness = new ComponentHarness(new Home([]), 30, 8);
        using var _h = harness;
        var palette = new CommandPalette(items);
        harness.App.Overlays.Show(palette);
        harness.RunUntilIdle();

        Assert.Equal(1, palette.Area.Y);
        Assert.Equal(30, palette.Area.Width);
        harness.Press("ctrl+end");
        Assert.Contains("Item 30", string.Join("\n", harness.Lines), StringComparison.Ordinal);
    }

    [Fact]
    public void NullItems_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new CommandPalette([new("a", () => { }), null!]));
    }

    // The filterable select

    private sealed class Form : Screen
    {
        public Form(SelectState state)
        {
            Field = AddChild(new SelectField(state));
            Focus.Add(Field);
            Keys.Add("g", "Go", () => Went++);
        }

        public SelectField Field { get; }
        public int Went { get; private set; }

        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) => ctx.Render(Field, buf, new Rect(2, 1, 24, 3));
    }

    private sealed class SelectField(SelectState state) : Component
    {
        public override bool HandleKey(ConsoleKeyInfo key) => state.HandleKey(key, App);
        public override bool HandleMouse(MouseEvent mouse) => state.HandleMouse(mouse, App);
        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
            Widget.Select().State(state).Placeholder("Pick").Block(Widget.Block().Title(" Country ")).Focused(ctx.Focused).Draw(buf, area);
    }

    private static readonly string[] Countries = ["Austria", "Belgium", "France", "Germany", "Greece", "Italy", "Spain"];

    private static (ComponentHarness Harness, SelectState State, Form Form, List<int> Changes) OpenForm(int? selected = null, bool filterable = true)
    {
        var state = new SelectState(Countries, selected) { Filterable = filterable };
        var changes = new List<int>();
        state.Changed += (_, _) => changes.Add(state.Selected ?? -1);
        var form = new Form(state);
        var harness = new ComponentHarness(form, 40, 24);
        harness.RunUntilIdle();
        return (harness, state, form, changes);
    }

    [Theory]
    [InlineData("enter")]
    [InlineData("space")]
    [InlineData("down")]
    [InlineData("f4")]
    public void Filterable_TheOpeningKeys_OpenTheFilterDropdown_UnderTheField(string key)
    {
        var (harness, state, _, _) = OpenForm();
        using var _h = harness;

        harness.Press(key);

        Assert.True(state.IsOpen);
        var menu = Assert.IsType<FilterMenu>(harness.App.Overlays.Top);
        Assert.Equal(new Rect(2, 4, 24, 11), menu.Area);
        Assert.Equal("", menu.List.Filter.Text);
        Assert.Contains("Type to filter", harness.Lines[5], StringComparison.Ordinal);
    }

    [Fact]
    public void Filterable_ATypedLetter_OpensIt_WithTheLetterInTheFilter()
    {
        var (harness, state, form, changes) = OpenForm();
        using var _h = harness;

        harness.Type("ger");

        var menu = Assert.IsType<FilterMenu>(harness.App.Overlays.Top);
        Assert.Equal("ger", menu.List.Filter.Text);
        Assert.Equal(0, form.Went);
        harness.Press("enter");
        Assert.Equal(3, state.Selected);
        Assert.Equal("Germany", state.Value);
        Assert.Equal([3], changes);
        Assert.False(state.IsOpen);
    }

    [Fact]
    public void Filterable_ChoosingMapsToTheIndexInOptions()
    {
        var (harness, state, _, _) = OpenForm();
        using var _h = harness;

        harness.Press("enter");
        harness.Type("sp");
        harness.Press("enter");

        Assert.Equal(6, state.Selected);
    }

    [Fact]
    public void Filterable_StartsOnTheChoice_ThenOnTheFirstMatch()
    {
        var (harness, _, _, _) = OpenForm(selected: 4);
        using var _h = harness;

        harness.Press("enter");
        var menu = Assert.IsType<FilterMenu>(harness.App.Overlays.Top);
        Assert.Equal(4, menu.List.Selected);

        harness.Type("a");
        Assert.Equal(0, menu.List.Selected);
    }

    [Theory]
    [InlineData("esc")]
    [InlineData("tab")]
    public void Filterable_EscAndTab_CloseWithoutAChange(string key)
    {
        var (harness, state, _, changes) = OpenForm(selected: 1);
        using var _h = harness;

        harness.Press("enter down");
        harness.Press(key);

        Assert.False(state.IsOpen);
        Assert.Equal(1, state.Selected);
        Assert.Empty(changes);
    }

    [Fact]
    public void Filterable_SpaceTypesASpace_OnceOpen()
    {
        var (harness, _, _, _) = OpenForm();
        using var _h = harness;

        harness.Press("enter");
        harness.Type("a b");

        Assert.Equal("a b", Assert.IsType<FilterMenu>(harness.App.Overlays.Top).List.Filter.Text);
    }

    [Fact]
    public void Filterable_AClickOnAnOption_ChoosesIt()
    {
        var (harness, state, _, _) = OpenForm();
        using var _h = harness;

        harness.Click(5, 2);
        harness.Click(5, 9); // the third option row, under the filter line and divider: France

        Assert.Equal(2, state.Selected);
    }

    [Fact]
    public void Filterable_OpensAbove_WhenThereIsNoRoomBelow_AndKeepsItsSide()
    {
        var state = new SelectState(Countries) { Filterable = true };
        var harness = new ComponentHarness(new Bottom(state), 40, 24);
        using var _h = harness;
        harness.RunUntilIdle();

        harness.Press("enter");
        var menu = Assert.IsType<FilterMenu>(harness.App.Overlays.Top);
        Assert.Equal(new Rect(2, 9, 24, 11), menu.Area);

        harness.Type("ger");
        Assert.Equal(new Rect(2, 15, 24, 5), menu.Area);
    }

    private sealed class Bottom : Screen
    {
        private readonly SelectField _field;

        public Bottom(SelectState state)
        {
            _field = AddChild(new SelectField(state));
            Focus.Add(_field);
        }

        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) => ctx.Render(_field, buf, new Rect(2, 20, 24, 3));
    }

    [Fact]
    public void APlainSelect_StillIgnoresLetters()
    {
        var (harness, state, form, _) = OpenForm(filterable: false);
        using var _h = harness;

        harness.Type("g");

        Assert.False(state.IsOpen);
        Assert.Equal(1, form.Went);
        harness.Press("enter");
        Assert.IsType<PopupMenu>(harness.App.Overlays.Top);
    }

    [Fact]
    public void Filterable_CtrlAndAltLetters_DontOpenIt()
    {
        var (harness, state, _, _) = OpenForm();
        using var _h = harness;

        harness.Press("alt+g ctrl+g");

        Assert.False(state.IsOpen);
    }
}
