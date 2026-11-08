using System.Text.RegularExpressions;

namespace MiniTui;

/// <summary>What a <see cref="MarkdownBlock"/> is (1.x spec §27.3).</summary>
public enum MarkdownBlockKind
{
    /// <summary>A heading, <c>#</c> to <c>######</c> or underlined with <c>===</c> or <c>---</c>.</summary>
    Heading,
    /// <summary>A paragraph, or the text of a list item.</summary>
    Paragraph,
    /// <summary>Fenced or indented code, shown as written.</summary>
    Code,
    /// <summary>A rule: <c>---</c>, <c>***</c> or <c>___</c>.</summary>
    Rule,
    /// <summary>A table with a header and a delimiter row, drawn in aligned columns.</summary>
    Table,
    /// <summary>Lines shown as written, wrapped at words: lines of plain text, or pipes that aren't a table.</summary>
    Verbatim,
}

/// <summary>
/// One block of Markdown (1.x spec §27.3): what a <see cref="MarkdownState"/>'s block cursor moves by. Its lines are
/// where it came from in the source, counting from 0, so it can be quoted as written.
/// </summary>
/// <param name="Kind">What it is.</param>
/// <param name="FirstLine">Its first line in the source.</param>
/// <param name="LastLine">Its last line in the source.</param>
public sealed record MarkdownBlock(MarkdownBlockKind Kind, int FirstLine, int LastLine)
{
    /// <summary>1 to 6 for a heading, otherwise 0.</summary>
    public int Level { get; init; }

    /// <summary>A heading's or paragraph's inline Markdown, with <c>\n</c> where a line breaks.</summary>
    internal string Text { get; init; } = "";

    /// <summary>A code or verbatim block's lines.</summary>
    internal IReadOnlyList<string> Lines { get; init; } = [];

    /// <summary>A table's rows of cells, the header first; each cell is inline Markdown.</summary>
    internal IReadOnlyList<IReadOnlyList<string>> Cells { get; init; } = [];

    /// <summary>A table's column alignments.</summary>
    internal IReadOnlyList<Alignment> Alignments { get; init; } = [];

    /// <summary>How many quotes it's inside.</summary>
    internal int Quote { get; init; }

    /// <summary>Columns before its text, after any quote bars. A <see cref="Marker"/> and a space fill the last of them.</summary>
    internal int Indent { get; init; }

    /// <summary>A list item's <c>•</c>, number or check box, shown on its first row.</summary>
    internal string? Marker { get; init; }

    /// <summary>In a list item: no blank row between it and the block before when the source has none.</summary>
    internal bool InList { get; init; }
}

/// <summary>
/// The CommonMark subset <see cref="MarkdownState"/> formats (1.x spec §27.3), as blocks. Anything it doesn't know is
/// a paragraph, shown as written, so nothing in the source is lost.
/// </summary>
internal static partial class MarkdownParser
{
    private readonly record struct Source(string Text, int Line);

    private sealed record Context(int Quote, int Indent, bool InList, string? Marker);

    /// <summary>The blocks of Markdown whose tabs are already spaces.</summary>
    public static List<MarkdownBlock> Parse(IReadOnlyList<string> lines)
    {
        var source = lines.Select((text, line) => new Source(text, line)).ToList();
        var blocks = new List<MarkdownBlock>();
        Parse(source[FrontMatter(lines)..], new Context(0, 0, false, null), blocks);
        return blocks;
    }

    /// <summary>The blocks of plain text: each run of lines between blank ones, as written.</summary>
    public static List<MarkdownBlock> PlainText(IReadOnlyList<string> lines)
    {
        var blocks = new List<MarkdownBlock>();
        for (var i = 0; i < lines.Count;)
        {
            if (IsBlank(lines[i])) { i++; continue; }
            var first = i;
            while (i < lines.Count && !IsBlank(lines[i])) i++;
            blocks.Add(new MarkdownBlock(MarkdownBlockKind.Verbatim, first, i - 1) { Lines = lines.Skip(first).Take(i - first).Select(l => l.TrimEnd()).ToList() });
        }
        return blocks;
    }

