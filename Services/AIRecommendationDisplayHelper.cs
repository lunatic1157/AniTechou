using System;

namespace AniTechou.Services
{
    public static class AIRecommendationDisplayHelper
    {
        public static RecommendationDisplay BuildRecommendationDisplay(AIWorkSearchResult work)
        {
            string category = NormalizeRecommendationCategory(work?.recommendationCategory);
            string reason = (work?.recommendationReason ?? "").Trim();

            if (string.IsNullOrEmpty(category) && string.IsNullOrEmpty(reason))
            {
                return new RecommendationDisplay
                {
                    HasRecommendationBlock = true,
                    Category = "推荐理由待补充",
                    Reason = "AI 未返回具体分类或理由，可先根据标题、简介和标签判断是否加入收藏。"
                };
            }

            return new RecommendationDisplay
            {
                HasRecommendationBlock = true,
                Category = category,
                Reason = reason
            };
        }

        public static string NormalizeRecommendationCategory(string category)
        {
            string value = (category ?? "").Trim();
            return value switch
            {
                "similar" or "相似" or "相似推荐" => "相似推荐",
                "taste" or "口味" or "口味推荐" => "口味推荐",
                "explore" or "拓展" or "拓展推荐" => "拓展推荐",
                "classic" or "补课" or "补课推荐" => "补课推荐",
                _ => value
            };
        }

        public class RecommendationDisplay
        {
            public bool HasRecommendationBlock { get; set; }
            public string Category { get; set; } = "";
            public string Reason { get; set; } = "";
        }
    }
}
