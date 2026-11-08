namespace MiniTui.Tests;

/// <summary>Markdown as <see cref="MarkdownState"/> formats it (1.x spec §27.3): the rows shown and the blocks.</summary>
public sealed class MarkdownTests
{
    private static readonly MarkdownStyles Styles = MarkdownStyles.From(Theme.Dark);

    private static List<MarkdownBlock> Blocks(string markdown) => MarkdownParser.Parse(markdown.Split('\n'));

    private static MarkdownLayout Laid(string markdown, int width = 40) => MarkdownLayout.Layout(Blocks(markdown), width, Styles);

    private static string Shown(string markdown, int width = 40) =>
        string.Join('\n', Laid(markdown, width).Rows.Select(r => string.Concat(r.Spans.Select(s => s.Content)).TrimEnd()));

    [Fact]
    public void Headings_LoseTheirMarks_AndTheFirstTwoLevelsAreUnderlined()
    {
        Assert.Equal("Garden plan\n═══════════\n\nBeds\n────\n\nSoil", Shown("# Garden plan\n## Beds ##\n### Soil"));
        Assert.Equal("Plan\n════\n\nBeds\n────", Shown("Plan\n===\nBeds\n---"));
        Assert.Equal("#hashtag", Shown("#hashtag"));
        var rows = Laid("# Plan\n### Soil").Rows;
        Assert.Equal(Theme.Dark.Heading, rows[0].Spans[0].Style);
        Assert.Equal(Modifiers.Bold, rows[3].Spans[0].Style.Add & Modifiers.Bold);
        Assert.Equal(1, Blocks("# Plan")[0].Level);
    }

    [Fact]
    public void ParagraphLines_JoinAndWrap_AndTwoSpacesOrABackslashBreakThem()
    {
        Assert.Equal("Tomatoes need six hours\nof sun.", Shown("Tomatoes need\nsix hours of sun.", width: 24));
        Assert.Equal("one\ntwo\nthree four", Shown("one  \ntwo\\\nthree\nfour"));
    }

    [Fact]
    public void AWordWiderThanThePage_IsBroken()
    {
        Assert.Equal("abcdefgh\nijkl", Shown("abcdefghijkl", width: 8));
    }

    [Fact]
    public void EmphasisCodeAndLinks_LoseTheirMarks_AndKeepTheirStyles()
    {
        var spans = MarkdownInline.Parse("a **bold** *it* ~~gone~~ `x*y` [guide](https://x.org) <https://y.org> ![cat](c.png)", default, Styles);
        Assert.Equal("a bold it gone x*y guide https://y.org [image: cat]", string.Concat(spans.Select(s => s.Content)));
        Assert.Equal(Modifiers.Bold, spans.Single(s => s.Content == "bold").Style.Add & Modifiers.Bold);
        Assert.Equal(Modifiers.Italic, spans.Single(s => s.Content == "it").Style.Add & Modifiers.Italic);
        Assert.Equal(Modifiers.CrossedOut, spans.Single(s => s.Content == "gone").Style.Add & Modifiers.CrossedOut);
        Assert.Equal(Theme.Dark.Code, spans.Single(s => s.Content == "x*y").Style);
        Assert.Equal(Theme.Dark.Link, spans.Single(s => s.Content == "guide").Style);
        Assert.Equal(Theme.Dark.Link, spans.Single(s => s.Content == "https://y.org").Style);
        Assert.Equal(Theme.Dark.TextMuted, spans.Single(s => s.Content == "[image: cat]").Style);
    }

    [Theory]
    [InlineData("snake_case_name", "snake_case_name")]
    [InlineData("2 * 3 * 4", "2 * 3 * 4")]
    [InlineData("**unclosed", "**unclosed")]
    [InlineData(@"\*not\* emphasis", "*not* emphasis")]
    [InlineData("[not a link]", "[not a link]")]
    [InlineData("`unclosed code", "`unclosed code")]
    [InlineData("***both***", "both")]
    [InlineData("*a **b** c*", "a b c")]
    public void MarksWithNothingToCloseThem_AreText(string markdown, string shown) =>
        Assert.Equal(shown, string.Concat(MarkdownInline.Parse(markdown, default, Styles).Select(s => s.Content)));

