using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AniTechou.Models;
using AniTechou.Services.SearchProviders;

namespace AniTechou.Utilities
{
    public static class TagPolicy
    {
        private static readonly HashSet<string> RedundantExactTags = new(StringComparer.OrdinalIgnoreCase)
        {
            "春", "夏", "秋", "冬",
            "动画", "動漫", "Anime", "anime",
            "漫画", "漫畫", "Manga", "manga",
            "轻小说", "輕小說", "LightNovel", "lightnovel", "Novel", "novel",
            "游戏", "Game", "game",
            "想看", "在看", "看过", "搁置", "抛弃",
            "wish", "doing", "done", "on_hold", "dropped",
            "原创", "漫改", "小说改", "游戏改", "其他"
        };

        private static readonly Dictionary<string, string> PrefixMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["cv"] = "cv",
            ["CV"] = "cv",
            ["声优"] = "cv",
            ["声優"] = "cv",
            ["配音"] = "cv",
            ["cast"] = "cv",
            ["导演"] = "导演",
            ["监督"] = "导演",
            ["監督"] = "导演",
            ["director"] = "导演",
            ["脚本"] = "脚本",
            ["剧本"] = "脚本",
            ["编剧"] = "脚本",
            ["系列构成"] = "脚本",
            ["音乐"] = "音乐",
            ["音楽"] = "音乐",
            ["作曲"] = "音乐",
            ["角色设计"] = "角色设计",
            ["人物设定"] = "角色设计",
            ["人设"] = "角色设计",
            ["キャラクターデザイン"] = "角色设计",
            ["原作"] = "原作",
            ["原作者"] = "原作",
            ["著者"] = "原作",
            ["作者"] = "原作",
            ["插画"] = "插画",
            ["插图"] = "插画",
            ["イラスト"] = "插画"
        };

        public class WorkTagContext
        {
            public string Title { get; set; } = "";
            public string OriginalTitle { get; set; } = "";
            public string Type { get; set; } = "";
            public string Year { get; set; } = "";
            public string Season { get; set; } = "";
            public string SourceType { get; set; } = "";
            public string Company { get; set; } = "";
            public string Author { get; set; } = "";
            public string OriginalWork { get; set; } = "";
        }

        public class TagCleanupPlan
        {
            public List<string> TagsToRemove { get; set; } = new();
            public List<string> TagsToAdd { get; set; } = new();
        }

        public static WorkTagContext FromWork(WorkInfo work)
        {
            if (work == null) return new WorkTagContext();
            return new WorkTagContext
            {
                Title = work.Title ?? "",
                OriginalTitle = work.OriginalTitle ?? "",
                Type = work.Type ?? "",
                Year = work.Year ?? "",
                Season = work.Season ?? "",
                SourceType = work.SourceType ?? "",
                Company = work.Company ?? "",
                Author = work.Author ?? "",
                OriginalWork = work.OriginalWork ?? ""
            };
        }

        public static WorkTagContext FromExternalResult(ExternalSearchResult work)
        {
            if (work == null) return new WorkTagContext();
            return new WorkTagContext
            {
                Title = work.Title ?? "",
                OriginalTitle = work.OriginalTitle ?? "",
                Type = work.Type ?? "",
                Year = work.Year ?? "",
                Season = work.Season ?? "",
                SourceType = work.SourceType ?? "",
                Company = work.Company ?? "",
                Author = work.Author ?? "",
                OriginalWork = work.OriginalWork ?? ""
            };
        }

        public static WorkTagContext FromAiResult(dynamic work)
        {
            if (work == null) return new WorkTagContext();
            return new WorkTagContext
            {
                Title = work.title ?? "",
                OriginalTitle = work.originalTitle ?? "",
                Type = work.type ?? "",
                Year = work.year ?? "",
                Season = work.season ?? "",
                SourceType = work.sourceType ?? "",
                Company = work.company ?? "",
                Author = work.author ?? "",
                OriginalWork = work.originalWork ?? ""
            };
        }

