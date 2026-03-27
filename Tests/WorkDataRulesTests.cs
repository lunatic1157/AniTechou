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
}
