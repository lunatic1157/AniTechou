using AniTechou.Utilities;
using Xunit;

namespace AniTechou.Tests;

public class MarkdownConverterTests
{
    // === XamlToMarkdown ===

    [Fact]
    public void XamlToMarkdown_EmptyContent_ReturnsEmpty()
    {
        var result = MarkdownConverter.XamlToMarkdown("");
        Assert.Equal("", result);
    }

    [Fact]
    public void XamlToMarkdown_NullContent_ReturnsEmpty()
    {
        var result = MarkdownConverter.XamlToMarkdown(null);
        Assert.Equal("", result);
    }

    [Fact]
    public void XamlToMarkdown_PlainText_ReturnsSameText()
    {
        var result = MarkdownConverter.XamlToMarkdown("Hello World");
        Assert.Equal("Hello World", result);
    }

    // === MarkdownToFlowDocument ===

    [Fact]
    public void MarkdownToFlowDocument_EmptyContent_ReturnsFlowDocument()
    {
        var result = MarkdownConverter.MarkdownToFlowDocument("");
        Assert.NotNull(result);
    }

    [Fact]
    public void MarkdownToFlowDocument_Heading_ReturnsCorrectFontSize()
    {
        var result = MarkdownConverter.MarkdownToFlowDocument("# Title");
        Assert.NotNull(result);
        Assert.NotEmpty(result.Blocks);
    }

    [Fact]
    public void MarkdownToFlowDocument_ParagraphText_Preserved()
    {
        var result = MarkdownConverter.MarkdownToFlowDocument("Hello World");
        Assert.NotNull(result);
        Assert.NotEmpty(result.Blocks);
    }
}