        public static List<string> NormalizeAutomaticTags(IEnumerable<string> tags, WorkTagContext context = null)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawTag in tags ?? Enumerable.Empty<string>())
            {
                foreach (var normalized in NormalizeOne(rawTag, context ?? new WorkTagContext()))
                {
                    if (seen.Add(normalized))
                        result.Add(normalized);
                }
            }

            return result;
        }

        public static TagCleanupPlan PlanCleanupForAiTouchedWork(IEnumerable<string> existingTags, WorkTagContext context = null)
        {
            var plan = new TagCleanupPlan();
            var addSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawTag in existingTags ?? Enumerable.Empty<string>())
            {
                string tag = Clean(rawTag);
                if (string.IsNullOrEmpty(tag)) continue;

                var normalized = NormalizeOne(tag, context ?? new WorkTagContext()).ToList();
                bool shouldRemove = IsRedundantMetadataTag(tag, context ?? new WorkTagContext());
                bool changedByNormalization = normalized.Count == 1 &&
                    !string.Equals(normalized[0], tag, StringComparison.Ordinal);

                if (shouldRemove || changedByNormalization || normalized.Count > 1)
                    plan.TagsToRemove.Add(tag);

                if (!shouldRemove)
                {
                    foreach (var normalizedTag in normalized)
                    {
                        if (!string.Equals(normalizedTag, tag, StringComparison.Ordinal) &&
                            addSeen.Add(normalizedTag))
                        {
                            plan.TagsToAdd.Add(normalizedTag);
                        }
                    }
                }
            }

            return plan;
        }

        public static string GetCategoryForTag(string tag)
        {
            string normalized = Clean(tag);
            if (normalized.StartsWith("cv:", StringComparison.OrdinalIgnoreCase)) return "声优";
            if (normalized.StartsWith("导演:", StringComparison.Ordinal)) return "导演";
            if (normalized.StartsWith("脚本:", StringComparison.Ordinal)) return "脚本";
            if (normalized.StartsWith("音乐:", StringComparison.Ordinal)) return "音乐";
            if (normalized.StartsWith("角色设计:", StringComparison.Ordinal)) return "角色设计";
            if (normalized.StartsWith("原作:", StringComparison.Ordinal)) return "原作";
            if (normalized.StartsWith("插画:", StringComparison.Ordinal)) return "插画";
            return "AI";
        }

        private static IEnumerable<string> NormalizeOne(string rawTag, WorkTagContext context)
        {
            string tag = Clean(rawTag);
            if (string.IsNullOrEmpty(tag)) yield break;
            if (IsRedundantMetadataTag(tag, context)) yield break;

            var prefixed = TryNormalizePeopleTag(tag);
            if (prefixed.Count > 0)
            {
                foreach (var item in prefixed)
                    yield return item;
                yield break;
            }

            yield return tag;
        }

        private static List<string> TryNormalizePeopleTag(string tag)
        {
            var match = Regex.Match(tag, @"^\s*([^:：]{1,20})\s*[:：]\s*(.+?)\s*$");
            if (!match.Success) return new List<string>();

            string prefix = match.Groups[1].Value.Trim();
            string value = Clean(match.Groups[2].Value);
            if (string.IsNullOrEmpty(value) || !PrefixMap.TryGetValue(prefix, out var standardPrefix))
                return new List<string>();

            var result = new List<string>();
            foreach (var name in SplitPeopleNames(value))
            {
                if (!string.IsNullOrWhiteSpace(name))
                    result.Add($"{standardPrefix}:{name.Trim()}");
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<string> SplitPeopleNames(string value)
        {
            return value.Split(new[] { '、', '，', ',', '；', ';' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(Clean)
                .Where(v => !string.IsNullOrEmpty(v));
        }

        private static bool IsRedundantMetadataTag(string tag, WorkTagContext context)
        {
            if (Regex.IsMatch(tag, @"^(年份[:：]?)?\d{4}年?(\d{1,2}月)?$") ||
                Regex.IsMatch(tag, @"^\d{4}[-/.]\d{1,2}$"))
                return true;

            if (Regex.IsMatch(tag, @"^(评分|MAL评分|AniList评分|Bangumi评分|BGM评分|bgm评分)[:：]?\s*\d+(\.\d+)?%?$", RegexOptions.IgnoreCase))
                return true;

            if (RedundantExactTags.Contains(tag))
                return true;

            if (!string.IsNullOrWhiteSpace(context?.Year) &&
                (string.Equals(tag, context.Year, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(tag, $"{context.Year}年", StringComparison.OrdinalIgnoreCase) ||
                 Regex.IsMatch(tag, $"^{Regex.Escape(context.Year)}年\\d{{1,2}}月$") ||
                 Regex.IsMatch(tag, $"^{Regex.Escape(context.Year)}[-/.]\\d{{1,2}}$")))
                return true;

            if (!string.IsNullOrWhiteSpace(context?.Season) &&
                string.Equals(tag, context.Season, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(context?.SourceType) &&
                string.Equals(tag, context.SourceType, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(context?.Type) &&
                string.Equals(tag, context.Type, StringComparison.OrdinalIgnoreCase))
                return true;

            var productionMatch = Regex.Match(tag, @"^(制作|制作公司|动画制作|製作)[:：]\s*(.+)$");
            if (productionMatch.Success)
                return IsCompanyName(productionMatch.Groups[2].Value, context);

            return IsCompanyName(tag, context);
        }

        private static bool IsCompanyName(string tag, WorkTagContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.Company)) return false;

            string normalizedTag = NormalizeName(tag);
            foreach (var company in context.Company.Split(new[] { '/', '／', ',', '，', '、', ';', '；' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(normalizedTag, NormalizeName(company), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string NormalizeName(string value)
        {
            return Clean(value).Replace(" ", "");
        }

        private static string Clean(string value)
        {
            return (value ?? "").Trim().Trim('　');
        }
    }
}
