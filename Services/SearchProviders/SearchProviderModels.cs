using System.Collections.Generic;

namespace AniTechou.Services.SearchProviders
{
    /// <summary>
    /// 统一的外部搜索结果模型（来自 Bangumi/AniList/MAL 等）
    /// </summary>
    public class ExternalSearchResult
    {
        public string Title { get; set; } = "";
        public string OriginalTitle { get; set; } = "";
        public string Type { get; set; } = "";
        public string Year { get; set; } = "";
        public string Season { get; set; } = "";
        public string Synopsis { get; set; } = "";
        public string CoverUrl { get; set; } = "";
        public string BangumiId { get; set; } = "";
        public string MALId { get; set; } = "";
        public string AniListId { get; set; } = "";
        public string Company { get; set; } = "";
        public string Author { get; set; } = "";
        public string OriginalWork { get; set; } = "";
        public string SourceType { get; set; } = "";
        public string Episodes { get; set; } = "";
        public List<string> Tags { get; set; } = new List<string>();
        public string VoiceActorInfo { get; set; } = "";
        /// <summary>数据来源: "bangumi", "mal", "anilist"</summary>
        public string Source { get; set; } = "";
    }
}
