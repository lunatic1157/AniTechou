using System;

namespace AniTechou.Utilities
{
    public static class WorkDataRules
    {
        public static string NormalizeTypeToEnglish(string type)
        {
            return type?.Trim() switch
            {
                "动画" or "Anime" => "Anime",
                "漫画" or "Manga" => "Manga",
                "轻小说" or "LightNovel" => "LightNovel",
                "游戏" or "Game" => "Game",
                _ => type?.Trim() ?? ""
            };
        }

        public static string NormalizeSourceType(string sourceType)
        {
            var value = sourceType?.Trim() ?? "";
            if (string.IsNullOrEmpty(value)) return "";
            if (value is "无" or "none" or "None") return "";
            if (value.Contains("原创", StringComparison.OrdinalIgnoreCase)) return "原创";
            if (value.Contains("漫改", StringComparison.OrdinalIgnoreCase) || value.Contains("漫画", StringComparison.OrdinalIgnoreCase)) return "漫改";
            if (value.Contains("小说", StringComparison.OrdinalIgnoreCase) || value.Contains("书", StringComparison.OrdinalIgnoreCase) || value.Contains("轻小说", StringComparison.OrdinalIgnoreCase)) return "小说改";
            if (value.Contains("游戏", StringComparison.OrdinalIgnoreCase)) return "游戏改";
            if (value.Contains("其他", StringComparison.OrdinalIgnoreCase) || value.Contains("改编", StringComparison.OrdinalIgnoreCase)) return "其他";
            return value;
        }

        public static bool IsSameWork(string existingTitle, string existingOriginalTitle, string existingType, string requestedTitle, string requestedOriginalTitle, string requestedType)
        {
            if (!string.Equals(NormalizeTypeToEnglish(existingType), NormalizeTypeToEnglish(requestedType), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var title = requestedTitle?.Trim() ?? "";
            var originalTitle = requestedOriginalTitle?.Trim() ?? "";
            var existingMainTitle = existingTitle?.Trim() ?? "";
            var existingAltTitle = existingOriginalTitle?.Trim() ?? "";

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(originalTitle))
            {
                return false;
            }

            return IsTitleEquivalent(existingMainTitle, title)
                || IsTitleEquivalent(existingMainTitle, originalTitle)
                || IsTitleEquivalent(existingAltTitle, title)
                || IsTitleEquivalent(existingAltTitle, originalTitle);
        }

        private static bool IsTitleEquivalent(string left, string right)
        {
            return !string.IsNullOrEmpty(left)
                && !string.IsNullOrEmpty(right)
                && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
