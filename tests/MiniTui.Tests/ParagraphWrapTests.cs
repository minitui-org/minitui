namespace MiniTui.Tests;

/// <summary><see cref="Paragraph.Wrap"/>: a paragraph's rows without drawing them (1.x spec §27.2).</summary>
public sealed class ParagraphWrapTests
{
    private static string Plain(Line line) => string.Concat(line.Spans.Select(s => s.Content));

    private static List<string> Rows(Text text, int width, WrapMode wrap) => [.. Paragraph.Wrap(text, width, wrap).Select(Plain)];

    [Fact]
    public void Words_wrap_at_spaces_and_a_word_wider_than_the_row_is_broken()
    {
        Assert.Equal(["Tomatoes need", "six hours of", "sun."], Rows("Tomatoes need six hours of sun.", 13, WrapMode.Word));
        Assert.Equal(["abcdefgh", "ijkl"], Rows("abcdefghijkl", 8, WrapMode.Word));
        Assert.Equal(["abcde", "fghij"], Rows("abcdefghij", 5, WrapMode.Char));
    }

    [Fact]
    public void Line_breaks_are_kept_and_a_first_rows_indent_too()
    {
        Assert.Equal(["  one two", "three", "", "four"], Rows("  one two three\n\nfour", 9, WrapMode.Word));
    }

    [Fact]
    public void Wide_characters_take_two_columns()
    {
        Assert.Equal(["日本", "語"], Rows("日本語", 5, WrapMode.Char));
    }

    [Fact]
    public void Styles_carry_across_rows_and_alignment_stays_on_each_row()
    {
        var red = new Style(Color.Red);
        var blue = new Style(Color.Blue);
        var line = new Line([new Span("red words ", red), new Span("blue", blue)], Alignment.Right);
        var rows = Paragraph.Wrap(line, 6, WrapMode.Word);
        Assert.Equal(3, rows.Count);
        Assert.Equal([("red", red)], rows[0].Spans.Select(s => (s.Content, s.Style)));
        Assert.Equal([("words", red)], rows[1].Spans.Select(s => (s.Content, s.Style)));
        Assert.Equal([("blue", blue)], rows[2].Spans.Select(s => (s.Content, s.Style)));
        Assert.All(rows, r => Assert.Equal(Alignment.Right, r.Alignment));
    }

    [Fact]
    public void Without_wrapping_or_room_the_lines_are_returned_as_they_are()
    {
        Text text = "a long line\nb";
        Assert.Equal(text.Lines, Paragraph.Wrap(text, 4, WrapMode.None));
        Assert.Equal(text.Lines, Paragraph.Wrap(text, 0, WrapMode.Word));
        Assert.Throws<ArgumentNullException>(() => Paragraph.Wrap(null!, 4, WrapMode.Word));
    }

    [Theory]
    [InlineData(WrapMode.Word)]
    [InlineData(WrapMode.Char)]
    public void The_rows_are_the_ones_a_paragraph_draws_and_as_many_as_MeasureLines_says(WrapMode wrap)
    {
        var random = new Random(91);
        string[] words = ["a", "tomato", "日本語", "is", "  ", "supercalifragilistic", "🍅", "x\ny", "é", "sun."];
        for (var n = 0; n < 200; n++)
        {
            Text text = string.Join(' ', Enumerable.Range(0, random.Next(1, 20)).Select(_ => words[random.Next(words.Length)]));
            var width = random.Next(1, 30);
            var rows = Paragraph.Wrap(text, width, wrap);
            Assert.Equal(Paragraph.MeasureLines(text, width, wrap), rows.Count);

            var buf = new CellBuffer(width, rows.Count);
            Widget.Paragraph().Text(text).Wrap(wrap).Draw(buf, buf.Area);
            for (var y = 0; y < rows.Count; y++)
            {
                var drawn = string.Concat(Enumerable.Range(0, width).Select(x => buf.Get(x, y).Symbol)).TrimEnd();
                Assert.Equal(drawn, Plain(rows[y]).TrimEnd());
            }
        }
    }
}
