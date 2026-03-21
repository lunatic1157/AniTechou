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
                                    Info = $"{SafeGetString(reader, 2)} · {reader.GetString(3)}",
                                    ProgressValue = ParseProgressToValue(SafeGetString(reader, 4)),
                                    ProgressText = SafeGetString(reader, 4) ?? "未开始",
                                    RatingDisplay = GetRatingDisplay(SafeGetInt(reader, 5))
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

        /// <summary>
        /// 安全获取字符串
        /// </summary>
        private string SafeGetString(SQLiteDataReader reader, int index)
        {
            var value = reader.GetValue(index);
            if (value == null || Convert.IsDBNull(value))
                return "";
            return value.ToString();
        }

        /// <summary>
        /// 安全获取整数
        /// </summary>
        private int SafeGetInt(SQLiteDataReader reader, int index)
        {
            var value = reader.GetValue(index);
            if (value == null || Convert.IsDBNull(value))
                return 0;
            if (int.TryParse(value.ToString(), out int result))
                return result;
            return 0;
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

        /// <summary>
        /// 添加新作品
        /// </summary>
        public int AddWork(string title, string originalTitle, string type, string company,
                   string yearSeason, string episodesVolumes, string progress,
                   string status, int rating, string synopsis, string coverPath)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 处理年份
                            int yearValue = 0;
                            if (!string.IsNullOrEmpty(yearSeason))
                            {
                                // 提取数字部分（如"2024春" -> 2024）
                                var match = System.Text.RegularExpressions.Regex.Match(yearSeason, @"\d+");
                                if (match.Success)
                                    int.TryParse(match.Value, out yearValue);
                            }

                            // 插入作品
                            string insertWork = @"
                        INSERT INTO Works (Title, OriginalTitle, Type, Company, Year, EpisodesVolumes, Synopsis, CoverPath, AddedTime, LastModified)
                        VALUES (@Title, @OriginalTitle, @Type, @Company, @Year, @EpisodesVolumes, @Synopsis, @CoverPath, @AddedTime, @LastModified);
                        SELECT last_insert_rowid();";

                            int workId;
                            using (var cmd = new SQLiteCommand(insertWork, conn))
                            {
                                cmd.Parameters.AddWithValue("@Title", title);
                                cmd.Parameters.AddWithValue("@OriginalTitle", originalTitle ?? "");
                                cmd.Parameters.AddWithValue("@Type", type);
                                cmd.Parameters.AddWithValue("@Company", company ?? "");
                                cmd.Parameters.AddWithValue("@Year", yearValue);
                                cmd.Parameters.AddWithValue("@EpisodesVolumes", episodesVolumes ?? "");
                                cmd.Parameters.AddWithValue("@Synopsis", synopsis ?? "");
                                cmd.Parameters.AddWithValue("@CoverPath", coverPath ?? "");
                                cmd.Parameters.AddWithValue("@AddedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@LastModified", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                workId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // 插入用户列表
                            string insertList = @"
                        INSERT INTO UserList (WorkId, Status, Progress, Rating, LastUpdated)
                        VALUES (@WorkId, @Status, @Progress, @Rating, @LastUpdated)";

                            using (var cmd = new SQLiteCommand(insertList, conn))
                            {
                                cmd.Parameters.AddWithValue("@WorkId", workId);
                                cmd.Parameters.AddWithValue("@Status", status);
                                cmd.Parameters.AddWithValue("@Progress", progress ?? "");
                                cmd.Parameters.AddWithValue("@Rating", rating);
                                cmd.Parameters.AddWithValue("@LastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return workId;
                        }
                        catch
                        {
                            transaction.Rollback();
                            return 0;
                        }
                    }
                }
            }
            catch
            {
                return 0;
            }
        }
    }
}    