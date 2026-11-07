namespace MiniTui;

/// <summary>
/// The kind of a toast, which sets its border color.
/// </summary>
public enum ToastLevel
{
    /// <summary>Information.</summary>
    Info,
    /// <summary>Something succeeded.</summary>
    Success,
    /// <summary>A warning.</summary>
    Warning,
    /// <summary>An error.</summary>
    Error,
}

/// <summary>
/// Short messages that pop up in the top-right corner and disappear on their own.
/// Draw them with <see cref="Widget.Toasts"/>. Thread-safe.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Name from the spec (§8.3); it is a queue of toasts.")]
public sealed class ToastQueue
{
    internal const int MaxVisible = 3;
    internal static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);

    // While a toast is showing, the app ticks this often so it disappears on time
    internal static readonly TimeSpan ExpiryCheck = TimeSpan.FromMilliseconds(250);

    private readonly Lock _lock = new();
    private readonly List<Toast> _toasts = [];
    private readonly TimeProvider? _time;

    /// <summary>Create a queue that uses the app's clock.</summary>
    public ToastQueue() { }

    /// <summary>Create a queue that uses <paramref name="time"/>, for example a fake clock in tests.</summary>
    public ToastQueue(TimeProvider time) => _time = time ?? throw new ArgumentNullException(nameof(time));

    /// <summary>The toasts waiting or showing.</summary>
    public int Count
    {
        get { lock (_lock) return _toasts.Count; }
    }

    /// <summary>
    /// Add a toast. Up to three show at a time; the rest wait. Each one shows for <paramref name="duration"/>
    /// (default 4 seconds) from when it first appears.
    /// </summary>
    public void Push(string text, ToastLevel level = ToastLevel.Info, TimeSpan? duration = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_lock) _toasts.Add(new Toast(text, level, duration ?? DefaultDuration));
    }

    /// <summary>Remove all toasts.</summary>
    public void Clear()
    {
        lock (_lock) _toasts.Clear();
    }

    /// <summary>Drop expired toasts and return the ones to show now, starting the clock on newly shown ones.</summary>
    internal List<Toast> Visible(TimeProvider fallback)
    {
        var now = (_time ?? fallback).GetUtcNow();
        lock (_lock)
        {
            _toasts.RemoveAll(t => t.ShownAt is { } shown && now - shown >= t.Duration);
            var visible = _toasts.Take(MaxVisible).ToList();
            foreach (var toast in visible) toast.ShownAt ??= now;
            return visible;
        }
    }

    internal sealed class Toast(string text, ToastLevel level, TimeSpan duration)
    {
        public string Text { get; } = text;
        public ToastLevel Level { get; } = level;
        public TimeSpan Duration { get; } = duration;
        public DateTimeOffset? ShownAt { get; set; }
    }
}

/// <summary>
/// Draws a <see cref="ToastQueue"/> in the top-right corner of the area, usually the whole screen.
/// </summary>
public sealed class ToastsBuilder
{
    private const int MaxWidth = 60;
    private const int MaxLines = 3;

    private ToastQueue? _queue;

    /// <summary>The toasts to draw.</summary>
    public ToastsBuilder Queue(ToastQueue queue) { _queue = queue; return this; }

    /// <summary>Draw the visible toasts over whatever is in <paramref name="area"/>.</summary>
    public void Draw(CellBuffer buf, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buf);
        if (_queue is null) return;

        var app = App.Current;
        var visible = _queue.Visible(app?.Time ?? TimeProvider.System);
        if (visible.Count == 0) return;
        app?.RequestTicks(ToastQueue.ExpiryCheck);

        var theme = Theme.Current;
        var y = area.Y;
        foreach (var toast in visible)
        {
            // 1.x spec §25.7: wrapped within the box, up to 3 lines, the box as wide as the longest
            var inside = Math.Min(MaxWidth, area.Width - 1) - 4;
            if (inside < 1) break;
            var lines = Lines(toast.Text, inside);
            var width = Math.Min(lines.Max(l => UnicodeWidth.Of(l)), inside) + 4;
            var height = lines.Count + 2;
            if (y + height > area.Bottom) break;

            var box = new Rect(area.Right - width - 1, y, width, height);
            WidgetText.Clear(buf, box);
            var border = toast.Level switch
            {
                ToastLevel.Success => theme.Success,
                ToastLevel.Warning => theme.Warning,
                ToastLevel.Error => theme.Error,
                _ => theme.Info,
            };
            var inner = new BlockBuilder().BorderType(BoxStyle.Rounded).BorderStyle(border).Padding(new Margin(1, 0)).Draw(buf, box);
            for (var i = 0; i < lines.Count && i < inner.Height; i++)
                WidgetText.DrawString(buf, inner.X, inner.Y + i, lines[i], inner.Width, theme.Text);
            y += height;
        }
    }

    // The text's rows at the width: line breaks kept, words wrapped, and past the last row the rest run into it, so
    // drawing it cuts it with "…"
    private static List<string> Lines(string text, int width)
    {
        var rows = TextWrap.Wrap(Text.From(text), width, WrapMode.Word)
            .Select(row => Sanitizer.SingleLine(string.Concat(row.Spans.Select(s => s.Content))).TrimEnd())
            .ToList();
        if (rows.Count == 0) rows.Add("");
        if (rows.Count <= MaxLines) return rows;
        var last = string.Join(' ', rows.Skip(MaxLines - 1));
        rows.RemoveRange(MaxLines - 1, rows.Count - MaxLines + 1);
        rows.Add(last);
        return rows;
    }
}
