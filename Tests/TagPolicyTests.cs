using AniTechou.Utilities;
using Xunit;

namespace AniTechou.Tests;

public class TagPolicyTests
{
    [Fact]
    public void NormalizeAutomaticTags_DropsRedundantMetadataAndCompanyTags()
    {
        var context = new TagPolicy.WorkTagContext
        {
            Year = "2024",
            Season = "春",
            Type = "Anime",
            SourceType = "漫改",
            Company = "MADHOUSE"
        };

        var tags = TagPolicy.NormalizeAutomaticTags(new[]
        {
            "2024",
            "2024年",
            "2024年4月",
            "2024-04",
            "年份:2024",
            "评分:8.5",
            "MAL评分:8.1",
            "MADHOUSE",
            "制作:MADHOUSE",
            "春",
            "动画",
            "漫改",
            "想看",
            "治愈",
            "群像"
        }, context);

        Assert.Equal(new[] { "治愈", "群像" }, tags);
    }

    [Fact]
    public void NormalizeAutomaticTags_StandardizesPeopleTags()
    {
        var tags = TagPolicy.NormalizeAutomaticTags(new[]
        {
            "CV:早见沙织",
            "声优:种崎敦美",
            "导演:新海诚",
            "脚本:吉田玲子",
            "音乐:牛尾宪辅",
            "角色设计:浅野直之",
            "原作:山田钟人",
            "插图:abec"
        });

        Assert.Equal(new[]
        {
            "cv:早见沙织",
            "cv:种崎敦美",
            "导演:新海诚",
            "脚本:吉田玲子",
            "音乐:牛尾宪辅",
            "角色设计:浅野直之",
            "原作:山田钟人",
            "插画:abec"
        }, tags);
    }

    [Fact]
    public void NormalizeAutomaticTags_DoesNotSplitSlashInsidePeopleNames()
    {
        var tags = TagPolicy.NormalizeAutomaticTags(new[]
        {
            "音乐:U/S",
            "导演:佐藤一郎／田中二郎"
        });

        Assert.Equal(new[]
        {
            "音乐:U/S",
            "导演:佐藤一郎／田中二郎"
        }, tags);
    }

    [Fact]
    public void PlanCleanupForAiTouchedWork_RemovesRedundantTagsAndAddsStandardizedTags()
    {
        var context = new TagPolicy.WorkTagContext
        {
            Year = "2023",
            Season = "秋",
            Type = "Anime",
            SourceType = "漫改",
            Company = "MADHOUSE"
        };

        var cleanup = TagPolicy.PlanCleanupForAiTouchedWork(new[]
        {
            "2023",
            "评分:8.7",
            "MADHOUSE",
            "CV:早见沙织",
            "悬疑",
            "用户手动标签"
        }, context);

        Assert.Contains("2023", cleanup.TagsToRemove);
        Assert.Contains("评分:8.7", cleanup.TagsToRemove);
        Assert.Contains("MADHOUSE", cleanup.TagsToRemove);
        Assert.Contains("CV:早见沙织", cleanup.TagsToRemove);
        Assert.Contains("cv:早见沙织", cleanup.TagsToAdd);
        Assert.DoesNotContain("悬疑", cleanup.TagsToRemove);
        Assert.DoesNotContain("用户手动标签", cleanup.TagsToRemove);
    }
}
