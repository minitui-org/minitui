using Microsoft.Extensions.Time.Testing;

namespace MiniTui.Tests;

/// <summary>The text prompt and wrapped toasts (1.x spec §25.4, §25.7).</summary>
public sealed class PromptTests
{
    private static (ComponentHarness Harness, Func<(bool Done, string? Answer)> Result) Prompt(
        string text = "", Func<string, string?>? validate = null, int width = 60, int height = 12, string? placeholder = null)
    {
        var harness = new ComponentHarness(new ProbeScreen([], "A"), width, height);
        var done = false;
        string? answer = null;
        _ = harness.App.RunTask(async _ =>
        {
            answer = await harness.App.Overlays.PromptAsync("New note", text, validate, placeholder);
            Assert.True(harness.App.IsUiThread);
            done = true;
        });
        harness.RunUntilIdle();
        return (harness, () => (done, answer));
    }

    private static string Row(ComponentHarness harness, string containing) =>
        harness.Lines.Single(l => l.Contains(containing, StringComparison.Ordinal));

    [Fact]
    public void Enter_answers_the_text_typed_after_the_initial_text()
    {
        var (harness, result) = Prompt("Shop");
        using var _ = harness;
        Assert.Contains("New note", Row(harness, "New note"), StringComparison.Ordinal);

        harness.Type("ping");
        harness.Press("enter");

        Assert.Equal((true, "Shopping"), result());
        Assert.False(harness.App.Overlays.Any);
    }

    [Fact]
    public void Esc_answers_null()
    {
        var (harness, result) = Prompt("Shop");
        using var _ = harness;
        harness.Press("esc");
        Assert.Equal((true, (string?)null), result());
        Assert.False(harness.App.Overlays.Any);
    }

    [Fact]
    public void Esc_with_a_selection_clears_it_first()
    {
        var (harness, result) = Prompt("Shop");
        using var _ = harness;
        harness.Press("shift+left esc");
        Assert.False(result().Done);
        harness.Press("esc");
        Assert.True(result().Done);
    }

    [Fact]
    public void Closing_it_answers_null()
    {
        var (harness, result) = Prompt();
        using var _ = harness;
        harness.App.Overlays.Close(harness.App.Overlays.Top!);
        harness.RunUntilIdle();
        Assert.Equal((true, (string?)null), result());
    }

    [Fact]
    public void A_problem_shows_under_the_field_keeps_it_open_and_goes_when_the_text_changes()
    {
        var checkedTexts = new List<string>();
        var (harness, result) = Prompt("Plan", text =>
        {
            checkedTexts.Add(text);
            return text == "Plan" ? "There's already a note with that name" : null;
        });
        using var _ = harness;
        Assert.Empty(checkedTexts);                 // only on Enter

        harness.Press("enter");
        Assert.False(result().Done);
        var dialog = Assert.IsType<PromptDialog>(harness.App.Overlays.Top);
        Assert.Equal("There's already a note with that name", dialog.Problem);
        var field = Array.FindIndex(harness.Lines, l => l.Contains("Plan", StringComparison.Ordinal));
        Assert.Contains("There's already a note with that name", harness.Lines[field + 1], StringComparison.Ordinal);

        harness.Type("s");
        Assert.Null(dialog.Problem);
        Assert.DoesNotContain(harness.Lines, l => l.Contains("already", StringComparison.Ordinal));

        harness.Press("enter");
        Assert.Equal((true, "Plans"), result());
        Assert.Equal(["Plan", "Plans"], checkedTexts);
    }

    [Fact]
    public void A_long_problem_wraps_to_three_rows_at_most()
    {
        var (harness, _) = Prompt("x", _ => string.Join(' ', Enumerable.Repeat("word", 60)));
        using var __ = harness;
        harness.Press("enter");
        Assert.Equal(3, harness.Lines.Count(l => l.Contains("word", StringComparison.Ordinal)));
        Assert.Equal(3 + 3, harness.App.Overlays.Top!.Placement(new Rect(0, 0, 60, 11)).Height);
    }

    [Fact]
    public void A_validation_that_throws_goes_to_OnError_and_leaves_it_open()
    {
        var (harness, result) = Prompt("x", _ => throw new IOException("disk"));
        using var _ = harness;
        var errors = new List<Exception>();
        harness.App.OnError(e =>
        {
            errors.Add(e);
            return ErrorAction.Continue;
        });

        harness.Press("enter");

        Assert.IsType<IOException>(Assert.Single(errors));
        Assert.False(result().Done);
        Assert.True(harness.App.Overlays.Any);
    }