    [Fact]
    public void Lists_GetBulletsNumbersAndBoxes_AndWrappedLinesGoUnderTheText()
    {
        Assert.Equal(
            "• milk\n• eggs, the large\n  ones\n  • nested\n☐ open\n☑ done\n\n1. First\n2. Second",
            Shown("- milk\n* eggs, the large ones\n  - nested\n+ [ ] open\n- [x] done\n\n1. First\n2. Second", width: 17));
    }

    [Fact]
    public void AListItem_RunsOnOverALineThatIsNotIndented()
    {
        Assert.Equal("• one two\n• three", Shown("- one\ntwo\n- three"));
    }

    [Fact]
    public void Quotes_GetABarPerLevel_AndBlankRowsBetweenTheirBlocksKeepIt()
    {
        Assert.Equal("│ A quote that\n│ wraps.\n│\n│ │ nested", Shown("> A quote that wraps.\n>\n> > nested", width: 14));
    }

    [Fact]
    public void Code_IsShownAsWritten_IndentedAndBrokenAtTheEdge()
    {
        Assert.Equal("  if (x)\n    y();\n\n  four spaces", Shown("```cs\nif (x)\n  y();\n```\n\n    four spaces"));
        Assert.Equal("  abcdef\n  gh", Shown("```\nabcdefgh\n```", width: 8));
        Assert.Equal("  # not a heading", Shown("~~~\n# not a heading"));
        Assert.Equal("  a\n\n  b", Shown("```\na\n\nb\n```"));
        Assert.All(Laid("```\ncode\n```").Rows[0].Spans, s => Assert.Equal(Theme.Dark.Code, s.Style));
    }

    [Fact]
    public void Rules_AndFrontMatter()
    {
        Assert.Equal("a\n\n──────────\n\nb", Shown("a\n\n---\n\nb", width: 10));
        Assert.Equal("Text", Shown("---\ntitle: x\n---\nText"));
    }

    // ---- Tables ----

    [Fact]
    public void ATable_IsDrawnInColumns_WithARuleUnderItsHeader()
    {
        Assert.Equal(
            "Name  │ Size\n──────┼─────\na.md  │ 3\nb.txt │ 1024",
            Shown("| Name | Size |\n|------|------|\n| a.md | 3 |\n| b.txt | 1024 |"));
        Assert.Equal(MarkdownBlockKind.Table, Blocks("| a |\n|---|\n| 1 |")[0].Kind);
    }

    [Fact]
    public void ATablesColumns_AreAlignedAsItsDelimiterRowSays()
    {
        Assert.Equal(
            "Left │ Mid │ Right\n─────┼─────┼──────\na    │  b  │     c",
            Shown("| Left | Mid | Right |\n| :--- | :---: | ---: |\n| a | b | c |"));
    }

    [Fact]
    public void ATableWithoutOuterPipes_InlineMarks_AndEscapedPipes()
    {
        Assert.Equal("Key    │ Does\n───────┼────────────\nctrl+c │ copy | quit", Shown("Key | Does\n--- | ---\n`ctrl+c` | copy \\| quit"));
        var rows = Laid("Key | Does\n--- | ---\n`ctrl+c` | **copy**").Rows;
        Assert.Equal(Theme.Dark.TableHeader, rows[0].Spans[0].Style);
        Assert.Contains(rows[2].Spans, s => s.Content == "ctrl+c" && s.Style == Theme.Dark.Code);
    }

    [Fact]
    public void ATableRow_WithMoreOrFewerCells_HasTheHeadersCount()
    {
        Assert.Equal("a │ b\n──┼──\n1 │\n3 │ 4", Shown("| a | b |\n|---|---|\n| 1 |\n| 3 | 4 | 5 |"));
    }

