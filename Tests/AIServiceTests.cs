using AniTechou.Services;
using Xunit;

namespace AniTechou.Tests;

public class AIServiceTests
{
    // === GetDefaultSystemPrompt ===

    [Fact]
    public void GetDefaultSystemPrompt_ContainsRequiredPlaceholders()
    {
        var prompt = AIService.GetDefaultSystemPrompt();

        Assert.Contains("{SEARCH_CONTEXT}", prompt);
        Assert.Contains("{USER_COLLECTION_CONTEXT}", prompt);
    }

    [Fact]
    public void GetDefaultSystemPrompt_ContainsCurrentDate()
    {
        var prompt = AIService.GetDefaultSystemPrompt();

        Assert.Contains(System.DateTime.Now.ToString("yyyy-MM-dd"), prompt);
    }

    [Fact]
    public void GetDefaultSystemPrompt_IsNotEmpty()
    {
        var prompt = AIService.GetDefaultSystemPrompt();

        Assert.NotNull(prompt);
        Assert.NotEmpty(prompt);
    }

    // === NeedsExternalSearch ===

    [Theory]
    [InlineData("推荐一些好看的动漫", true)]
    [InlineData("帮我找一个番剧", true)]
    [InlineData("有什么好看的动画", true)]
    [InlineData("搜索咒术回战", true)]
    [InlineData("有没有类似鬼灭之刃的作品", true)]
    [InlineData("《命运石之门》好看吗", true)]
    [InlineData("find me some anime", true)]
    [InlineData("recommend manga", true)]
    [InlineData("你好", false)]
    [InlineData("今天天气怎么样", false)]
    [InlineData("帮我写一段代码", true)]
    [InlineData("今天会下雨吗", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void NeedsExternalSearch_KeywordDetection(string message, bool expected)
    {
        // 使用反射调用 private static 方法
        var method = typeof(AIService).GetMethod("NeedsExternalSearch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method.Invoke(null, new object[] { message });

        Assert.Equal(expected, result);
    }

    [Fact]
    public void NeedsExternalSearch_BookTitleBracketsTriggersSearch()
    {
        var method = typeof(AIService).GetMethod("NeedsExternalSearch",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // 书名号触发
        Assert.True((bool)method.Invoke(null, new object[] { "我想了解关于《进击的巨人》的更多信息" }));
        // 纯书名号无关键词，仍触发
        Assert.True((bool)method.Invoke(null, new object[] { "《三体》《流浪地球》" }));
    }

    // === BuildCollectionContext ===

    [Fact]
    public void BuildCollectionContext_EmptyList_ReturnsEmptyMessage()
    {
        var result = AIService.BuildCollectionContext(new List<WorkService.WorkCardData>(), null!);
        Assert.Contains("空", result);
    }

    [Fact]
    public void BuildCollectionContext_NullList_ReturnsEmptyMessage()
    {
        var result = AIService.BuildCollectionContext(null!, null!);
        Assert.Contains("空", result);
    }

    // === GetDefaultSystemPrompt Localization ===

    [Fact]
    public void GetDefaultSystemPrompt_ContainsLocalizationInstructions()
    {
        var prompt = AIService.GetDefaultSystemPrompt();
        Assert.Contains("USER_COLLECTION_CONTEXT", prompt);
    }
}
