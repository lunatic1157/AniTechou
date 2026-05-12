namespace AniTechou.Models
{
    public class WorkInfo
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string OriginalTitle { get; set; }
        public string Type { get; set; }
        public string Company { get; set; }
        public string Author { get; set; }
        public string OriginalWork { get; set; }
        public string Year { get; set; }
        public string Season { get; set; }
        public string SourceType { get; set; }
        public string EpisodesVolumes { get; set; }
        public string Synopsis { get; set; }
        public string CoverPath { get; set; }

        // === 第二层改进：外部数据库 ID ===
        public string BangumiId { get; set; }
        public string MALId { get; set; }
        public string AniListId { get; set; }
        public string VoiceActorInfo { get; set; }
    }
}