namespace MiniTui;

/// <summary>
/// Markdown blocks laid out at a width (1.x spec §27.3): the rows drawn, and which block each row belongs to. Blank
/// rows between blocks belong to none.
/// </summary>
internal sealed class MarkdownLayout
{
    private const string ColumnGap = " │ ";

    private MarkdownLayout(List<Line> rows, List<int> rowBlock, List<(int First, int Last)> blockRows)
    {
        Rows = rows;
        RowBlock = rowBlock;
        BlockRows = blockRows;
    }

    public IReadOnlyList<Line> Rows { get; }

    /// <summary>Each row's block, or -1 for a blank row between blocks.</summary>
    public IReadOnlyList<int> RowBlock { get; }

    /// <summary>Each block's first and last row.</summary>
    public IReadOnlyList<(int First, int Last)> BlockRows { get; }

    /// <summary>The rows of blocks <paramref name="first"/> to <paramref name="last"/> as shown, one line each.</summary>
    public string Shown(int first, int last) =>
        string.Join('\n', Enumerable.Range(BlockRows[first].First, BlockRows[last].Last - BlockRows[first].First + 1)
            .Select(r => string.Concat(Rows[r].Spans.Select(s => s.Content)).TrimEnd()));

    public static MarkdownLayout Layout(IReadOnlyList<MarkdownBlock> blocks, int width, MarkdownStyles styles)
    {
        width = Math.Max(width, 8);
        var rows = new List<Line>();
        var rowBlock = new List<int>();
        var blockRows = new List<(int, int)>();
        for (var b = 0; b < blocks.Count; b++)
        {
            var block = blocks[b];
            if (b > 0 && !(block.InList && blocks[b - 1].InList && block.FirstLine == blocks[b - 1].LastLine + 1))
            {
                rows.Add(new Line(Bars(Math.Min(block.Quote, blocks[b - 1].Quote), styles)));
                rowBlock.Add(-1);
            }
            var first = rows.Count;
            foreach (var row in Block(block, width, styles))
            {
                rows.Add(row);
                rowBlock.Add(b);
            }
            blockRows.Add((first, rows.Count - 1));
        }
        return new MarkdownLayout(rows, rowBlock, blockRows);
    }

    private static List<Line> Block(MarkdownBlock block, int width, MarkdownStyles styles)
    {
        var lead = Bars(block.Quote, styles);
        var leadWidth = 2 * block.Quote + block.Indent;
        var room = Math.Max(4, width - leadWidth);
        var body = block.Kind switch
        {
            MarkdownBlockKind.Heading => Heading(block, room, styles),
            MarkdownBlockKind.Paragraph => Words(MarkdownInline.Parse(block.Text, default, styles), room),
            MarkdownBlockKind.Code => [.. block.Lines.SelectMany(l => Wrap(new Line(l, styles.Code), room - 2, WrapMode.Char))
                .Select(r => new Line([new Span("  ", styles.Code), .. r.Spans]))],
            MarkdownBlockKind.Rule => [new Line(new string('─', room), styles.Muted)],
            MarkdownBlockKind.Table => Table(block, room, styles),
            _ => block.Lines.SelectMany(l => Wrap(new Line(l), room, WrapMode.Word)).ToList(),
        };
        if (body.Count == 0) body.Add(new Line([]));

        var rows = new List<Line>();
        for (var r = 0; r < body.Count; r++)
        {
            var spans = new List<Span>(lead);
            if (block.Indent > 0)
            {
                var marker = r == 0 && block.Marker is { } m ? m + " " : "";
                spans.Add(new Span(new string(' ', block.Indent - UnicodeWidth.Of(marker)) + marker, default));
            }
            spans.AddRange(body[r].Spans);
            rows.Add(new Line(spans));
        }
        return rows;
    }

    private static List<Line> Heading(MarkdownBlock block, int room, MarkdownStyles styles)
    {
        var style = block.Level <= 2 ? styles.Heading : Style.Default.Bold();
        var rows = Words(MarkdownInline.Parse(block.Text, style, styles), room);
        if (block.Level <= 2)
        {
            var wide = Math.Max(1, rows.Max(r => r.Width));
            rows.Add(new Line(new string(block.Level == 1 ? '═' : '─', wide), styles.Heading));
        }
        return rows;
    }

