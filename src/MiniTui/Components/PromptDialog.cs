namespace MiniTui;

/// <summary>
/// Asks for a line of text, used by <see cref="OverlayStack.PromptAsync"/> (1.x spec §25.4). Enter answers the text,
/// unless the validation has a problem with it, which shows under the field and keeps the dialog open; Esc and
/// closing the dialog answer null.
/// </summary>
public sealed class PromptDialog : Overlay
{
    private const int FieldWidth = 40;
    private const int MaxProblemRows = 3;

    private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _title;
    private readonly Func<string, string?>? _validate;
    private readonly string? _placeholder;
    private readonly TextFieldState _field;
    private string? _problem;

    /// <summary>
    /// Create a dialog. Show it with <see cref="OverlayStack.Show"/> and await <see cref="Result"/>.
    /// </summary>
    /// <param name="title">The title in the border.</param>
    /// <param name="text">The text to start with, with the cursor at its end.</param>
    /// <param name="validate">
    /// Given the text on Enter, a message saying what's wrong with it, or null to accept it. Its exceptions go to
    /// <see cref="App.OnError"/>, leaving the dialog open.
    /// </param>
    /// <param name="placeholder">Muted text shown while the field is empty.</param>
    public PromptDialog(string title, string text = "", Func<string, string?>? validate = null, string? placeholder = null)
    {
        _title = title ?? "";
        _validate = validate;
        _placeholder = placeholder;
        _field = new TextFieldState(text ?? "");
        _field.Changed += (_, _) => _problem = null;
    }

    /// <summary>The answer: the text on Enter, or null for Esc or when the dialog is closed any other way.</summary>
    public Task<string?> Result => _result.Task;

    /// <summary>The validation's message for the text last entered, until the text changes; null for none.</summary>
    public string? Problem => _problem;

    /// <inheritdoc/>
    public override string? Title => _title;

    /// <inheritdoc/>
    public override bool CapturesText => true;

    /// <inheritdoc/>
    public override Rect Placement(Rect screen)
    {
        // The border and a column of padding each side around the field
        var width = Math.Min(FieldWidth + 4, Math.Max(0, screen.Width - 4));
        return Centered(screen, width, 3 + ProblemRows(Math.Max(1, width - 4)));
    }

    /// <inheritdoc/>
    protected override void OnMount()
    {
        Keys.Add("enter", "OK", Submit).Add("esc", "Cancel", () => Answer(null));
    }

    /// <inheritdoc/>
    public override bool HandleKey(ConsoleKeyInfo key)
    {
        // Enter, and Esc without a selection, are the bindings'
        if (key.Key == ConsoleKey.Enter) return false;
        if (key.Key == ConsoleKey.Escape && !_field.HasSelection) return false;
        return _field.HandleKey(key);
    }

    /// <inheritdoc/>
    public override bool HandlePaste(string text) => _field.Paste(text);

    /// <inheritdoc/>
    public override bool HandleMouse(MouseEvent mouse) => _field.HandleMouse(mouse);

    /// <inheritdoc/>
    public override void Render(CellBuffer buf, Rect area, RenderContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (area.IsEmpty) return;
        Widget.TextField().State(_field).Focused(ctx.Focused).Placeholder(_placeholder).Draw(buf, area with { Height = 1 });
        if (_problem is { } problem && area.Height > 1)
        {
            Widget.Paragraph().Text(new Line(problem)).Wrap(WrapMode.Word).Style(ctx.Theme.Error)
                .Draw(buf, area with { Y = area.Y + 1, Height = Math.Min(MaxProblemRows, area.Height - 1) });
        }
    }

    /// <inheritdoc/>
    protected override void OnUnmount()
    {
        _result.TrySetResult(null);
    }

    private int ProblemRows(int width) =>
        _problem is { } problem ? Math.Min(MaxProblemRows, Paragraph.MeasureLines(problem, width, WrapMode.Word)) : 0;

    // A validation that throws leaves the dialog open, and the exception goes to OnError like any key handler's
    private void Submit()
    {
        var text = _field.Text;
        if (_validate?.Invoke(text) is { } problem)
        {
            _problem = problem;
            return;
        }
        Answer(text);
    }

    private void Answer(string? text)
    {
        _result.TrySetResult(text);
        Close();
    }
}
