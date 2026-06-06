using AniTechou.Models;
using AniTechou.Services;
using Xunit;

namespace AniTechou.Tests;

public class RecommendationProfileTests
{
    [Fact]
    public void Build_ExtractsHighRatedActiveTagsPeopleAndTypePreferences()
    {
        var snapshots = new List<RecommendationProfile.WorkSnapshot>
        {
            Snapshot(1, "葬送的芙莉莲", "Anime", "done", 9.5, "治愈", "奇幻", "导演:斋藤圭一郎"),
            Snapshot(2, "CLANNAD", "Anime", "done", 9.0, "治愈", "校园", "音乐:麻枝准"),
            Snapshot(3, "Girls Band Cry", "Anime", "doing", 8.8, "音乐", "青春", "脚本:花田十辉"),
            Snapshot(4, "蓦然回首", "Manga", "wish", 0, "青春", "漫画改")
        };

        var profile = RecommendationProfile.Build(snapshots);

        Assert.Contains(profile.HighRatedWorks, item => item.Title == "葬送的芙莉莲" && item.Rating == 9.5);
        Assert.Contains(profile.ActiveWorks, item => item.Title == "Girls Band Cry" && item.Status == "doing");
        Assert.Contains(profile.CompletedWorks, item => item.Title == "CLANNAD" && item.Status == "done");
        Assert.Contains(profile.CommonTags, item => item.Value == "治愈" && item.Count == 2);
        Assert.Contains(profile.PeopleTags, item => item.Value == "导演:斋藤圭一郎");
        Assert.Contains(profile.TypePreferences, item => item.Value == "Anime" && item.Count == 3);
    }

    [Fact]
    public void FormatForPrompt_IncludesProfileSignalsAndRecommendationCategories()
    {
        var profile = RecommendationProfile.Build(new[]
        {
            Snapshot(1, "葬送的芙莉莲", "Anime", "done", 9.5, "治愈", "奇幻", "导演:斋藤圭一郎"),
            Snapshot(2, "Girls Band Cry", "Anime", "doing", 8.8, "音乐", "青春")
        });

        var prompt = profile.FormatForPrompt();

        Assert.Contains("用户画像", prompt);
        Assert.Contains("高评分作品", prompt);
        Assert.Contains("看过/在看", prompt);
        Assert.Contains("常见普通标签", prompt);
        Assert.Contains("人员标签", prompt);
        Assert.Contains("类型偏好", prompt);
        Assert.Contains("相似推荐", prompt);
        Assert.Contains("口味推荐", prompt);
        Assert.Contains("拓展推荐", prompt);
        Assert.Contains("补课推荐", prompt);
        Assert.Contains("葬送的芙莉莲", prompt);
        Assert.Contains("治愈", prompt);
    }

    private static RecommendationProfile.WorkSnapshot Snapshot(
        int id,
        string title,
        string type,
        string status,
        double rating,
        params string[] tags)
    {
        return new RecommendationProfile.WorkSnapshot
        {
            Work = new WorkService.WorkCardData { Id = id, Title = title, Type = type },
            UserWork = new UserWorkInfo { WorkId = id, Status = status, Rating = rating },
            Tags = tags.ToList()
        };
    }
}