    // Inline spans wrapped at words, a "\n" piece breaking the line
    private static List<Line> Words(List<Span> spans, int width)
    {
        var lines = new List<Line>();
        var line = new List<Span>();
        foreach (var span in spans)
        {
            if (span.Content == "\n")
            {
                lines.Add(new Line(line));
                line = [];
            }
            else line.Add(span);
        }
        lines.Add(new Line(line));
        return Wrap(new Text(lines), width, WrapMode.Word);
    }

    private static List<Line> Wrap(Text text, int width, WrapMode wrap) => TextWrap.Wrap(text, Math.Max(1, width), wrap);

    // Columns as wide as their widest cell, narrowed to fit, cells wrapped at words; when even 3 columns each
    // don't fit, the lines as written
    private static List<Line> Table(MarkdownBlock block, int room, MarkdownStyles styles)
    {
        var columns = block.Alignments.Count;
        var available = room - ColumnGap.Length * (columns - 1);
        if (available < 3 * columns) return block.Lines.SelectMany(l => Wrap(new Line(l), room, WrapMode.Word)).ToList();

        var cells = block.Cells.Select((row, r) => row.Select(c => MarkdownInline.Parse(c, r == 0 ? styles.TableHeader : default, styles)).ToList()).ToList();
        var natural = Enumerable.Range(0, columns)
            .Select(c => Math.Max(1, cells.Max(row => row[c].Sum(s => UnicodeWidth.Of(s.Content))))).ToArray();
        var widths = Fit(natural, available);

        var rows = new List<Line>();
        for (var r = 0; r < cells.Count; r++)
        {
            var wrapped = Enumerable.Range(0, columns).Select(c => Words(cells[r][c], widths[c])).ToList();
            var height = wrapped.Max(w => w.Count);
            for (var y = 0; y < height; y++)
            {
                var spans = new List<Span>();
                for (var c = 0; c < columns; c++)
                {
                    if (c > 0) spans.Add(new Span(ColumnGap, styles.Muted));
                    var cell = y < wrapped[c].Count ? wrapped[c][y] : new Line([]);
                    var space = widths[c] - cell.Width;
                    var before = block.Alignments[c] switch
                    {
                        Alignment.Right => space,
                        Alignment.Center => space / 2,
                        _ => 0,
                    };
                    if (before > 0) spans.Add(new Span(new string(' ', before), default));
                    spans.AddRange(cell.Spans);
                    if (c < columns - 1 && space - before > 0) spans.Add(new Span(new string(' ', space - before), default));
                }
                rows.Add(new Line(spans));
            }
            if (r == 0)
                rows.Add(new Line(string.Join("─┼─", widths.Select(w => new string('─', w))), styles.Muted));
        }
        return rows;
    }

    // Each column its natural width if they all fit; otherwise the narrow ones keep theirs and the rest share what's
    // left
    internal static int[] Fit(int[] natural, int available)
    {
        if (natural.Sum() <= available) return natural;
        var widths = new int[natural.Length];
        var left = available;
        var order = Enumerable.Range(0, natural.Length).OrderBy(c => natural[c]).ToList();
        for (var k = 0; k < order.Count; k++)
        {
            var share = left / (order.Count - k);
            var c = order[k];
            if (natural[c] <= share)
            {
                widths[c] = natural[c];
                left -= natural[c];
                continue;
            }
            // The rest are all wider than their share: split what's left, the remainder to the first ones
            var rest = order.Skip(k).OrderBy(x => x).ToList();
            for (var j = 0; j < rest.Count; j++) widths[rest[j]] = left / rest.Count + (j < left % rest.Count ? 1 : 0);
            break;
        }
        return widths;
    }

    private static List<Span> Bars(int quote, MarkdownStyles styles) =>
        quote == 0 ? [] : [new Span(string.Concat(Enumerable.Repeat("│ ", quote)), styles.Muted)];
}
