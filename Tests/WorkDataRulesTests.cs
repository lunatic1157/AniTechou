using AniTechou.Utilities;
using Xunit;

namespace AniTechou.Tests;

public class WorkDataRulesTests
{
    [Theory]
    [InlineData("动画", "Anime")]
    [InlineData("Anime", "Anime")]
    [InlineData("漫画", "Manga")]
    [InlineData("LightNovel", "LightNovel")]
    [InlineData("游戏", "Game")]
    public void NormalizeTypeToEnglish_ReturnsExpectedValue(string input, string expected)
    {
        Assert.Equal(expected, WorkDataRules.NormalizeTypeToEnglish(input));
    }

    [Theory]
    [InlineData("无", "")]
    [InlineData("原创动画", "原创")]
    [InlineData("漫画改编", "漫改")]
    [InlineData("轻小说改", "小说改")]
    [InlineData("游戏改编", "游戏改")]
    [InlineData("其他改编", "其他")]
    public void NormalizeSourceType_ReturnsExpectedValue(string input, string expected)
    {
        Assert.Equal(expected, WorkDataRules.NormalizeSourceType(input));
    }

    [Fact]
    public void IsSameWork_ReturnsFalse_WhenTypeIsDifferent()
    {
        var result = WorkDataRules.IsSameWork("死亡笔记", "", "Anime", "死亡笔记", "", "Manga");

        Assert.False(result);
    }

    [Fact]
    public void IsSameWork_ReturnsTrue_WhenOriginalTitleMatchesAndTypeMatches()
    {
        var result = WorkDataRules.IsSameWork("Odd Taxi", "奇巧计程车", "Anime", "奇巧计程车", "", "动画");

        Assert.True(result);
    }

    // === 中日文名混用去重场景 ===

    [Fact]
    public void IsSameWork_ReturnsTrue_WhenExactTitleMatchesCaseInsensitive()
    {
        // 完全相同的标题但大小写不同
        var result = WorkDataRules.IsSameWork("Steins;Gate", "", "Anime", "steins;gate", "", "Anime");
        Assert.True(result);
    }

    [Fact]
    public void IsSameWork_ReturnsTrue_WhenBothHaveJapaneseTitle()
    {
        // 双方都有同样的日文原名
        var result = WorkDataRules.IsSameWork("钢之炼金术师", "鋼の錬金術師", "Anime", "鋼の錬金術師", "", "Anime");
        Assert.True(result);
    }

    [Fact]
    public void IsSameWork_ReturnsFalse_WhenTitlesCompletelyDifferent()
    {
        var result = WorkDataRules.IsSameWork("鬼灭之刃", "", "Anime", "咒术回战", "", "Anime");
        Assert.False(result);
    }

    [Fact]
    public void IsSameWork_ReturnsFalse_WhenBothTitlesEmpty()
    {
        var result = WorkDataRules.IsSameWork("SomeTitle", "", "Anime", "", "", "Anime");
        Assert.False(result);
    }

    // === NormalizeSourceType 边界场景 ===

    [Theory]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("未知类型", "未知类型")]
    [InlineData("轻小说改编", "小说改")]
    [InlineData("小说", "小说改")]
    [InlineData("Visual Novel 改编", "其他")]
    [InlineData("书改", "小说改")]
    [InlineData("漫画", "漫改")]
    [InlineData("原创", "原创")]
    [InlineData("游戏", "游戏改")]
    [InlineData("改编自漫画", "漫改")]
    public void NormalizeSourceType_EdgeCases(string input, string expected)
    {
        Assert.Equal(expected, WorkDataRules.NormalizeSourceType(input));
    }
}
