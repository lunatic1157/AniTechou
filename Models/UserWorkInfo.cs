namespace AniTechou.Models
{
    public class UserWorkInfo
    {
        public int Id { get; set; }
        public int WorkId { get; set; }
        public string Status { get; set; }
        public string Progress { get; set; }
        public double Rating { get; set; }
        public string StartedDate { get; set; }
        public string FinishedDate { get; set; }
    }
}
