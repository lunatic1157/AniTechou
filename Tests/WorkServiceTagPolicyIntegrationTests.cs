using System;
using System.IO;
using AniTechou.Services;
using AniTechou.Utilities;
using Xunit;

namespace AniTechou.Tests;

public class WorkServiceTagPolicyIntegrationTests
{
    [Fact]
    public void CleanTagsForAiTouchedWork_UpdatesRealWorkTagsTable()
    {
        var accountName = NewAccountName();
        try
        {
            var service = new WorkService(accountName);
            int workId = service.AddWork(
                title: "标签整理测试",
                originalTitle: "",
                type: "Anime",
                company: "MADHOUSE",
                year: "2024",
                season: "春",
                sourceType: "漫改",
                episodesVolumes: "12",
                progress: "",
                status: "wish",
                rating: 0,
                synopsis: "",
                coverPath: "");

            Assert.True(workId > 0);
            service.AddWorkTag(workId, "2024", source: "AI");
            service.AddWorkTag(workId, "评分:8.5", source: "AI");
            service.AddWorkTag(workId, "MADHOUSE", source: "AI");
            service.AddWorkTag(workId, "CV:早见沙织", source: "AI");
            service.AddWorkTag(workId, "用户手动标签", source: "Manual");

            var cleanup = service.CleanTagsForAiTouchedWork(workId);
            var tags = service.GetWorkTags(workId);

            Assert.Contains("2024", cleanup.TagsToRemove);
            Assert.Contains("评分:8.5", cleanup.TagsToRemove);
            Assert.Contains("MADHOUSE", cleanup.TagsToRemove);
            Assert.Contains("cv:早见沙织", tags);
            Assert.Contains("用户手动标签", tags);
            Assert.DoesNotContain("2024", tags);
            Assert.DoesNotContain("评分:8.5", tags);
            Assert.DoesNotContain("MADHOUSE", tags);
            Assert.DoesNotContain("CV:早见沙织", tags);
        }
        finally
        {
            CleanupDatabaseFile(accountName);
        }
    }

    [Fact]
    public void AddAutomaticWorkTags_NormalizesBeforeWriting()
    {
        var accountName = NewAccountName();
        try
        {
            var service = new WorkService(accountName);
            int workId = service.AddWork(
                title: "自动标签测试",
                originalTitle: "",
                type: "Anime",
                company: "京都动画",
                year: "2023",
                season: "秋",
                sourceType: "原创",
                episodesVolumes: "12",
                progress: "",
                status: "wish",
                rating: 0,
                synopsis: "",
                coverPath: "");

            var added = service.AddAutomaticWorkTags(workId, new[]
            {
                "2023",
                "京都动画",
                "声优:种崎敦美",
                "导演:石原立也",
                "治愈"
            }, new TagPolicy.WorkTagContext
            {
                Year = "2023",
                Season = "秋",
                Type = "Anime",
                SourceType = "原创",
                Company = "京都动画"
            });

            Assert.Equal(new[] { "cv:种崎敦美", "导演:石原立也", "治愈" }, added);
            var tags = service.GetWorkTags(workId);
            Assert.DoesNotContain("2023", tags);
            Assert.DoesNotContain("京都动画", tags);
            Assert.Contains("cv:种崎敦美", tags);
            Assert.Contains("导演:石原立也", tags);
            Assert.Contains("治愈", tags);
        }
        finally
        {
            CleanupDatabaseFile(accountName);
        }
    }

    [Fact]
    public void GenerateTagCleanupPreview_FindsAffectedWorksWithoutChangingData()
    {
        var accountName = NewAccountName();
        try
        {
            var service = new WorkService(accountName);
            int firstId = service.AddWork("全库预览A", "", "Anime", "MADHOUSE", "2024", "春", "漫改", "12", "", "wish", 0, "", "");
            int secondId = service.AddWork("全库预览B", "", "Anime", "京都动画", "2023", "秋", "原创", "12", "", "wish", 0, "", "");

            service.AddWorkTag(firstId, "2024", source: "AI");
            service.AddWorkTag(firstId, "CV:早见沙织", source: "AI");
            service.AddWorkTag(firstId, "悬疑", source: "Manual");
            service.AddWorkTag(secondId, "治愈", source: "Manual");

            var preview = service.GenerateTagCleanupPreview();
            var firstItem = Assert.Single(preview.Items);

            Assert.Equal(firstId, firstItem.WorkId);
            Assert.Equal(1, preview.TotalWorksAffected);
            Assert.Equal(2, preview.TagsToRemoveCount);
            Assert.Equal(1, preview.TagsToAddCount);
            Assert.Contains("2024", firstItem.TagsToRemove);
            Assert.Contains("CV:早见沙织", firstItem.TagsToRemove);
            Assert.Contains("cv:早见沙织", firstItem.TagsToAdd);

            var tagsAfterPreview = service.GetWorkTags(firstId);
            Assert.Contains("2024", tagsAfterPreview);
            Assert.Contains("CV:早见沙织", tagsAfterPreview);
            Assert.Contains("悬疑", tagsAfterPreview);
        }
        finally
        {
            CleanupDatabaseFile(accountName);
        }
    }

    [Fact]
    public void ApplyTagCleanupPreview_CleansAllAffectedWorks()
    {
        var accountName = NewAccountName();
        try
        {
            var service = new WorkService(accountName);
            int workId = service.AddWork("全库应用测试", "", "Anime", "MADHOUSE", "2024", "春", "漫改", "12", "", "wish", 0, "", "");

            service.AddWorkTag(workId, "2024", source: "AI");
            service.AddWorkTag(workId, "MADHOUSE", source: "AI");
            service.AddWorkTag(workId, "声优:种崎敦美", source: "AI");
            service.AddWorkTag(workId, "群像", source: "Manual");

            var preview = service.GenerateTagCleanupPreview();
            var result = service.ApplyTagCleanupPreview(preview);
            var tags = service.GetWorkTags(workId);

            Assert.Equal(1, result.AffectedWorks);
            Assert.Equal(3, result.RemovedTags);
            Assert.Equal(1, result.AddedTags);
            Assert.DoesNotContain("2024", tags);
            Assert.DoesNotContain("MADHOUSE", tags);
            Assert.DoesNotContain("声优:种崎敦美", tags);
            Assert.Contains("cv:种崎敦美", tags);
            Assert.Contains("群像", tags);
        }
        finally
        {
            CleanupDatabaseFile(accountName);
        }
    }

    private static string NewAccountName()
    {
        return $"test_tagpolicy_{Guid.NewGuid():N}";
    }

    private static void CleanupDatabaseFile(string accountName)
    {
        try
        {
            var path = DatabaseHelper.GetDatabasePath(accountName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
