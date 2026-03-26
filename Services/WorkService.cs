using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AniTechou.Services
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
    }

    public class UserWorkInfo
    {
        public int Id { get; set; }
        public int WorkId { get; set; }
        public string Status { get; set; }
        public string Progress { get; set; }
        public int Rating { get; set; }
    }

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
            return await GetWorksAsync(type, status, "全部年份", "全部季节", "全部原作", "全部制作", "全部评分", new List<string>());
        }

        /// <summary>
        /// 获取作品列表（支持所有筛选条件）
        /// </summary>
        public async Task<List<WorkCardData>> GetWorksAsync(string type, string status, string year, string season,
                                                     string sourceType, string studio, string rating, List<string> tags)
        {
            return await Task.Run(() =>
            {
                var works = new List<WorkCardData>();

                using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                {
                    conn.Open();

                    string sql = @"
                        SELECT w.Id, w.Title, w.OriginalTitle, w.Year, w.Company, w.CoverPath,
                               ul.Progress, ul.Rating, w.Author, w.Type
                        FROM Works w
                        INNER JOIN UserList ul ON w.Id = ul.WorkId
                        WHERE 1=1";

                    var parameters = new List<SQLiteParameter>();

                    // 类型筛选
                    if (type != "all")
                    {
                        sql += " AND w.Type = @Type";
                        parameters.Add(new SQLiteParameter("@Type", type));
                    }

                    // 状态筛选
                    if (status != "all")
                    {
                        sql += " AND ul.Status = @Status";
                        parameters.Add(new SQLiteParameter("@Status", status));
                    }

                    // 年份筛选
                    if (year != "全部年份" && !string.IsNullOrEmpty(year))
                    {
                        sql += " AND w.Year LIKE @Year";
                        parameters.Add(new SQLiteParameter("@Year", $"{year}%"));
                    }

                    // 季节筛选
                    if (season != "全部季节" && !string.IsNullOrEmpty(season))
                    {
                        sql += " AND w.Season = @Season";
                        parameters.Add(new SQLiteParameter("@Season", season));
                    }

                    // 原作类型筛选
                    if (sourceType != "全部原作" && !string.IsNullOrEmpty(sourceType))
                    {
                        sql += " AND w.SourceType = @SourceType";
                        parameters.Add(new SQLiteParameter("@SourceType", sourceType));
                    }

                    // 制作公司筛选
                    if (studio != "全部制作" && !string.IsNullOrEmpty(studio))
                    {
                        sql += " AND w.Company = @Studio";
                        parameters.Add(new SQLiteParameter("@Studio", studio));
                    }

                    // 评分筛选
                    if (rating != "全部评分" && !string.IsNullOrEmpty(rating))
                    {
                        int minRating = rating switch
                        {
                            "★ 1-2分" => 1,
                            "★★ 3-4分" => 3,
                            "★★★ 5-6分" => 5,
                            "★★★★ 7-8分" => 7,
                            "★★★★★ 9-10分" => 9,
                            _ => 0
                        };
                        int maxRating = minRating + 1;
                        sql += " AND ul.Rating >= @MinRating AND ul.Rating <= @MaxRating";
                        parameters.Add(new SQLiteParameter("@MinRating", minRating));
                        parameters.Add(new SQLiteParameter("@MaxRating", maxRating));
                    }

                    // 标签筛选
                    if (tags != null && tags.Count > 0)
                    {
                        sql += " AND EXISTS (SELECT 1 FROM WorkTags wt WHERE wt.WorkId = w.Id AND wt.TagName IN (";
                        for (int i = 0; i < tags.Count; i++)
                        {
                            sql += $"@Tag{i}";
                            if (i < tags.Count - 1) sql += ",";
                            parameters.Add(new SQLiteParameter($"@Tag{i}", tags[i]));
                        }
                        sql += "))";
                    }

                    sql += " ORDER BY ul.LastUpdated DESC";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string company = SafeGetString(reader, 4);
                                string author = SafeGetString(reader, 8);
                                string displayMaker = !string.IsNullOrEmpty(company) ? company : author;

                                works.Add(new WorkCardData
                                {
                                    Id = SafeGetInt(reader, 0),
                                    Title = SafeGetString(reader, 1),
                                    OriginalTitle = SafeGetString(reader, 2),
                                    Type = SafeGetString(reader, 9), // 新增
                                    Info = $"{SafeGetString(reader, 3)} · {displayMaker}",
                                    CoverPath = SafeGetString(reader, 5),
                                    ProgressValue = ParseProgressToValue(SafeGetString(reader, 6)),
                                    ProgressText = SafeGetString(reader, 6) ?? "未开始",
                                    RatingDisplay = GetRatingDisplay(SafeGetInt(reader, 7))
                                });
                            }
                        }
                    }
                }

                return works;
            });
        }

        /// <summary>
        /// 获取所有年份
        /// </summary>
        public List<string> GetAllYears()
        {
            var years = new List<string>();
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "SELECT DISTINCT Year FROM Works WHERE Year IS NOT NULL AND Year != '' ORDER BY Year DESC";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        years.Add(SafeGetString(reader, 0));
                    }
                }
            }
            return years;
        }

        /// <summary>
        /// 获取所有制作公司
        /// </summary>
        public List<string> GetAllStudios()
        {
            var studios = new List<string>();
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "SELECT DISTINCT Company FROM Works WHERE Company IS NOT NULL AND Company != '' ORDER BY Company";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        studios.Add(SafeGetString(reader, 0));
                    }
                }
            }
            return studios;
        }

        /// <summary>
        /// 获取所有标签
        /// </summary>
        public List<string> GetAllTags()
        {
            var tags = new List<string>();
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "SELECT DISTINCT TagName FROM WorkTags ORDER BY TagName";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tags.Add(SafeGetString(reader, 0));
                    }
                }
            }
            return tags;
        }

        /// <summary>
        /// 获取所有笔记标签
        /// </summary>
        public List<string> GetAllNoteTags()
        {
            var tags = new List<string>();
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "SELECT DISTINCT TagName FROM NoteTags ORDER BY TagName";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tags.Add(SafeGetString(reader, 0));
                    }
                }
            }
            return tags;
        }

        /// <summary>
        /// 获取作品的所有标签
        /// </summary>
        public List<string> GetWorkTags(int workId)
        {
            var tags = new List<string>();
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "SELECT TagName FROM WorkTags WHERE WorkId = @WorkId ORDER BY TagName";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tags.Add(SafeGetString(reader, 0));
                        }
                    }
                }
            }
            return tags;
        }

        /// <summary>
        /// 添加作品标签
        /// </summary>
        public bool AddWorkTag(int workId, string tagName, string category = "个人", string source = "Manual")
        {
            if (string.IsNullOrWhiteSpace(tagName)) return false;

            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = @"INSERT OR IGNORE INTO WorkTags (WorkId, TagName, Category, Source)
                        VALUES (@WorkId, @TagName, @Category, @Source)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    cmd.Parameters.AddWithValue("@TagName", tagName.Trim());
                    cmd.Parameters.AddWithValue("@Category", category);
                    cmd.Parameters.AddWithValue("@Source", source);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>
        /// 删除作品标签
        /// </summary>
        public List<WorkCardData> GetAllWorksForSearch()
        {
            var works = new List<WorkCardData>();
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = @"SELECT w.Id, w.Title, w.OriginalTitle, w.Year, w.Company, w.CoverPath, ul.Progress, ul.Rating, w.Author, w.Type 
                               FROM Works w 
                               INNER JOIN UserList ul ON w.Id = ul.WorkId 
                               ORDER BY w.Title";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string company = SafeGetString(reader, 4);
                            string author = SafeGetString(reader, 8);
                            string displayMaker = !string.IsNullOrEmpty(company) ? company : author;

                            works.Add(new WorkCardData
                            {
                                Id = SafeGetInt(reader, 0),
                                Title = SafeGetString(reader, 1),
                                OriginalTitle = SafeGetString(reader, 2),
                                Type = SafeGetString(reader, 9),
                                Info = $"{SafeGetString(reader, 3)} · {displayMaker}",
                                CoverPath = SafeGetString(reader, 5),
                                ProgressValue = ParseProgressToValue(SafeGetString(reader, 6)),
                                ProgressText = SafeGetString(reader, 6) ?? "未开始",
                                RatingDisplay = GetRatingDisplay(SafeGetInt(reader, 7))
                            });
                        }
                    }
                }
            }
            return works;
        }
        public async Task<List<WorkCardData>> SearchWorksByNameAsync(string title)
        {
            return await Task.Run(() =>
            {
                var works = new List<WorkCardData>();
                using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                {
                    conn.Open();
                    string sql = @"SELECT w.Id, w.Title, w.OriginalTitle, w.Year, w.Company, w.CoverPath, ul.Progress, ul.Rating, w.Author, w.Type 
                                   FROM Works w 
                                   INNER JOIN UserList ul ON w.Id = ul.WorkId 
                                   WHERE w.Title LIKE @Title OR w.OriginalTitle LIKE @Title";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", $"%{title}%");
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string company = SafeGetString(reader, 4);
                                string author = SafeGetString(reader, 8);
                                string displayMaker = !string.IsNullOrEmpty(company) ? company : author;

                                works.Add(new WorkCardData
                                {
                                    Id = SafeGetInt(reader, 0),
                                    Title = SafeGetString(reader, 1),
                                    OriginalTitle = SafeGetString(reader, 2),
                                    Type = SafeGetString(reader, 9),
                                    Info = $"{SafeGetString(reader, 3)} · {displayMaker}",
                                    CoverPath = SafeGetString(reader, 5),
                                    ProgressValue = ParseProgressToValue(SafeGetString(reader, 6)),
                                    ProgressText = SafeGetString(reader, 6) ?? "未开始",
                                    RatingDisplay = GetRatingDisplay(SafeGetInt(reader, 7))
                                });
                            }
                        }
                    }
                }
                return works;
            });
        }

        // ==========================================
        // 关联作品 (Work Relations)
        // ==========================================

        /// <summary>
        /// 添加关联作品（双向绑定）
        /// </summary>
        public bool AddWorkRelation(int sourceWorkId, int targetWorkId)
        {
            if (sourceWorkId == targetWorkId) return false;

            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string sql = @"INSERT OR IGNORE INTO WorkRelations (SourceWorkId, TargetWorkId) VALUES (@S1, @T1), (@T2, @S2)";
                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@S1", sourceWorkId);
                            cmd.Parameters.AddWithValue("@T1", targetWorkId);
                            cmd.Parameters.AddWithValue("@T2", targetWorkId);
                            cmd.Parameters.AddWithValue("@S2", sourceWorkId);
                            cmd.ExecuteNonQuery();
                        }
                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// 删除关联作品（双向解除）
        /// </summary>
        public bool RemoveWorkRelation(int sourceWorkId, int targetWorkId)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = @"DELETE FROM WorkRelations 
                               WHERE (SourceWorkId = @S1 AND TargetWorkId = @T1) 
                                  OR (SourceWorkId = @T2 AND TargetWorkId = @S2)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@S1", sourceWorkId);
                    cmd.Parameters.AddWithValue("@T1", targetWorkId);
                    cmd.Parameters.AddWithValue("@T2", targetWorkId);
                    cmd.Parameters.AddWithValue("@S2", sourceWorkId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>
        /// 获取某部作品的所有关联作品卡片数据
        /// </summary>
        public List<WorkCardData> GetRelatedWorks(int workId)
        {
            var works = new List<WorkCardData>();
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = @"SELECT w.Id, w.Title, w.OriginalTitle, w.Year, w.Company, w.CoverPath, ul.Progress, ul.Rating, w.Author, w.Type 
                               FROM Works w 
                               INNER JOIN UserList ul ON w.Id = ul.WorkId 
                               INNER JOIN WorkRelations wr ON w.Id = wr.TargetWorkId
                               WHERE wr.SourceWorkId = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string company = SafeGetString(reader, 4);
                            string author = SafeGetString(reader, 8);
                            string displayMaker = !string.IsNullOrEmpty(company) ? company : author;

                            works.Add(new WorkCardData
                            {
                                Id = SafeGetInt(reader, 0),
                                Title = SafeGetString(reader, 1),
                                OriginalTitle = SafeGetString(reader, 2),
                                Type = SafeGetString(reader, 9),
                                Info = $"{SafeGetString(reader, 3)} · {displayMaker}",
                                CoverPath = SafeGetString(reader, 5),
                                ProgressValue = ParseProgressToValue(SafeGetString(reader, 6)),
                                ProgressText = SafeGetString(reader, 6) ?? "未开始",
                                RatingDisplay = GetRatingDisplay(SafeGetInt(reader, 7))
                            });
                        }
                    }
                }
            }
            return works;
        }

        public bool UpdateWorkStatus(int workId, string status)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE UserList SET Status = @Status, LastUpdated = @Now WHERE WorkId = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool UpdateWorkProgress(int workId, string progress)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE UserList SET Progress = @Progress, LastUpdated = @Now WHERE WorkId = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Progress", progress);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool UpdateWorkRating(int workId, int rating)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE UserList SET Rating = @Rating, LastUpdated = @Now WHERE WorkId = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Rating", rating > 0 ? rating : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool UpdateWorkEpisodes(int workId, string episodes)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE Works SET EpisodesVolumes = @Episodes, LastModified = @Now WHERE Id = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Episodes", episodes);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool UpdateWorkSeason(int workId, string season)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE Works SET Season = @Season, LastModified = @Now WHERE Id = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Season", season);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool UpdateWorkSourceType(int workId, string sourceType)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE Works SET SourceType = @SourceType, LastModified = @Now WHERE Id = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@SourceType", sourceType);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool UpdateWorkSynopsis(int workId, string synopsis)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE Works SET Synopsis = @Synopsis, LastModified = @Now WHERE Id = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Synopsis", synopsis);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool UpdateWorkCompany(int workId, string company)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE Works SET Company = @Company, LastModified = @Now WHERE Id = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Company", company);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool UpdateWorkAuthor(int workId, string author)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE Works SET Author = @Author, LastModified = @Now WHERE Id = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Author", author);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool UpdateWorkOriginalWork(int workId, string originalWork)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE Works SET OriginalWork = @OriginalWork, LastModified = @Now WHERE Id = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@OriginalWork", originalWork);
                    cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>
        /// 批量统一标签
        /// </summary>
        public int UnifyTags(string targetTag, string newTag)
        {
            if (string.IsNullOrWhiteSpace(targetTag) || string.IsNullOrWhiteSpace(newTag)) return 0;

            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. 找出所有包含 targetTag 的作品ID
                        var workIds = new List<int>();
                        string findSql = "SELECT DISTINCT WorkId FROM WorkTags WHERE TagName = @TargetTag";
                        using (var findCmd = new SQLiteCommand(findSql, conn))
                        {
                            findCmd.Parameters.AddWithValue("@TargetTag", targetTag);
                            using (var reader = findCmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    workIds.Add(reader.GetInt32(0));
                                }
                            }
                        }

                        if (workIds.Count == 0) return 0;

                        // 2. 为这些作品添加 newTag (如果不存在)
                        string insertSql = "INSERT OR IGNORE INTO WorkTags (WorkId, TagName) VALUES (@WorkId, @NewTag)";
                        foreach (var workId in workIds)
                        {
                            using (var insertCmd = new SQLiteCommand(insertSql, conn))
                            {
                                insertCmd.Parameters.AddWithValue("@WorkId", workId);
                                insertCmd.Parameters.AddWithValue("@NewTag", newTag);
                                insertCmd.ExecuteNonQuery();
                            }
                        }

                        // 3. 从这些作品中删除 targetTag
                        string deleteSql = "DELETE FROM WorkTags WHERE TagName = @TargetTag AND WorkId IN (" + string.Join(",", workIds) + ")";
                        using (var deleteCmd = new SQLiteCommand(deleteSql, conn))
                        {
                            deleteCmd.Parameters.AddWithValue("@TargetTag", targetTag);
                            deleteCmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return workIds.Count;
                    }
                    catch
                    {
                        transaction.Rollback();
                        return 0;
                    }
                }
            }
        }

        public bool RemoveWorkTag(int workId, string tagName)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "DELETE FROM WorkTags WHERE WorkId = @WorkId AND TagName = @TagName";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    cmd.Parameters.AddWithValue("@TagName", tagName);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
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
        /// 下载并保存封面图片
        /// </summary>
        /// <param name="url">图片URL</param>
        /// <param name="workId">作品ID</param>
        /// <returns>本地路径</returns>
        public async Task<string> DownloadAndSaveCoverAsync(string urlOrBangumiInfo, int workId)
        {
            if (string.IsNullOrWhiteSpace(urlOrBangumiInfo)) return "";

            var targetUrls = new List<string>();
            string originalUrl = urlOrBangumiInfo;
            string bangumiId = "";
            string searchTitle = "";

            // 解析我们自定义的格式 "bgm_id:{id}|{url}"
            if (urlOrBangumiInfo.StartsWith("bgm_id:"))
            {
                var parts = urlOrBangumiInfo.Split('|', 2);
                if (parts.Length == 2)
                {
                    bangumiId = parts[0].Replace("bgm_id:", "").Trim();
                    originalUrl = parts[1];
                }
            }
            else if (urlOrBangumiInfo.StartsWith("search_title:"))
            {
                searchTitle = urlOrBangumiInfo.Replace("search_title:", "").Trim();
                originalUrl = ""; // 放弃原始链接，完全依赖搜索
            }

            // 清洗 URL
            if (!string.IsNullOrEmpty(originalUrl))
            {
                originalUrl = originalUrl.Trim().TrimEnd(')', ':', ']', '}', '。', '，', ',', '.');
                if (originalUrl.Contains("?"))
                {
                    originalUrl = originalUrl.Split('?')[0];
                }
            }

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(30); 
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
                
                string jikanSearchQuery = searchTitle;
                
                // 如果没有传入 searchTitle，从数据库中获取标题用于搜索
                if (string.IsNullOrEmpty(jikanSearchQuery))
                {
                    using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                    {
                        conn.Open();
                        using (var cmd = new SQLiteCommand("SELECT Title, OriginalTitle, Year FROM Works WHERE Id = @Id", conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", workId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string title = SafeGetString(reader, 0);
                                    string originalTitle = SafeGetString(reader, 1);
                                    string year = SafeGetString(reader, 2);
                                    
                                    // 基础关键词
                                    jikanSearchQuery = string.IsNullOrEmpty(originalTitle) ? title : originalTitle;
                                    
                                    // 【优化】多季度作品防误伤：把年份加进搜索词，防止“咒术回战 第2季”搜出第一季的图
                                    if (!string.IsNullOrEmpty(year))
                                    {
                                        // 提取年份数字
                                        var match = System.Text.RegularExpressions.Regex.Match(year, @"\d{4}");
                                        if (match.Success)
                                        {
                                            jikanSearchQuery += $" {match.Value}";
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // 如果有 Bangumi ID，优先通过官方 API 获取最准确的图片 URL
                if (!string.IsNullOrEmpty(bangumiId))
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkService] 尝试通过 Bangumi API 获取真实封面，ID: {bangumiId}");
                        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AniTechou/1.0 (https://github.com/your-repo/AniTechou)");
                        
                        var apiResponse = await client.GetAsync($"https://api.bgm.tv/v0/subjects/{bangumiId}");
                        if (apiResponse.IsSuccessStatusCode)
                        {
                            var jsonString = await apiResponse.Content.ReadAsStringAsync();
                            using (var doc = System.Text.Json.JsonDocument.Parse(jsonString))
                            {
                                var root = doc.RootElement;
                                if (root.TryGetProperty("images", out var images))
                                {
                                    if (images.TryGetProperty("large", out var largeUrl)) targetUrls.Add(largeUrl.GetString());
                                    if (images.TryGetProperty("common", out var commonUrl)) targetUrls.Add(commonUrl.GetString());
                                    if (images.TryGetProperty("medium", out var mediumUrl)) targetUrls.Add(mediumUrl.GetString());
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkService] Bangumi API 异常: {ex.Message}");
                    }
                }

                // 【新增策略】如果 AI 没有给 ID，或者 API 请求失败，我们用标题去搜 Bangumi
                if (targetUrls.Count == 0 && !string.IsNullOrEmpty(jikanSearchQuery))
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkService] 启动 Bangumi 关键词搜索，关键词: {jikanSearchQuery}");
                        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AniTechou/1.0 (https://github.com/your-repo/AniTechou)");
                        
                        // 调用 Bangumi 的搜索 API
                        var searchResponse = await client.GetAsync($"https://api.bgm.tv/search/subject/{Uri.EscapeDataString(jikanSearchQuery)}?type=2&responseGroup=small");
                        if (searchResponse.IsSuccessStatusCode)
                        {
                            var jsonString = await searchResponse.Content.ReadAsStringAsync();
                            using (var doc = System.Text.Json.JsonDocument.Parse(jsonString))
                            {
                                var root = doc.RootElement;
                                if (root.TryGetProperty("list", out var listArray) && listArray.GetArrayLength() > 0)
                                {
                                    var firstResult = listArray[0];
                                    if (firstResult.TryGetProperty("images", out var images))
                                    {
                                        if (images.TryGetProperty("large", out var largeUrl)) targetUrls.Add(largeUrl.GetString());
                                        if (images.TryGetProperty("common", out var commonUrl)) targetUrls.Add(commonUrl.GetString());
                                    }
                                    System.Diagnostics.Debug.WriteLine($"[WorkService] Bangumi 关键词搜索成功，匹配到 ID: {firstResult.GetProperty("id")}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkService] Bangumi 搜索异常: {ex.Message}");
                    }
                }

                // 【核心策略2】如果 Bgm 彻底失败，启动 Jikan API (MyAnimeList) 搜索兜底
                if (targetUrls.Count == 0 && !string.IsNullOrEmpty(jikanSearchQuery))
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkService] 启动 Jikan API (MAL) 备用搜索，关键词: {jikanSearchQuery}");
                        // Jikan 搜索动画
                        var jikanResponse = await client.GetAsync($"https://api.jikan.moe/v4/anime?q={Uri.EscapeDataString(jikanSearchQuery)}&limit=1");
                        if (jikanResponse.IsSuccessStatusCode)
                        {
                            var jsonString = await jikanResponse.Content.ReadAsStringAsync();
                            using (var doc = System.Text.Json.JsonDocument.Parse(jsonString))
                            {
                                var root = doc.RootElement;
                                if (root.TryGetProperty("data", out var dataArray) && dataArray.GetArrayLength() > 0)
                                {
                                    var firstResult = dataArray[0];
                                    if (firstResult.TryGetProperty("images", out var images) && 
                                        images.TryGetProperty("jpg", out var jpgImages) && 
                                        jpgImages.TryGetProperty("large_image_url", out var imgUrl))
                                    {
                                        string malUrl = imgUrl.GetString();
                                        if (!string.IsNullOrEmpty(malUrl))
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[WorkService] 从 MAL 获取到封面: {malUrl}");
                                            targetUrls.Add(malUrl);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkService] Jikan API 搜索异常: {ex.Message}");
                    }
                }

                // 如果上述都失败，退回到对原始 URL 的猜测策略
                if (targetUrls.Count == 0 && !string.IsNullOrEmpty(originalUrl) && originalUrl.Contains("lain.bgm.tv"))
                {
                    string rawUrl = originalUrl;
                    if (rawUrl.Contains("/s/")) { targetUrls.Add(rawUrl.Replace("/s/", "/l/")); targetUrls.Add(rawUrl.Replace("/s/", "/c/")); targetUrls.Add(rawUrl); }
                    else if (rawUrl.Contains("/l/")) { targetUrls.Add(rawUrl); targetUrls.Add(rawUrl.Replace("/l/", "/c/")); targetUrls.Add(rawUrl.Replace("/l/", "/m/")); }
                    else if (rawUrl.Contains("/c/")) { targetUrls.Add(rawUrl.Replace("/c/", "/l/")); targetUrls.Add(rawUrl); }
                    else if (rawUrl.Contains("/m/")) { targetUrls.Add(rawUrl.Replace("/m/", "/l/")); targetUrls.Add(rawUrl.Replace("/m/", "/c/")); targetUrls.Add(rawUrl); }
                    else { targetUrls.Add(rawUrl); }
                }

                targetUrls = targetUrls.Where(u => !string.IsNullOrEmpty(u)).Distinct().ToList();

                foreach (var targetUrl in targetUrls)
                {
                    try
                    {
                        // 发送图片下载请求时，动态调整 Referer
                        if (targetUrl.Contains("bgm.tv"))
                        {
                            client.DefaultRequestHeaders.Remove("Referer");
                            client.DefaultRequestHeaders.Add("Referer", "https://bgm.tv/");
                        }
                        else if (targetUrl.Contains("myanimelist.net"))
                        {
                            client.DefaultRequestHeaders.Remove("Referer");
                            client.DefaultRequestHeaders.Add("Referer", "https://myanimelist.net/");
                        }

                        string appDataDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "AniTechou",
                            "covers"
                        );

                        if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);

                        string extension = ".jpg";
                        try
                        {
                            Uri uri = new Uri(targetUrl);
                            string path = uri.AbsolutePath;
                            if (path.Contains(".")) extension = Path.GetExtension(path);
                        }
                        catch { }

                        string fileName = $"{workId}_{DateTime.Now.Ticks}{extension}";
                        string localPath = Path.Combine(appDataDir, fileName);

                        System.Diagnostics.Debug.WriteLine($"[WorkService] 真正开始下载图片: {targetUrl}");
                        var response = await client.GetAsync(targetUrl);
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WorkService] 下载失败，状态码: {response.StatusCode}");
                            continue; 
                        }

                        // 检查 Content-Type，确保下载的确实是图片而不是防盗链的 HTML 页面
                        if (response.Content.Headers.ContentType != null && !response.Content.Headers.ContentType.MediaType.StartsWith("image/"))
                        {
                            System.Diagnostics.Debug.WriteLine($"[WorkService] 下载的不是图片格式 ({response.Content.Headers.ContentType.MediaType})，可能是防盗链拦截");
                            continue;
                        }

                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        if (bytes.Length < 1024) 
                        {
                            System.Diagnostics.Debug.WriteLine($"[WorkService] 图片文件过小 (<1KB)，可能是占位图或防盗链错误");
                            continue;
                        }

                        await File.WriteAllBytesAsync(localPath, bytes);
                        UpdateCoverPath(workId, localPath);
                        System.Diagnostics.Debug.WriteLine($"[WorkService] 封面保存成功: {localPath}");
                        return localPath;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkService] 下载异常 ({targetUrl}): {ex.Message}");
                        continue; 
                    }
                }
            }

            return "";
        }

        private void UpdateCoverPath(int workId, string localPath)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE Works SET CoverPath = @CoverPath WHERE Id = @Id";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@CoverPath", localPath);
                    cmd.Parameters.AddWithValue("@Id", workId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 作品卡片数据模型
        /// </summary>
        public class WorkCardData
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string OriginalTitle { get; set; } = "";
            public string Type { get; set; } = "";
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
                   string year, string season, string sourceType, string episodesVolumes, string progress,
                   string status, int rating, string synopsis, string coverPath, string author = "", string originalWork = "")
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
                        INSERT INTO Works (Title, OriginalTitle, Type, Company, Year, Season, SourceType, EpisodesVolumes, Synopsis, CoverPath, Author, OriginalWork, AddedTime, LastModified)
                        VALUES (@Title, @OriginalTitle, @Type, @Company, @Year, @Season, @SourceType, @EpisodesVolumes, @Synopsis, @CoverPath, @Author, @OriginalWork, @AddedTime, @LastModified);
                        SELECT last_insert_rowid();";

                            int workId;
                            using (var cmd = new SQLiteCommand(insertWork, conn))
                            {
                                cmd.Parameters.AddWithValue("@Title", title);
                                cmd.Parameters.AddWithValue("@OriginalTitle", originalTitle ?? "");
                                cmd.Parameters.AddWithValue("@Type", type);
                                cmd.Parameters.AddWithValue("@Company", company ?? "");
                                cmd.Parameters.AddWithValue("@Author", author ?? "");
                                cmd.Parameters.AddWithValue("@OriginalWork", originalWork ?? "");
                                cmd.Parameters.AddWithValue("@Year", yearValue);
                                cmd.Parameters.AddWithValue("@Season", season ?? "");
                                cmd.Parameters.AddWithValue("@SourceType", sourceType ?? "原创");
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
                                // Rating 为 0 时存入 NULL（现有数据库约束是 1-10）
                                cmd.Parameters.AddWithValue("@Rating", rating > 0 ? rating : DBNull.Value);
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
                string sql = @"SELECT Id, Title, OriginalTitle, Type, Company, Year, Season, SourceType, EpisodesVolumes, Synopsis, CoverPath, Author, OriginalWork
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
                                SourceType = SafeGetString(reader, 7),
                                EpisodesVolumes = SafeGetString(reader, 8),
                                Synopsis = SafeGetString(reader, 9),
                                CoverPath = SafeGetString(reader, 10),
                                Author = SafeGetString(reader, 11),
                                OriginalWork = SafeGetString(reader, 12)
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
                           string sourceType, string episodesVolumes, string synopsis, string coverPath, string author = "", string originalWork = "")
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
                    Author = @Author,
                    OriginalWork = @OriginalWork,
                    Year = @Year,
                    Season = @Season,
                    SourceType = @SourceType,
                    EpisodesVolumes = @EpisodesVolumes,
                    Synopsis = @Synopsis,
                    CoverPath = @CoverPath,
                    LastModified = @LastModified
                WHERE Id = @Id";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@OriginalTitle", originalTitle ?? "");
                        cmd.Parameters.AddWithValue("@Type", type);
                        cmd.Parameters.AddWithValue("@Company", company ?? "");
                        cmd.Parameters.AddWithValue("@Author", author ?? "");
                        cmd.Parameters.AddWithValue("@OriginalWork", originalWork ?? "");
                        cmd.Parameters.AddWithValue("@Year", year ?? "");
                        cmd.Parameters.AddWithValue("@Season", season ?? "");
                        cmd.Parameters.AddWithValue("@SourceType", sourceType ?? "");
                        cmd.Parameters.AddWithValue("@EpisodesVolumes", episodesVolumes ?? "");
                        cmd.Parameters.AddWithValue("@Synopsis", synopsis ?? "");
                        cmd.Parameters.AddWithValue("@CoverPath", coverPath ?? "");
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

        /// <summary>
        /// 笔记信息
        /// </summary>
        public class NoteInfo
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string Content { get; set; } = "";
            public DateTime CreatedTime { get; set; }
            public DateTime ModifiedTime { get; set; }
            public List<int> WorkIds { get; set; } = new List<int>();
            public List<string> Tags { get; set; } = new List<string>();
        }

        /// <summary>
        /// 笔记列表项
        /// </summary>
        public class NoteListItem
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string Content { get; set; } = "";
            public string Preview { get; set; } = "";
            public DateTime CreatedTime { get; set; }
            public string Tags { get; set; } = "";
            public int WorkCount { get; set; }
            public List<string> WorkTitles { get; set; } = new List<string>();
            public List<int> WorkIds { get; set; } = new List<int>();

            // 显示属性
            public string DisplayTitle { get; set; } = "";
            public string DisplayContent { get; set; } = "";
            public string CreatedTimeDisplay { get; set; } = "";
            public string TagsDisplay { get; set; } = "";
            public bool HasTags { get; set; }
            public string WorkTitlesDisplay { get; set; } = "";
        }

        /// <summary>
        /// 获取所有笔记
        /// </summary>
        public List<NoteListItem> GetAllNotes()
        {
            var notes = new List<NoteListItem>();
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = @"
                    SELECT n.Id, COALESCE(n.Title, '') as Title, n.Content, n.CreatedTime,
                           GROUP_CONCAT(DISTINCT nt.TagName) as Tags,
                           COUNT(DISTINCT nw.WorkId) as WorkCount
                    FROM Notes n
                    LEFT JOIN NoteTags nt ON n.Id = nt.NoteId
                    LEFT JOIN NoteWorks nw ON n.Id = nw.NoteId
                    GROUP BY n.Id
                    ORDER BY n.CreatedTime DESC";

                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string title = SafeGetString(reader, 1);
                        string content = SafeGetString(reader, 2);
                        string preview = content.Length > 100 ? content.Substring(0, 100) + "..." : content;
                        int noteId = SafeGetInt(reader, 0);
                        int workCount = SafeGetInt(reader, 5);

                        // 获取关联作品标题和ID
                        var workTitles = new List<string>();
                        var workIds = new List<int>();
                        if (workCount > 0)
                        {
                            using (var cmdWorks = new SQLiteCommand("SELECT w.Id, w.Title FROM Works w INNER JOIN NoteWorks nw ON w.Id = nw.WorkId WHERE nw.NoteId = @NoteId", conn))
                            {
                                cmdWorks.Parameters.AddWithValue("@NoteId", noteId);
                                using (var readerWorks = cmdWorks.ExecuteReader())
                                {
                                    while (readerWorks.Read())
                                    {
                                        workIds.Add(SafeGetInt(readerWorks, 0));
                                        workTitles.Add(SafeGetString(readerWorks, 1));
                                    }
                                }
                            }
                        }

                        notes.Add(new NoteListItem
                        {
                            Id = noteId,
                            Title = title,
                            Content = content,
                            Preview = preview,
                            CreatedTime = DateTime.Parse(SafeGetString(reader, 3)),
                            Tags = SafeGetString(reader, 4) ?? "",
                            WorkCount = workCount,
                            WorkTitles = workTitles,
                            WorkIds = workIds,
                            DisplayTitle = string.IsNullOrEmpty(title) ? "无标题" : title
                        });
                    }
                }
            }
            return notes;
        }

        /// <summary>
        /// 获取单个笔记
        /// </summary>
        public NoteInfo GetNoteById(int noteId)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();

                // 获取笔记内容
                string sqlNote = "SELECT Id, COALESCE(Title, '') as Title, Content, CreatedTime, ModifiedTime FROM Notes WHERE Id = @Id";
                NoteInfo note = null;
                using (var cmd = new SQLiteCommand(sqlNote, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", noteId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            note = new NoteInfo
                            {
                                Id = SafeGetInt(reader, 0),
                                Title = SafeGetString(reader, 1),
                                Content = SafeGetString(reader, 2),
                                CreatedTime = DateTime.Parse(SafeGetString(reader, 3)),
                                ModifiedTime = DateTime.Parse(SafeGetString(reader, 4))
                            };
                        }
                    }
                }

                if (note == null) return null;

                // 获取关联的作品
                string sqlWorks = "SELECT WorkId FROM NoteWorks WHERE NoteId = @NoteId";
                using (var cmd = new SQLiteCommand(sqlWorks, conn))
                {
                    cmd.Parameters.AddWithValue("@NoteId", noteId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            note.WorkIds.Add(SafeGetInt(reader, 0));
                        }
                    }
                }

                // 获取标签
                string sqlTags = "SELECT TagName FROM NoteTags WHERE NoteId = @NoteId";
                using (var cmd = new SQLiteCommand(sqlTags, conn))
                {
                    cmd.Parameters.AddWithValue("@NoteId", noteId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            note.Tags.Add(SafeGetString(reader, 0));
                        }
                    }
                }

                return note;
            }
        }

        /// <summary>
        /// 保存笔记（新增或更新）
        /// </summary>
        public int SaveNote(NoteInfo note)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        int noteId;

                        if (note.Id == 0)
                        {
                            // 新增
                            string insertSql = @"
                        INSERT INTO Notes (Title, Content, CreatedTime, ModifiedTime)
                        VALUES (@Title, @Content, @CreatedTime, @ModifiedTime);
                        SELECT last_insert_rowid();";
                            using (var cmd = new SQLiteCommand(insertSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@Title", note.Title ?? "");
                                cmd.Parameters.AddWithValue("@Content", note.Content);
                                cmd.Parameters.AddWithValue("@CreatedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@ModifiedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                noteId = Convert.ToInt32(cmd.ExecuteScalar());
                            }
                        }
                        else
                        {
                            // 更新
                            string updateSql = @"
                        UPDATE Notes SET Title = @Title, Content = @Content, ModifiedTime = @ModifiedTime WHERE Id = @Id";
                            using (var cmd = new SQLiteCommand(updateSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@Title", note.Title ?? "");
                                cmd.Parameters.AddWithValue("@Content", note.Content);
                                cmd.Parameters.AddWithValue("@ModifiedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@Id", note.Id);
                                cmd.ExecuteNonQuery();
                            }
                            noteId = note.Id;

                            // 删除旧的关联和标签
                            using (var cmd = new SQLiteCommand("DELETE FROM NoteWorks WHERE NoteId = @NoteId", conn))
                            {
                                cmd.Parameters.AddWithValue("@NoteId", noteId);
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = new SQLiteCommand("DELETE FROM NoteTags WHERE NoteId = @NoteId", conn))
                            {
                                cmd.Parameters.AddWithValue("@NoteId", noteId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 插入作品关联
                        foreach (int workId in note.WorkIds)
                        {
                            using (var cmd = new SQLiteCommand("INSERT INTO NoteWorks (NoteId, WorkId) VALUES (@NoteId, @WorkId)", conn))
                            {
                                cmd.Parameters.AddWithValue("@NoteId", noteId);
                                cmd.Parameters.AddWithValue("@WorkId", workId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 插入标签
                        foreach (string tag in note.Tags)
                        {
                            if (!string.IsNullOrWhiteSpace(tag))
                            {
                                using (var cmd = new SQLiteCommand("INSERT INTO NoteTags (NoteId, TagName) VALUES (@NoteId, @TagName)", conn))
                                {
                                    cmd.Parameters.AddWithValue("@NoteId", noteId);
                                    cmd.Parameters.AddWithValue("@TagName", tag.Trim());
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return noteId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        return 0;
                    }
                }
            }
        }

        /// <summary>
        /// 删除笔记
        /// </summary>
        public bool DeleteNote(int noteId)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "DELETE FROM Notes WHERE Id = @Id";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", noteId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>
        /// 删除作品
        /// </summary>
        public bool DeleteWork(int workId)
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
                            // 1. 获取封面路径（用于后续删除文件）
                            string coverPath = "";
                            using (var cmd = new SQLiteCommand("SELECT CoverPath FROM Works WHERE Id = @Id", conn))
                            {
                                cmd.Parameters.AddWithValue("@Id", workId);
                                coverPath = cmd.ExecuteScalar()?.ToString();
                            }

                            // 2. 删除关联数据（外键级联删除应该已经处理了部分，但这里手动处理更稳妥）
                            new SQLiteCommand("DELETE FROM UserList WHERE WorkId = " + workId, conn).ExecuteNonQuery();
                            new SQLiteCommand("DELETE FROM WorkTags WHERE WorkId = " + workId, conn).ExecuteNonQuery();
                            new SQLiteCommand("DELETE FROM NoteWorks WHERE WorkId = " + workId, conn).ExecuteNonQuery();
                            
                            // 3. 删除作品主表
                            using (var cmd = new SQLiteCommand("DELETE FROM Works WHERE Id = @Id", conn))
                            {
                                cmd.Parameters.AddWithValue("@Id", workId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            // 4. 尝试删除本地封面文件
                            if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
                            {
                                try { File.Delete(coverPath); } catch { }
                            }

                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            return false;
                        }
                    }
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// 获取作品列表（用于选择关联作品）
        /// </summary>
        public List<WorkListItem> GetWorksForSelection()
        {
            var works = new List<WorkListItem>();
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "SELECT Id, Title FROM Works ORDER BY Title";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        works.Add(new WorkListItem
                        {
                            Id = SafeGetInt(reader, 0),
                            Title = SafeGetString(reader, 1)
                        });
                    }
                }
            }
            return works;
        }

        /// <summary>
        /// 作品列表项
        /// </summary>
        public class WorkListItem
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
        }

        /// <summary>
        /// 个人资料统计
        /// </summary>
        public class ProfileStats
        {
            public int AnimeCount { get; set; }
            public int MangaCount { get; set; }
            public int GameCount { get; set; }
            public int TotalWorks { get; set; }
            public int TotalNotes { get; set; }
            public Dictionary<int, int> YearStats { get; set; } = new Dictionary<int, int>();
            public Dictionary<string, int> TagStats { get; set; } = new Dictionary<string, int>();
        }

        /// <summary>
        /// 获取个人统计数据
        /// </summary>
        public ProfileStats GetStats()
        {
            var stats = new ProfileStats();

            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();

                // 统计各类型作品数量
                string typeSql = @"
                    SELECT w.Type, COUNT(*) as Count
                    FROM Works w
                    INNER JOIN UserList ul ON w.Id = ul.WorkId
                    GROUP BY w.Type";
                using (var cmd = new SQLiteCommand(typeSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string type = SafeGetString(reader, 0);
                        int count = SafeGetInt(reader, 1);
                        switch (type)
                        {
                            case "Anime":
                                stats.AnimeCount = count;
                                break;
                            case "Manga":
                                stats.MangaCount = count;
                                break;
                            case "LightNovel":
                                // 可以添加轻小说统计
                                break;
                            case "Game":
                                stats.GameCount = count;
                                break;
                        }
                    }
                }

                stats.TotalWorks = stats.AnimeCount + stats.MangaCount + stats.GameCount;

                // 统计笔记数量
                string noteCountSql = "SELECT COUNT(*) FROM Notes";
                using (var cmd = new SQLiteCommand(noteCountSql, conn))
                {
                    stats.TotalNotes = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // 年度统计
                string yearSql = @"
                    SELECT w.Year, COUNT(*) as Count
                    FROM Works w
                    INNER JOIN UserList ul ON w.Id = ul.WorkId
                    WHERE w.Year IS NOT NULL AND w.Year != ''
                    GROUP BY w.Year
                    ORDER BY w.Year DESC";
                using (var cmd = new SQLiteCommand(yearSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string yearStr = SafeGetString(reader, 0);
                        int count = SafeGetInt(reader, 1);
                        if (int.TryParse(yearStr, out int year) && year > 0)
                        {
                            stats.YearStats[year] = count;
                        }
                    }
                }

                // 标签统计
                string tagSql = @"
                    SELECT TagName, COUNT(*) as Count
                    FROM WorkTags
                    GROUP BY TagName
                    ORDER BY Count DESC
                    LIMIT 50";
                using (var cmd = new SQLiteCommand(tagSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tag = SafeGetString(reader, 0);
                        int count = SafeGetInt(reader, 1);
                        stats.TagStats[tag] = count;
                    }
                }
            }

            return stats;
        }

        /// <summary>
        /// 获取账户信息
        /// </summary>
        public AccountInfo GetAccountInfo()
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "SELECT Username, Nickname, CreatedTime FROM Accounts WHERE Username = @Username";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Username", _currentAccount);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new AccountInfo
                            {
                                Username = SafeGetString(reader, 0),
                                Nickname = SafeGetString(reader, 1),
                                CreatedTime = DateTime.Parse(SafeGetString(reader, 2))
                            };
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 更新昵称
        /// </summary>
        public bool UpdateNickname(string nickname)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                {
                    conn.Open();

                    // 创建 AccountInfo 表（如果不存在）
                    string createTable = @"
                        CREATE TABLE IF NOT EXISTS AccountInfo (
                            Id INTEGER PRIMARY KEY,
                            Nickname TEXT,
                            AvatarPath TEXT
                        )";
                    using (var cmd = new SQLiteCommand(createTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // 插入或更新昵称
                    string sql = @"
                        INSERT OR REPLACE INTO AccountInfo (Id, Nickname)
                        VALUES (1, @Nickname)";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Nickname", nickname);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateNickname error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取昵称
        /// </summary>
        public string GetNickname()
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                {
                    conn.Open();
                    string sql = "SELECT Nickname FROM AccountInfo WHERE Id = 1";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        return result?.ToString() ?? "";
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 账户信息
        /// </summary>
        public class AccountInfo
        {
            public string Username { get; set; } = "";
            public string Nickname { get; set; } = "";
            public DateTime CreatedTime { get; set; }
        }

        /// <summary>
        /// 导出所有数据
        /// </summary>
        public string ExportAllData()
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();

                var data = new
                {
                    Works = GetWorksForExport(conn),
                    Notes = GetNotesForExport(conn),
                    ExportTime = DateTime.Now
                };

                return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
        }

        private List<object> GetWorksForExport(SQLiteConnection conn)
        {
            var works = new List<object>();
            string sql = "SELECT Id, Title, OriginalTitle, Type, Company, Year, Season, SourceType, EpisodesVolumes, Synopsis, CoverPath, AddedTime FROM Works";
            using (var cmd = new SQLiteCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    works.Add(new
                    {
                        Id = SafeGetInt(reader, 0),
                        Title = SafeGetString(reader, 1),
                        OriginalTitle = SafeGetString(reader, 2),
                        Type = SafeGetString(reader, 3),
                        Company = SafeGetString(reader, 4),
                        Year = SafeGetString(reader, 5),
                        Season = SafeGetString(reader, 6),
                        SourceType = SafeGetString(reader, 7),
                        EpisodesVolumes = SafeGetString(reader, 8),
                        Synopsis = SafeGetString(reader, 9),
                        CoverPath = SafeGetString(reader, 10),
                        AddedTime = SafeGetString(reader, 11)
                    });
                }
            }
            return works;
        }

        private List<object> GetNotesForExport(SQLiteConnection conn)
        {
            var notes = new List<object>();
            string sql = "SELECT Id, Title, Content, CreatedTime, ModifiedTime FROM Notes";
            using (var cmd = new SQLiteCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    notes.Add(new
                    {
                        Id = SafeGetInt(reader, 0),
                        Title = SafeGetString(reader, 1),
                        Content = SafeGetString(reader, 2),
                        CreatedTime = SafeGetString(reader, 3),
                        ModifiedTime = SafeGetString(reader, 4)
                    });
                }
            }
            return notes;
        }
    }
}