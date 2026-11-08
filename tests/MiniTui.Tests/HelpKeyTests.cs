namespace MiniTui.Tests;

/// <summary>F1 for help, and hints that show a key that can fire (1.x spec §26).</summary>
public sealed class HelpKeyTests
{
    private sealed class Field : Component
    {
        public TextFieldState State { get; } = new();
        public override bool CapturesText => true;
        public override bool HandleKey(ConsoleKeyInfo key) => State.HandleKey(key);
        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
            Widget.TextField().State(State).Focused(ctx.Focused).Draw(buf, area with { Height = 1 });
    }

    private sealed class Editor : Component
    {
        public TextAreaState State { get; } = new("");
        public override bool CapturesText => true;
        public override bool HandleKey(ConsoleKeyInfo key) => State.HandleKey(key);
        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) =>
            Widget.TextArea().State(State).Focused(ctx.Focused).Draw(buf, area);
    }

    private sealed class Plain : Component
    {
        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) { }
    }

    private sealed class OneChild : Screen
    {
        private readonly Component _child;

        public OneChild(Component child)
        {
            _child = child;
            Focus.Add(AddChild(child));
        }

        public override void Render(CellBuffer buf, Rect area, RenderContext ctx) => _child.Render(buf, area, ctx.For(_child));
    }

    [Fact]
    public void F1_opens_and_closes_help_while_a_text_field_has_focus_and_question_mark_is_typed()
    {
        var field = new Field();
        using var harness = new ComponentHarness(new OneChild(field));

        harness.Press("?");
        Assert.False(harness.App.Overlays.Any);
        Assert.Equal("?", field.State.Text);

        harness.Press("f1");
        Assert.IsType<HelpOverlay>(harness.App.Overlays.Top);
        harness.Press("f1");
        Assert.False(harness.App.Overlays.Any);
        Assert.Equal("?", field.State.Text);
    }

    [Fact]
    public void F1_opens_help_from_a_text_area_without_changing_its_text()
    {
        var editor = new Editor();
        using var harness = new ComponentHarness(new OneChild(editor));
        harness.Type("notes");

        harness.Press("f1");

        Assert.IsType<HelpOverlay>(harness.App.Overlays.Top);
        Assert.Equal("notes", editor.State.Text);
    }

    [Fact]
    public void Question_mark_still_opens_help_without_text_capture_and_help_lists_both_keys()
    {
        using var harness = new ComponentHarness(new OneChild(new Plain()));
        Assert.Contains("  ?, f1   Help", KeyRouter.HelpLines(harness.App));

        harness.Press("?");
        Assert.IsType<HelpOverlay>(harness.App.Overlays.Top);
        Assert.Contains(harness.Lines, l => l.Contains("?, f1", StringComparison.Ordinal));
    }

    [Fact]
    public void An_app_binding_F1_keeps_it_and_help_stays_on_question_mark()
    {
        var screen = new OneChild(new Plain());
        var pressed = 0;
        screen.Keys.Add("f1", "Manual", () => pressed++);
        using var harness = new ComponentHarness(screen);

        harness.Press("f1");
        Assert.Equal(1, pressed);
        Assert.False(harness.App.Overlays.Any);
        Assert.Contains("? Help", KeyRouter.Hints(harness.App, 200), StringComparison.Ordinal);
    }

    [Fact]
    public void Hints_while_typing_show_keys_that_fire_and_leave_out_plain_characters_that_would_be_typed()
    {
        var field = new Field();
        field.Keys.Add("+", "Grow", () => { });                 // the field's own: still fires
        var screen = new OneChild(field);
        screen.Keys.Add("r", "Refresh", () => { }).Add("ctrl+s", "Save", () => { }).Add("d,delete", "Delete", () => { });
        using var harness = new ComponentHarness(screen);
        harness.App.Keys.Add("q", "Quit", harness.App.Quit);

        Assert.Equal("+ Grow  ctrl+s Save  delete Delete  f1 Help", KeyRouter.Hints(harness.App, 200));
    }

    [Fact]
    public void Hints_without_text_capture_show_each_bindings_first_key_as_before()
    {
        var screen = new OneChild(new Plain());
        screen.Keys.Add("r", "Refresh", () => { }).Add("d,delete", "Delete", () => { });
        using var harness = new ComponentHarness(screen);
        harness.App.Keys.Add("q", "Quit", harness.App.Quit);

        Assert.Equal("r Refresh  d Delete  ? Help  q Quit", KeyRouter.Hints(harness.App, 200));
    }
}