    // `---` on the first line, up to the next `---` or `...`: front matter, which isn't shown
    private static int FrontMatter(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0 || lines[0].TrimEnd() != "---") return 0;
        for (var i = 1; i < lines.Count; i++)
            if (lines[i].TrimEnd() is "---" or "...") return i + 1;
        return 0;
    }

    private static void Parse(List<Source> lines, Context context, List<MarkdownBlock> blocks)
    {
        var marker = context.Marker; // the first block of a list item carries its marker
        void Add(MarkdownBlock block)
        {
            blocks.Add(block with
            {
                Quote = context.Quote,
                Indent = context.Indent,
                InList = context.InList,
                Marker = marker,
            });
            marker = null;
        }

        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i].Text;
            if (IsBlank(line)) { i++; continue; }
            var indent = Indentation(line);

            // Indented code
            if (indent >= 4)
            {
                var first = i;
                while (i < lines.Count && (IsBlank(lines[i].Text) || Indentation(lines[i].Text) >= 4)) i++;
                var last = i - 1;
                while (IsBlank(lines[last].Text)) last--;
                Add(new MarkdownBlock(MarkdownBlockKind.Code, lines[first].Line, lines[last].Line)
                {
                    Lines = lines[first..(last + 1)].Select(l => l.Text.Length >= 4 ? l.Text[4..].TrimEnd() : "").ToList(),
                });
                i = last + 1;
                continue;
            }

            var text = line[indent..];

            // Fenced code
            if (Fence().Match(text) is { Success: true } fence)
            {
                var run = fence.Groups[1].Value;
                var first = i++;
                var code = new List<string>();
                while (i < lines.Count && !IsClosingFence(lines[i].Text, run)) code.Add(Dedent(lines[i++].Text, indent).TrimEnd());
                var last = i < lines.Count ? i++ : i - 1;
                Add(new MarkdownBlock(MarkdownBlockKind.Code, lines[first].Line, lines[last].Line) { Lines = code });
                continue;
            }

            if (Atx().Match(text) is { Success: true } atx)
            {
                Add(new MarkdownBlock(MarkdownBlockKind.Heading, lines[i].Line, lines[i].Line) { Level = atx.Groups[1].Length, Text = atx.Groups[2].Value.Trim() });
                i++;
                continue;
            }

            if (Rule().IsMatch(text))
            {
                Add(new MarkdownBlock(MarkdownBlockKind.Rule, lines[i].Line, lines[i].Line));
                i++;
                continue;
            }

            // A quote: its lines without the `>`, as blocks of their own
            if (text.StartsWith('>'))
            {
                var inner = new List<Source>();
                while (i < lines.Count && QuoteText(lines[i].Text) is { } quoted) inner.Add(lines[i++] with { Text = quoted });
                var before = blocks.Count;
                Parse(inner, context with { Quote = context.Quote + 1, Marker = marker }, blocks);
                if (blocks.Count > before) marker = null;
                continue;
            }

            // A list item: its marker, then its lines without the indentation, as blocks of their own
            if (ListItem().Match(text) is { Success: true } item)
            {
                var content = indent + item.Groups[1].Length + ContentGap(item.Groups[2].Value);
                var shown = item.Groups[1].Value is "-" or "*" or "+" ? "•" : item.Groups[1].Value;
                var first = line.Length > content ? line[content..] : "";
                if (TaskBox().Match(first) is { Success: true } box)
                {
                    shown = box.Groups[1].Value == " " ? "☐" : "☑";
                    first = first[box.Length..];
                }
                var inner = new List<Source> { lines[i] with { Text = first } };
                i++;
                while (i < lines.Count)
                {
                    var next = lines[i].Text;
                    if (IsBlank(next))
                    {
                        inner.Add(lines[i++] with { Text = "" });
                        continue;
                    }
                    if (Indentation(next) >= content) inner.Add(lines[i++] with { Text = next[content..] });
                    else if (!IsBlank(inner[^1].Text) && next[Math.Min(Indentation(next), 3)..] is var lazy && !StartsBlock(lazy) && !ListItem().IsMatch(lazy))
                        inner.Add(lines[i++] with { Text = next.TrimStart() }); // runs on
                    else break;
                }
                if (marker is not null) Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, lines[i - inner.Count].Line, lines[i - inner.Count].Line)); // "- - a": the outer marker alone
                var before = blocks.Count;
                var itemIndent = context.Indent + UnicodeWidth.Of(shown) + 1;
                Parse(inner, new Context(context.Quote, itemIndent, true, shown), blocks);
                if (blocks.Count == before)
                    blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, inner[0].Line, inner[0].Line) { Quote = context.Quote, Indent = itemIndent, InList = true, Marker = shown });
                continue;
            }

            // A table: a header row, then a delimiter row with as many cells
            if (text.Contains('|', StringComparison.Ordinal) && i + 1 < lines.Count && Table(lines, i) is { } table)
            {
                Add(table.Block);
                i = table.End;
                continue;
            }

            if (text.StartsWith('|'))
            {
                var first = i;
                while (i < lines.Count && Indentation(lines[i].Text) < 4 && lines[i].Text.TrimStart().StartsWith('|')) i++;
                Add(new MarkdownBlock(MarkdownBlockKind.Verbatim, lines[first].Line, lines[i - 1].Line) { Lines = lines[first..i].Select(l => l.Text.Trim()).ToList() });
                continue;
            }

            // A paragraph, or a heading underlined with === or ---
            {
                var first = i++;
                var level = 0;
                while (i < lines.Count && !IsBlank(lines[i].Text))
                {
                    var next = lines[i].Text;
                    var trimmed = next[Math.Min(Indentation(next), 3)..];
                    if (Indentation(next) < 4 && SetextUnderline().Match(trimmed) is { Success: true } underline)
                    {
                        level = underline.Groups[1].Value[0] == '=' ? 1 : 2;
                        break;
                    }
                    if (Indentation(next) < 4 && StartsBlock(trimmed)) break;
                    i++;
                }
                var paragraph = Join(lines[first..i].Select(l => l.Text).ToList());
                if (level > 0) Add(new MarkdownBlock(MarkdownBlockKind.Heading, lines[first].Line, lines[i++].Line) { Level = level, Text = paragraph.Replace('\n', ' ') });
                else Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, lines[first].Line, lines[i - 1].Line) { Text = paragraph });
            }
        }
    }

    // A table at line i, if the next line is a delimiter row with as many cells as it: its rows run to a blank line, a
    // line without a pipe, or another block
    private static (MarkdownBlock Block, int End)? Table(List<Source> lines, int i)
    {
        if (Indentation(lines[i + 1].Text) >= 4 || !Delimiter().IsMatch(lines[i + 1].Text.Trim())) return null;
        var header = Cells(lines[i].Text);
        var delimiters = Cells(lines[i + 1].Text);
        if (header.Count != delimiters.Count) return null;
        var alignments = delimiters.Select(d => (d.StartsWith(':'), d.EndsWith(':')) switch
        {
            (true, true) => Alignment.Center,
            (false, true) => Alignment.Right,
            _ => Alignment.Left,
        }).ToList();
        var rows = new List<IReadOnlyList<string>> { header };
        var end = i + 2;
        while (end < lines.Count)
        {
            var next = lines[end].Text;
            if (IsBlank(next) || Indentation(next) >= 4 || !next.Contains('|', StringComparison.Ordinal) || StartsBlock(next.TrimStart())) break;
            var cells = Cells(next);
            // As many cells as the header: extra ones are dropped, missing ones empty
            rows.Add([.. Enumerable.Range(0, header.Count).Select(c => c < cells.Count ? cells[c] : "")]);
            end++;
        }
        var block = new MarkdownBlock(MarkdownBlockKind.Table, lines[i].Line, lines[end - 1].Line)
        {
            Cells = rows,
            Alignments = alignments,
            Lines = lines[i..end].Select(l => l.Text.Trim()).ToList(),
        };
        return (block, end);
    }

    // A row's cells: split at pipes that aren't escaped, without the outer ones, trimmed, with \| as |
    private static List<string> Cells(string line)
    {
        var text = line.Trim();
        if (text.StartsWith('|')) text = text[1..];
        if (text.EndsWith('|') && !text.EndsWith(@"\|", StringComparison.Ordinal)) text = text[..^1];
        var cells = new List<string>();
        var cell = new System.Text.StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '|')
            {
                cell.Append('|');
                i++;
            }
            else if (text[i] == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else cell.Append(text[i]);
        }
        cells.Add(cell.ToString().Trim());
        return cells;
    }

    // What ends a paragraph: a heading, a fence, a rule, a quote, or a list item with something in it
    private static bool StartsBlock(string text) =>
        Atx().IsMatch(text) || Fence().IsMatch(text) || Rule().IsMatch(text) || text.StartsWith('>')
        || (ListItem().Match(text) is { Success: true } item && item.Groups[2].Value.Length > 0 && text.Length > item.Length
            && (item.Groups[1].Value is "-" or "*" or "+" or "1." or "1)"));

    // Lines that follow on are joined; two trailing spaces or a trailing \ break the line
    private static string Join(List<string> lines)
    {
        var parts = new System.Text.StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].TrimStart();
            if (i == lines.Count - 1)
            {
                parts.Append(line.TrimEnd());
                break;
            }
            if (line.EndsWith("  ", StringComparison.Ordinal)) parts.Append(line.TrimEnd()).Append('\n');
            else if (line.TrimEnd().EndsWith('\\') && !line.TrimEnd().EndsWith(@"\\", StringComparison.Ordinal)) parts.Append(line.TrimEnd()[..^1]).Append('\n');
            else parts.Append(line.TrimEnd()).Append(' ');
        }
        return parts.ToString();
    }

    private static string? QuoteText(string line)
    {
        var indent = Indentation(line);
        if (indent > 3 || indent >= line.Length || line[indent] != '>') return null;
        var rest = line[(indent + 1)..];
        return rest.StartsWith(' ') ? rest[1..] : rest;
    }

    // 1 to 4 spaces after the marker are part of it; more means indented code, so just 1; none (an empty item) is 1
    private static int ContentGap(string spaces) => spaces.Length is >= 1 and <= 4 ? spaces.Length : 1;

    private static bool IsClosingFence(string line, string run)
    {
        var indent = Indentation(line);
        if (indent > 3) return false;
        var text = line[indent..].TrimEnd();
        return text.Length >= run.Length && text.All(c => c == run[0]);
    }

    private static string Dedent(string line, int columns) => line[Math.Min(columns, Indentation(line))..];

    internal static int Indentation(string line)
    {
        var n = 0;
        while (n < line.Length && line[n] == ' ') n++;
        return n;
    }

    internal static bool IsBlank(string line) => line.All(char.IsWhiteSpace);

    [GeneratedRegex(@"^(`{3,}|~{3,})(?(?<=`+)[^`]*|.*)$")]
    private static partial Regex Fence();

    [GeneratedRegex(@"^(#{1,6})(?:[ ]+(.*?))?(?:[ ]+#+)?[ ]*$")]
    private static partial Regex Atx();

    [GeneratedRegex(@"^([-*_])(?:[ ]*\1){2,}[ ]*$")]
    private static partial Regex Rule();

    [GeneratedRegex(@"^([-*+]|\d{1,9}[.)])( +|$)")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^\[([ xX])\](?: +|$)")]
    private static partial Regex TaskBox();

    [GeneratedRegex(@"^(=+|-+)[ ]*$")]
    private static partial Regex SetextUnderline();

    // | :-- | --: | :-: |, the outer pipes optional
    [GeneratedRegex(@"^\|?[ ]*:?-+:?[ ]*(\|[ ]*:?-+:?[ ]*)*\|?$")]
    private static partial Regex Delimiter();
}
