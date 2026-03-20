using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;

namespace AniTechou.Services
{
    /// <summary>
    /// 作品数据服务
    /// </summary>
    public class WorkService
    {
        private string _currentAccount;

        public WorkService(string accountName)
        {
            _currentAccount = accountName;
        }

        /// <summary>
        /// 获取作品列表（按类型和状态）
        /// </summary>
        public async Task<List<WorkCardData>> GetWorksAsync(string type, string status)
        {
            return await Task.Run(() =>
            {
                var works = new List<WorkCardData>();
                
                using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                {
                    conn.Open();
                    
                    string sql = @"
                        SELECT w.Id, w.Title, w.Year, w.Company, 
                               ul.Progress, ul.Rating
                        FROM Works w
                        INNER JOIN UserList ul ON w.Id = ul.WorkId
                        WHERE (@Type = 'all' OR w.Type = @Type)
                          AND (@Status = 'all' OR ul.Status = @Status)
                        ORDER BY ul.LastUpdated DESC";
                    
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Type", type);
                        cmd.Parameters.AddWithValue("@Status", status);
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                works.Add(new WorkCardData
                                {
                                    Id = reader.GetInt32(0),
                                    Title = reader.GetString(1),
                                    Info = $"{reader.GetInt32(2)} · {reader.GetString(3)}",
                                    ProgressValue = ParseProgressToValue(reader.GetString(4)),
                                    ProgressText = reader.GetString(4) ?? "未开始",
                                    RatingDisplay = GetRatingDisplay(reader.GetInt32(5))
                                });
                            }
                        }
                    }
                }
                
                return works;
            });
        }

        /// <summary>
        /// 解析进度字符串为数值
        /// </summary>
        private double ParseProgressToValue(string progress)
        {
            if (string.IsNullOrEmpty(progress)) return 0;
            
            var parts = progress.Split('/');
            if (parts.Length == 2 && double.TryParse(parts[0], out double current) && double.TryParse(parts[1], out double total))
            {
                return total > 0 ? (current / total) * 100 : 0;
            }
            return 0;
        }

        /// <summary>
        /// 获取评分显示
        /// </summary>
        private string GetRatingDisplay(int rating)
        {
            if (rating <= 0) return "未评分";
            
            int fullStars = rating / 2;
            string stars = new string('★', fullStars);
            if (rating % 2 == 1)
                stars += "½";
            stars += new string('☆', 5 - fullStars);
            return stars;
        }
    }

    /// <summary>
    /// 作品卡片数据模型
    /// </summary>
    public class WorkCardData
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Info { get; set; } = "";
        public double ProgressValue { get; set; }
        public string ProgressText { get; set; } = "";
        public string RatingDisplay { get; set; } = "";
    }
}