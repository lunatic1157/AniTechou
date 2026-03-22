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
                        SELECT w.Id, w.Title, w.Year, w.Company, w.CoverPath,
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
                                    Id = SafeGetInt(reader, 0),
                                    Title = SafeGetString(reader, 1),
                                    Info = $"{SafeGetString(reader, 2)} · {SafeGetString(reader, 3)}",
                                    CoverPath = SafeGetString(reader, 4),
                                    ProgressValue = ParseProgressToValue(SafeGetString(reader, 5)),
                                    ProgressText = SafeGetString(reader, 5) ?? "未开始",
                                    RatingDisplay = GetRatingDisplay(SafeGetInt(reader, 6))
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
            public string CoverPath { get; set; } = "";
            public double ProgressValue { get; set; }
            public string ProgressText { get; set; } = "";
            public string RatingDisplay { get; set; } = "";
        }

        /// <summary>
        /// 添加新作品
        /// </summary>
        public int AddWork(string title, string originalTitle, string type, string company,
                   string year, string season, string episodesVolumes, string progress,
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
                            if (!string.IsNullOrEmpty(year))
                            {
                                // 提取数字部分
                                var match = System.Text.RegularExpressions.Regex.Match(year, @"\d+");
                                if (match.Success)
                                    int.TryParse(match.Value, out yearValue);
                            }

                            // 插入作品
                            string insertWork = @"
                        INSERT INTO Works (Title, OriginalTitle, Type, Company, Year, Season, EpisodesVolumes, Synopsis, CoverPath, AddedTime, LastModified)
                        VALUES (@Title, @OriginalTitle, @Type, @Company, @Year, @Season, @EpisodesVolumes, @Synopsis, @CoverPath, @AddedTime, @LastModified);
                        SELECT last_insert_rowid();";

                            int workId;
                            using (var cmd = new SQLiteCommand(insertWork, conn))
                            {
                                cmd.Parameters.AddWithValue("@Title", title);
                                cmd.Parameters.AddWithValue("@OriginalTitle", originalTitle ?? "");
                                cmd.Parameters.AddWithValue("@Type", type);
                                cmd.Parameters.AddWithValue("@Company", company ?? "");
                                cmd.Parameters.AddWithValue("@Year", yearValue);
                                cmd.Parameters.AddWithValue("@Season", season ?? "");
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

        public WorkInfo GetWorkById(int workId)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = @"SELECT Id, Title, OriginalTitle, Type, Company, Year, Season, EpisodesVolumes, Synopsis, CoverPath
                               FROM Works WHERE Id = @Id";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", workId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new WorkInfo
                            {
                                Id = SafeGetInt(reader, 0),
                                Title = SafeGetString(reader, 1),
                                OriginalTitle = SafeGetString(reader, 2),
                                Type = SafeGetString(reader, 3),
                                Company = SafeGetString(reader, 4),
                                Year = SafeGetString(reader, 5),
                                Season = SafeGetString(reader, 6),
                                EpisodesVolumes = SafeGetString(reader, 7),
                                Synopsis = SafeGetString(reader, 8),
                                CoverPath = SafeGetString(reader, 9)
                            };
                        }
                    }
                }
            }
            return null;
        }

        public UserWorkInfo GetUserWorkByWorkId(int workId)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "SELECT Id, WorkId, Status, Progress, Rating FROM UserList WHERE WorkId = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new UserWorkInfo
                            {
                                Id = SafeGetInt(reader, 0),
                                WorkId = SafeGetInt(reader, 1),
                                Status = SafeGetString(reader, 2),
                                Progress = SafeGetString(reader, 3),
                                Rating = SafeGetInt(reader, 4)
                            };
                        }
                    }
                }
            }
            return null;
        }

        public void UpdateUserWork(int userListId, string status, string progress, int rating)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                {
                    conn.Open();
                    string sql = @"
                UPDATE UserList 
                SET Status = @Status, 
                    Progress = @Progress, 
                    Rating = @Rating, 
                    LastUpdated = @LastUpdated
                WHERE Id = @Id";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Status", status);
                        cmd.Parameters.AddWithValue("@Progress", progress);
                        cmd.Parameters.AddWithValue("@Rating", rating);
                        cmd.Parameters.AddWithValue("@LastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@Id", userListId);
                        int rows = cmd.ExecuteNonQuery();

                        if (rows == 0)
                        {
                            System.Windows.MessageBox.Show("没有找到要更新的记录");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"更新失败：{ex.Message}");
            }
        }

        public class WorkInfo
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string OriginalTitle { get; set; } = "";
            public string Type { get; set; } = "";
            public string Company { get; set; } = "";
            public string Year { get; set; } = "";
            public string Season { get; set; } = "";
            public string EpisodesVolumes { get; set; } = "";
            public string Synopsis { get; set; } = "";
            public string CoverPath { get; set; } = "";
        }

        public class UserWorkInfo
        {
            public int Id { get; set; }
            public int WorkId { get; set; }
            public string Status { get; set; } = "";
            public string Progress { get; set; } = "";
            public int Rating { get; set; }
        }

        public string GetCurrentAccount()
        {
            return _currentAccount;
        }

        public bool UpdateWorkInfo(int workId, string title, string originalTitle,
                           string type, string company, string year, string season,
                           string episodesVolumes, string synopsis)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                {
                    conn.Open();
                    string sql = @"
                UPDATE Works
                SET Title = @Title,
                    OriginalTitle = @OriginalTitle,
                    Type = @Type,
                    Company = @Company,
                    Year = @Year,
                    Season = @Season,
                    EpisodesVolumes = @EpisodesVolumes,
                    Synopsis = @Synopsis,
                    LastModified = @LastModified
                WHERE Id = @Id";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@OriginalTitle", originalTitle ?? "");
                        cmd.Parameters.AddWithValue("@Type", type);
                        cmd.Parameters.AddWithValue("@Company", company ?? "");
                        cmd.Parameters.AddWithValue("@Year", year ?? "");
                        cmd.Parameters.AddWithValue("@Season", season ?? "");
                        cmd.Parameters.AddWithValue("@EpisodesVolumes", episodesVolumes ?? "");
                        cmd.Parameters.AddWithValue("@Synopsis", synopsis ?? "");
                        cmd.Parameters.AddWithValue("@LastModified", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@Id", workId);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"UpdateWorkInfo: workId={workId}, rowsAffected={rowsAffected}");
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateWorkInfo error: {ex.Message}");
                return false;
            }
        }
    }
}    