using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniTechou.Models;

namespace AniTechou.Services
{
    public class RecommendationProfile
    {
        private static readonly string[] PeoplePrefixes =
        {
            "cv:", "导演:", "脚本:", "音乐:", "角色设计:", "原作:", "插画:"
        };

        public List<WorkSignal> HighRatedWorks { get; set; } = new List<WorkSignal>();
        public List<WorkSignal> ActiveWorks { get; set; } = new List<WorkSignal>();
        public List<WorkSignal> CompletedWorks { get; set; } = new List<WorkSignal>();
        public List<RankedSignal> CommonTags { get; set; } = new List<RankedSignal>();
        public List<RankedSignal> PeopleTags { get; set; } = new List<RankedSignal>();
        public List<RankedSignal> TypePreferences { get; set; } = new List<RankedSignal>();

        public static RecommendationProfile Build(IEnumerable<WorkSnapshot> snapshots)
        {
            var source = (snapshots ?? Enumerable.Empty<WorkSnapshot>())
                .Where(x => x?.Work != null)
                .ToList();

            return new RecommendationProfile
            {
                HighRatedWorks = source
                    .Where(x => x.UserWork?.Rating >= 8)
                    .OrderByDescending(x => x.UserWork.Rating)
                    .ThenBy(x => x.Work.Title)
                    .Take(8)
                    .Select(ToWorkSignal)
                    .ToList(),

                ActiveWorks = source
                    .Where(x => string.Equals(x.UserWork?.Status, "doing", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.UserWork?.Rating ?? 0)
                    .ThenBy(x => x.Work.Title)
                    .Take(8)
                    .Select(ToWorkSignal)
                    .ToList(),

                CompletedWorks = source
                    .Where(x => string.Equals(x.UserWork?.Status, "done", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.UserWork?.Rating ?? 0)
                    .ThenBy(x => x.Work.Title)
                    .Take(8)
                    .Select(ToWorkSignal)
                    .ToList(),

                CommonTags = RankSignals(source
                    .SelectMany(x => x.Tags ?? new List<string>())
                    .Where(IsCommonTag), 10),

                PeopleTags = RankSignals(source
                    .SelectMany(x => x.Tags ?? new List<string>())
                    .Where(IsPeopleTag), 10),

                TypePreferences = RankSignals(source
                    .Select(x => x.Work.Type)
                    .Where(t => !string.IsNullOrWhiteSpace(t)), 6)
            };
        }

        public static RecommendationProfile FromWorks(
            IEnumerable<WorkService.WorkCardData> works,
            WorkService workService)
        {
            var snapshots = (works ?? Enumerable.Empty<WorkService.WorkCardData>())
                .Select(work => new WorkSnapshot
                {
                    Work = work,
                    UserWork = workService?.GetUserWorkByWorkId(work.Id),
                    Tags = workService?.GetWorkTags(work.Id) ?? new List<string>()
                });

            return Build(snapshots);
        }

        public string FormatForPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("【用户画像】");
            AppendWorkSignals(sb, "高评分作品", HighRatedWorks);
            AppendWorkSignals(sb, "看过/在看", CompletedWorks.Concat(ActiveWorks).Take(12));
            AppendRankedSignals(sb, "常见普通标签", CommonTags);
            AppendRankedSignals(sb, "人员标签", PeopleTags);
            AppendRankedSignals(sb, "类型偏好", TypePreferences);
            sb.AppendLine("推荐分类要求: 相似推荐要说明相似作品信号；口味推荐要说明标签/类型/高评分信号；拓展推荐要说明从既有口味延伸到的新方向；补课推荐要说明经典、缺口或应补作品信号。");
            return sb.ToString();
        }

        private static WorkSignal ToWorkSignal(WorkSnapshot snapshot)
        {
            return new WorkSignal
            {
                Title = snapshot.Work.Title ?? "",
                Type = snapshot.Work.Type ?? "",
                Status = snapshot.UserWork?.Status ?? "",
                Rating = snapshot.UserWork?.Rating ?? 0
            };
        }

        private static List<RankedSignal> RankSignals(IEnumerable<string> values, int limit)
        {
            return values
                .Select(v => (v ?? "").Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(limit)
                .Select(g => new RankedSignal { Value = g.Key, Count = g.Count() })
                .ToList();
        }

        private static bool IsPeopleTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return false;
            return PeoplePrefixes.Any(prefix => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsCommonTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return false;
            return !tag.Contains(':') && !tag.Contains('：');
        }

        private static void AppendWorkSignals(StringBuilder sb, string label, IEnumerable<WorkSignal> signals)
        {
            var values = signals
                .Where(x => !string.IsNullOrWhiteSpace(x.Title))
                .Select(x =>
                {
                    string rating = x.Rating > 0 ? $"({x.Rating:F1})" : "";
                    string status = string.IsNullOrWhiteSpace(x.Status) ? "" : $"/{x.Status}";
                    return $"{x.Title}{rating}{status}";
                })
                .ToList();

            sb.AppendLine(values.Count > 0 ? $"{label}: {string.Join("、", values)}" : $"{label}: （暂无）");
        }

        private static void AppendRankedSignals(StringBuilder sb, string label, IEnumerable<RankedSignal> signals)
        {
            var values = signals
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{x.Value}({x.Count})")
                .ToList();

            sb.AppendLine(values.Count > 0 ? $"{label}: {string.Join("、", values)}" : $"{label}: （暂无）");
        }

        public class WorkSnapshot
        {
            public WorkService.WorkCardData Work { get; set; }
            public UserWorkInfo UserWork { get; set; }
            public List<string> Tags { get; set; } = new List<string>();
        }

        public class WorkSignal
        {
            public string Title { get; set; } = "";
            public string Type { get; set; } = "";
            public string Status { get; set; } = "";
            public double Rating { get; set; }
        }

        public class RankedSignal
        {
            public string Value { get; set; } = "";
            public int Count { get; set; }
        }
    }
}