    [Fact]
    public void ATableTooWide_WrapsItsWidestCells_AndOneTooNarrowForColumnsIsShownAsWritten()
    {
        Assert.Equal(
            "id │ note\n───┼────────────\n1  │ a long note\n   │ that wraps",
            Shown("| id | note |\n|---|---|\n| 1 | a long note that wraps |", width: 16));
        Assert.Equal("| a | b | c |\n|---|---|---|", Shown("| a | b | c |\n|---|---|---|", width: 13));
    }

    [Fact]
    public void ColumnsThatDoNotFit_KeepTheNarrowOnes_AndShareTheRest()
    {
        Assert.Equal([2, 9, 9], MarkdownLayout.Fit([2, 30, 40], 20));
        Assert.Equal([5, 5], MarkdownLayout.Fit([5, 5], 20));
        Assert.Equal([4, 3, 3], MarkdownLayout.Fit([10, 10, 10], 10));
    }

    [Fact]
    public void PipesWithoutADelimiterRow_AreShownAsWritten_AndATextLineAndRuleAreAHeading()
    {
        Assert.Equal("| a | b |\n| 1 | 2 |", Shown("| a | b |\n| 1 | 2 |"));
        Assert.Equal("a | b\n─────", Shown("a | b\n---"));
    }

    // ---- Blocks ----

    [Fact]
    public void EachBlock_KnowsItsLinesInTheSource()
    {
        var blocks = Blocks("# Title\n\nOne\ntwo\n\n- a\n  - b\n\n```\ncode\n```\n\n| x |\n|---|\n| 1 |");
        Assert.Equal(
            [(MarkdownBlockKind.Heading, 0, 0), (MarkdownBlockKind.Paragraph, 2, 3), (MarkdownBlockKind.Paragraph, 5, 5),
             (MarkdownBlockKind.Paragraph, 6, 6), (MarkdownBlockKind.Code, 8, 10), (MarkdownBlockKind.Table, 12, 14)],
            blocks.Select(b => (b.Kind, b.FirstLine, b.LastLine)));
    }

    [Fact]
    public void EachRow_KnowsItsBlock_AndBlankRowsBelongToNone()
    {
        var page = Laid("# Title\n\nOne\n\n- a\n- b");
        Assert.Equal([0, 0, -1, 1, -1, 2, 3], page.RowBlock);
        Assert.Equal((5, 5), page.BlockRows[2]);
        Assert.Equal("• a\n• b", page.Shown(2, 3));
    }

    [Fact]
    public void PlainText_IsItsParagraphsAsWritten()
    {
        var page = MarkdownLayout.Layout(MarkdownParser.PlainText(["  indented # not markdown", "next line", "", "", "second paragraph"]), 40, Styles);
        Assert.Equal(["  indented # not markdown", "next line", "", "second paragraph"],
            page.Rows.Select(r => string.Concat(r.Spans.Select(s => s.Content))));
    }

    [Fact]
    public void NothingInTheSource_IsLost()
    {
        var shown = Shown("<div>html</div>\n\n[ref]: https://x.org\n\n-\n\n1)\n\n>\n\n####### seven");
        foreach (var word in new[] { "<div>html</div>", "[ref]:", "#######", "seven" }) Assert.Contains(word, shown, StringComparison.Ordinal);
        Assert.Contains("•", shown, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryBuiltInTheme_HasHeadingCodeAndLinkStyles()
    {
        foreach (var theme in new[] { Theme.Default, Theme.Dark, Theme.Light })
        {
            Assert.NotNull(theme.Heading.Fg);
            Assert.NotNull(theme.Code.Fg);
            Assert.NotNull(theme.Link.Fg);
            Assert.NotEqual(theme.Code, theme.Link);
        }
        Assert.Equal(Modifiers.Underlined, Theme.Monochrome.Link.Add & Modifiers.Underlined);
        Assert.NotEqual(Style.Default, Theme.Monochrome.Heading);
    }
}