    [Fact]
    public void A_paste_with_line_breaks_is_one_line()
    {
        var (harness, result) = Prompt();
        using var _ = harness;
        harness.Paste("two\nlines");
        harness.Press("enter");
        Assert.Equal((true, "two lines"), result());
    }

    [Fact]
    public void It_is_44_columns_wide_or_the_screen_less_4_and_shows_the_placeholder_and_keys()
    {
        var (wide, _) = Prompt(placeholder: "Name");
        using (wide)
        {
            var row = Row(wide, "Name");
            Assert.Equal(44, row.Trim().Length);
            Assert.Contains("enter OK", wide.Lines[^1], StringComparison.Ordinal);
            Assert.Contains("esc Cancel", wide.Lines[^1], StringComparison.Ordinal);
        }

        var (narrow, _) = Prompt(placeholder: "Name", width: 30);
        using (narrow)
            Assert.Equal(26, Row(narrow, "Name").Trim().Length);
    }

    // ---- Toasts (§25.7) ----

    private static string[] DrawToasts(ToastQueue queue, int width = 70, int height = 16)
    {
        var buf = new CellBuffer(width, height);
        Widget.Toasts().Queue(queue).Draw(buf, buf.Area);
        return [.. buf.ToLines().Select(l => l.TrimEnd())];
    }

    [Fact]
    public void A_short_toast_is_one_line_as_before()
    {
        var queue = new ToastQueue(new FakeTimeProvider());
        queue.Push("Saved");
        var lines = DrawToasts(queue);
        Assert.Equal("╭───────╮", lines[0].Trim());
        Assert.Equal("│ Saved │", lines[1].Trim());
        Assert.Equal("╰───────╯", lines[2].Trim());
    }

    [Fact]
    public void A_long_toast_wraps_at_words_within_60_columns()
    {
        var queue = new ToastQueue(new FakeTimeProvider());
        var message = "Can't save “Quarterly plan for the platform team”: Permission denied (the folder is read-only)";
        queue.Push(message, ToastLevel.Error);

        var lines = DrawToasts(queue);

        var rows = lines.TakeWhile(l => l.Length > 0).ToArray();
        Assert.Equal(4, rows.Length);                      // two lines of text and the border
        Assert.All(rows, r => Assert.True(r.Trim().Length <= 60, r));
        var text = string.Join(' ', rows[1..^1].Select(r => r.Trim().Trim('│').Trim()));
        Assert.Equal(message, text);
    }

    [Fact]
    public void A_toast_longer_than_three_lines_is_cut_at_the_end_of_the_third()
    {
        var queue = new ToastQueue(new FakeTimeProvider());
        queue.Push(string.Join(' ', Enumerable.Range(1, 80).Select(i => $"w{i}")));

        var rows = DrawToasts(queue).TakeWhile(l => l.Length > 0).ToArray();

        Assert.Equal(5, rows.Length);
        Assert.EndsWith("… │", rows[3], StringComparison.Ordinal);
        Assert.Contains(" w1 ", rows[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_line_break_starts_a_new_line()
    {
        var queue = new ToastQueue(new FakeTimeProvider());
        queue.Push("Saved\nto disk");
        var rows = DrawToasts(queue).TakeWhile(l => l.Length > 0).ToArray();
        Assert.Equal(["│ Saved   │", "│ to disk │"], rows[1..3].Select(r => r.Trim()));
    }

    [Fact]
    public void Toasts_stack_each_as_tall_as_its_text_and_one_that_does_not_fit_waits()
    {
        var queue = new ToastQueue(new FakeTimeProvider());
        queue.Push("one\ntwo");
        queue.Push("three");
        queue.Push("four\nfive\nsix");

        var lines = DrawToasts(queue, height: 10);

        Assert.Contains("one", lines[1], StringComparison.Ordinal);
        Assert.Contains("three", lines[5], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, l => l.Contains("four", StringComparison.Ordinal));     // needs 5 rows, 3 left
    }

    [Fact]
    public void A_narrow_screen_wraps_toasts_narrower()
    {
        var queue = new ToastQueue(new FakeTimeProvider());
        queue.Push("a message that is too long for this screen");
        var rows = DrawToasts(queue, width: 24).TakeWhile(l => l.Length > 0).ToArray();
        Assert.True(rows.Length > 3);
        Assert.All(rows, r => Assert.True(r.Length <= 23, r));
    }
}
