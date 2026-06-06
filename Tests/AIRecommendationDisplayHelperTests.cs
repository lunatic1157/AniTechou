using AniTechou.Services;
using Xunit;

namespace AniTechou.Tests;

public class AIRecommendationDisplayHelperTests
{
    [Theory]
    [InlineData("similar", "相似推荐")]
    [InlineData("taste", "口味推荐")]
    [InlineData("explore", "拓展推荐")]
    [InlineData("classic", "补课推荐")]
    [InlineData("相似", "相似推荐")]
    [InlineData("口味推荐", "口味推荐")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeRecommendationCategory_MapsAliasesAndKeepsEmptySafe(string input, string expected)
    {
        Assert.Equal(expected, AIRecommendationDisplayHelper.NormalizeRecommendationCategory(input));
    }

    [Fact]
    public void BuildRecommendationDisplay_ReturnsFallbackWhenCategoryAndReasonAreEmpty()
    {
        var display = AIRecommendationDisplayHelper.BuildRecommendationDisplay(new AIWorkSearchResult
        {
            title = "未分类推荐"
        });

        Assert.True(display.HasRecommendationBlock);
        Assert.Equal("推荐理由待补充", display.Category);
        Assert.Equal("AI 未返回具体分类或理由，可先根据标题、简介和标签判断是否加入收藏。", display.Reason);
    }

    [Fact]
    public void BuildRecommendationDisplay_KeepsReasonWhenCategoryIsMissing()
    {
        var display = AIRecommendationDisplayHelper.BuildRecommendationDisplay(new AIWorkSearchResult
        {
            title = "有理由推荐",
            recommendationReason = "基于高评分作品和治愈标签推荐。"
        });

        Assert.True(display.HasRecommendationBlock);
        Assert.Equal("", display.Category);
        Assert.Equal("基于高评分作品和治愈标签推荐。", display.Reason);
    }
}
