using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AniTechou.Models;
using AniTechou.Utilities;

namespace AniTechou.Services
{
    /// <summary>
    /// 作品数据服务
    /// </summary>
    public partial class WorkService
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
                        double minR = 0, maxR = 10;
                        switch (rating)
                        {
                            case "一般 (1-4)": minR = 10; maxR = 39; break;
                            case "还行 (5-6)": minR = 50; maxR = 69; break;
                            case "佳作 (7-8)": minR = 70; maxR = 89; break;
                            case "神作 (9-10)": minR = 90; maxR = 100; break;
                        }
                        sql += " AND ul.Rating >= @MinRating AND ul.Rating <= @MaxRating";
                        parameters.Add(new SQLiteParameter("@MinRating", minR));
                        parameters.Add(new SQLiteParameter("@MaxRating", maxR));
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
                                    RatingDisplay = GetRatingDisplay(SafeGetDouble(reader, 7))
                                });
                            }
                        }
                    }
                }

                return works;
            });
        }

        /// <summary>
        /// 分页获取作品列表
        /// </summary>
        public async Task<List<WorkCardData>> GetWorksPaginatedAsync(string type, string status, string year, string season,
                                                     string sourceType, string studio, string rating, List<string> tags,
                                                     int offset, int limit)
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

                    if (type != "all")
                    {
                        sql += " AND w.Type = @Type";
                        parameters.Add(new SQLiteParameter("@Type", type));
                    }
                    if (status != "all")
                    {
                        sql += " AND ul.Status = @Status";
                        parameters.Add(new SQLiteParameter("@Status", status));
                    }
                    if (year != "全部年份" && !string.IsNullOrEmpty(year))
                    {
                        sql += " AND w.Year LIKE @Year";
                        parameters.Add(new SQLiteParameter("@Year", $"{year}%"));
                    }
                    if (season != "全部季节" && !string.IsNullOrEmpty(season))
                    {
                        sql += " AND w.Season = @Season";
                        parameters.Add(new SQLiteParameter("@Season", season));
                    }
                    if (sourceType != "全部原作" && !string.IsNullOrEmpty(sourceType))
                    {
                        sql += " AND w.SourceType = @SourceType";
                        parameters.Add(new SQLiteParameter("@SourceType", sourceType));
                    }
                    if (studio != "全部制作" && !string.IsNullOrEmpty(studio))
                    {
                        sql += " AND w.Company = @Studio";
                        parameters.Add(new SQLiteParameter("@Studio", studio));
                    }
                    if (rating != "全部评分" && !string.IsNullOrEmpty(rating))
                    {
                        double minR = 0, maxR = 10;
                        switch (rating)
                        {
                            case "一般 (1-4)": minR = 10; maxR = 39; break;
                            case "还行 (5-6)": minR = 50; maxR = 69; break;
                            case "佳作 (7-8)": minR = 70; maxR = 89; break;
                            case "神作 (9-10)": minR = 90; maxR = 100; break;
                        }
                        sql += " AND ul.Rating >= @MinRating AND ul.Rating <= @MaxRating";
                        parameters.Add(new SQLiteParameter("@MinRating", minR));
                        parameters.Add(new SQLiteParameter("@MaxRating", maxR));
                    }
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

                    sql += " ORDER BY ul.LastUpdated DESC LIMIT @Limit OFFSET @Offset";
                    parameters.Add(new SQLiteParameter("@Limit", limit));
                    parameters.Add(new SQLiteParameter("@Offset", offset));

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
                                    Type = SafeGetString(reader, 9),
                                    Info = $"{SafeGetString(reader, 3)} · {displayMaker}",
                                    CoverPath = SafeGetString(reader, 5),
                                    ProgressValue = ParseProgressToValue(SafeGetString(reader, 6)),
                                    ProgressText = SafeGetString(reader, 6) ?? "未开始",
                                    RatingDisplay = GetRatingDisplay(SafeGetDouble(reader, 7))
                                });
                            }
                        }
                    }
                }

                return works;
            });
        }

        /// <summary>
        /// 获取符合条件的作品总数(不含分页)
        /// </summary>
        public int GetWorksCountAsync(string type, string status, string year, string season,
            string sourceType, string studio, string rating, List<string> tags)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();

                string sql = @"
                    SELECT COUNT(*)
                    FROM Works w
                    INNER JOIN UserList ul ON w.Id = ul.WorkId
                    WHERE 1=1";

                var parameters = new List<SQLiteParameter>();

                if (type != "all")
                {
                    sql += " AND w.Type = @Type";
                    parameters.Add(new SQLiteParameter("@Type", type));
                }
                if (status != "all")
                {
                    sql += " AND ul.Status = @Status";
                    parameters.Add(new SQLiteParameter("@Status", status));
                }
                if (year != "全部年份" && !string.IsNullOrEmpty(year))
                {
                    sql += " AND w.Year LIKE @Year";
                    parameters.Add(new SQLiteParameter("@Year", $"{year}%"));
                }
                if (season != "全部季节" && !string.IsNullOrEmpty(season))
                {
                    sql += " AND w.Season = @Season";
                    parameters.Add(new SQLiteParameter("@Season", season));
                }
                if (sourceType != "全部原作" && !string.IsNullOrEmpty(sourceType))
                {
                    sql += " AND w.SourceType = @SourceType";
                    parameters.Add(new SQLiteParameter("@SourceType", sourceType));
                }
                if (studio != "全部制作" && !string.IsNullOrEmpty(studio))
                {
                    sql += " AND w.Company = @Studio";
                    parameters.Add(new SQLiteParameter("@Studio", studio));
                }
                if (rating != "全部评分" && !string.IsNullOrEmpty(rating))
                {
                    double minR = 0, maxR = 10;
                    switch (rating)
                    {
                        case "一般 (1-4)": minR = 10; maxR = 39; break;
                        case "还行 (5-6)": minR = 50; maxR = 69; break;
                        case "佳作 (7-8)": minR = 70; maxR = 89; break;
                        case "神作 (9-10)": minR = 90; maxR = 100; break;
                    }
                    sql += " AND ul.Rating >= @MinRating AND ul.Rating <= @MaxRating";
                    parameters.Add(new SQLiteParameter("@MinRating", minR));
                    parameters.Add(new SQLiteParameter("@MaxRating", maxR));
                }
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

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddRange(parameters.ToArray());
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
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
                string sql = "SELECT DISTINCT TagName FROM WorkTags WHERE WorkId = @WorkId ORDER BY TagName";
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
                string sql = @"SELECT w.Id, w.Title, w.OriginalTitle, w.Year, w.Company, w.CoverPath, ul.Progress, ul.Rating, w.Author, w.Type, w.BangumiId
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
                                BangumiId = SafeGetString(reader, 10),
                                ProgressValue = ParseProgressToValue(SafeGetString(reader, 6)),
                                ProgressText = SafeGetString(reader, 6) ?? "未开始",
                                RatingDisplay = GetRatingDisplay(SafeGetDouble(reader, 7))
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
                                    RatingDisplay = GetRatingDisplay(SafeGetDouble(reader, 7))
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
                                BangumiId = "",
                                ProgressValue = ParseProgressToValue(SafeGetString(reader, 6)),
                                ProgressText = SafeGetString(reader, 6) ?? "未开始",
                                RatingDisplay = GetRatingDisplay(SafeGetDouble(reader, 7))
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

        public bool UpdateWorkRating(int workId, double rating)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE UserList SET Rating = @Rating, LastUpdated = @Now WHERE WorkId = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.Add("@Rating", System.Data.DbType.Double).Value = rating > 0 ? (object)(int)(rating*10) : DBNull.Value;
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

        /// <summary>
        /// 第2层改进：更新作品的 Bangumi ID
        /// </summary>
        public bool UpdateWorkBangumiId(int workId, string bangumiId)
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                string sql = "UPDATE Works SET BangumiId = @BangumiId, LastModified = @Now WHERE Id = @WorkId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@BangumiId", bangumiId ?? "");
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
        private static string GetRatingDisplay(double rawRating)
        {
            double r = rawRating / 10.0; if (r <= 0) return "-";
            return r.ToString("F1");
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
        private double SafeGetDouble(SQLiteDataReader reader, int index)
        {
            var value = reader.GetValue(index);
            if (value == null || Convert.IsDBNull(value)) return 0;
            if (double.TryParse(value.ToString(), out double r)) return r;
            return 0;
        }

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
            public string BangumiId { get; set; } = "";
            public double ProgressValue { get; set; }
            public string ProgressText { get; set; } = "";
            public string RatingDisplay { get; set; } = "";
        }

        /// <summary>
        /// 添加新作品
        /// </summary>
        public int AddWork(string title, string originalTitle, string type, string company,
                   string year, string season, string sourceType, string episodesVolumes, string progress,
                   string status, double rating, string synopsis, string coverPath, string author = "", string originalWork = "",
                   string bangumiId = "", string malId = "", string anilistId = "", string voiceActorInfo = "",
                   string startedDate = "", string finishedDate = "")
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
                        INSERT INTO Works (Title, OriginalTitle, Type, Company, Year, Season, SourceType, EpisodesVolumes, Synopsis, CoverPath, Author, OriginalWork, BangumiId, MALId, AniListId, VoiceActorInfo, AddedTime, LastModified)
                        VALUES (@Title, @OriginalTitle, @Type, @Company, @Year, @Season, @SourceType, @EpisodesVolumes, @Synopsis, @CoverPath, @Author, @OriginalWork, @BangumiId, @MALId, @AniListId, @VoiceActorInfo, @AddedTime, @LastModified);
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
                                cmd.Parameters.AddWithValue("@BangumiId", bangumiId ?? "");
                                cmd.Parameters.AddWithValue("@MALId", malId ?? "");
                                cmd.Parameters.AddWithValue("@AniListId", anilistId ?? "");
                                cmd.Parameters.AddWithValue("@VoiceActorInfo", voiceActorInfo ?? "");
                                cmd.Parameters.AddWithValue("@AddedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@LastModified", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                workId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // 插入用户列表
                            string insertList = @"
                        INSERT INTO UserList (WorkId, Status, Progress, Rating, StartedDate, FinishedDate, LastUpdated)
                        VALUES (@WorkId, @Status, @Progress, @Rating, @StartedDate, @FinishedDate, @LastUpdated)";

                            using (var cmd = new SQLiteCommand(insertList, conn))
                            {
                                cmd.Parameters.AddWithValue("@WorkId", workId);
                                cmd.Parameters.AddWithValue("@Status", status);
                                cmd.Parameters.AddWithValue("@Progress", progress ?? "");
                                // Rating 为 0 时存入 NULL（现有数据库约束是 1-10）
                                cmd.Parameters.Add("@Rating", System.Data.DbType.Double).Value = rating > 0 ? (object)(int)(rating*10) : DBNull.Value;
                                cmd.Parameters.AddWithValue("@StartedDate", string.IsNullOrEmpty(startedDate) ? DBNull.Value : startedDate);
                                cmd.Parameters.AddWithValue("@FinishedDate", string.IsNullOrEmpty(finishedDate) ? DBNull.Value : finishedDate);
                                cmd.Parameters.AddWithValue("@LastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            // Auto-fetch Bangumi details if BangumiId is provided
                            if (!string.IsNullOrEmpty(bangumiId))
                            {
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        var bgmProvider = new SearchProviders.BangumiSearchProvider();
                                        var detail = await bgmProvider.GetByIdAsync(bangumiId);
                                        if (detail != null)
                                        {
                                            // 标签
                                            if (detail.Tags != null)
                                            {
                                                foreach (var tag in detail.Tags)
                                                {
                                                    if (!string.IsNullOrWhiteSpace(tag))
                                                        AddWorkTag(workId, tag, "Bangumi");
                                                }
                                            }
                                            // 补全空字段
                                            if (string.IsNullOrEmpty(company) && !string.IsNullOrEmpty(detail.Company))
                                                UpdateWorkCompany(workId, detail.Company);
                                            if (string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(detail.Author))
                                                UpdateWorkAuthor(workId, detail.Author);
                                            if (string.IsNullOrEmpty(originalWork) && !string.IsNullOrEmpty(detail.OriginalWork))
                                                UpdateWorkOriginalWork(workId, detail.OriginalWork);
                                            if (string.IsNullOrEmpty(season) && !string.IsNullOrEmpty(detail.Season))
                                                UpdateWorkSeason(workId, detail.Season);
                                            if ((string.IsNullOrEmpty(sourceType) || sourceType == "原创") && !string.IsNullOrEmpty(detail.SourceType))
                                                UpdateWorkSourceType(workId, detail.SourceType);
                                            if (string.IsNullOrEmpty(episodesVolumes) && !string.IsNullOrEmpty(detail.Episodes))
                                                UpdateWorkEpisodes(workId, detail.Episodes);
                                            if (string.IsNullOrEmpty(synopsis) && !string.IsNullOrEmpty(detail.Synopsis))
                                                UpdateWorkSynopsis(workId, detail.Synopsis);
                                            // 封面
                                            if (string.IsNullOrEmpty(coverPath) && !string.IsNullOrEmpty(detail.CoverUrl))
                                                _ = DownloadAndSaveCoverAsync(
                                                    $"bgm_id:{bangumiId}|{detail.CoverUrl}", workId);
                                        }
                                    }
                                    catch { /* best-effort */ }
                                });
                            }

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
                string sql = @"SELECT Id, Title, OriginalTitle, Type, Company, Year, Season, SourceType, EpisodesVolumes, Synopsis, CoverPath, Author, OriginalWork, BangumiId, MALId, AniListId, VoiceActorInfo
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
                                OriginalWork = SafeGetString(reader, 12),
                                BangumiId = SafeGetString(reader, 13),
                                MALId = SafeGetString(reader, 14),
                                AniListId = SafeGetString(reader, 15),
                                VoiceActorInfo = SafeGetString(reader, 16)
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
                string sql = "SELECT Id, WorkId, Status, Progress, Rating, COALESCE(StartedDate,''), COALESCE(FinishedDate,'') FROM UserList WHERE WorkId = @WorkId";
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
                                Rating = SafeGetDouble(reader, 4) / 10.0,
                                StartedDate = SafeGetString(reader, 5),
                                FinishedDate = SafeGetString(reader, 6)
                            };
                        }
                    }
                }
            }
            return null;
        }

        public void UpdateUserWork(int userListId, string status, string progress, double rating,
            string startedDate = null, string finishedDate = null)
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
                    StartedDate = @StartedDate,
                    FinishedDate = @FinishedDate,
                    LastUpdated = @LastUpdated
                WHERE Id = @Id";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Status", status);
                        cmd.Parameters.AddWithValue("@Progress", progress);
                        System.Diagnostics.Debug.WriteLine($"[Rating] UpdateUserWork 存储: rating={rating}");
                        cmd.Parameters.Add("@Rating", System.Data.DbType.Double).Value = rating > 0 ? (object)(int)(rating*10) : DBNull.Value;
                        cmd.Parameters.AddWithValue("@StartedDate", string.IsNullOrEmpty(startedDate) ? DBNull.Value : startedDate);
                        cmd.Parameters.AddWithValue("@FinishedDate", string.IsNullOrEmpty(finishedDate) ? DBNull.Value : finishedDate);
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
            public string ContentType { get; set; } = "Xaml";
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
            public string ContentType { get; set; } = "Xaml";
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
        /// 从 XAML / Markdown / 纯文本中提取预览纯文本
        /// </summary>
        private string ExtractPreviewText(string content, int maxLength = 100, string contentType = "Xaml")
        {
            if (string.IsNullOrWhiteSpace(content)) return "";
            string text = content;

            if (contentType == "Markdown")
            {
                try
                {
                    text = Markdig.Markdown.ToPlainText(content);
                }
                catch
                {
                    text = content;
                }
            }
            else if (content.TrimStart().StartsWith("<FlowDocument", StringComparison.OrdinalIgnoreCase) ||
                     content.TrimStart().StartsWith("<Section", StringComparison.OrdinalIgnoreCase) ||
                     content.TrimStart().StartsWith("<Span", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var document = new System.Windows.Documents.FlowDocument();
                    var range = new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd);
                    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
                    range.Load(stream, System.Windows.DataFormats.Xaml);
                    text = range.Text;
                }
                catch
                {
                    text = System.Text.RegularExpressions.Regex.Replace(content, "<.*?>", string.Empty);
                }
            }

            text = text.Trim();
            if (text.Length > maxLength)
            {
                return text.Substring(0, maxLength) + "...";
            }
            return text;
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
                    SELECT n.Id, COALESCE(n.Title, '') as Title, n.Content,
                           COALESCE(n.ContentType, 'Xaml') as ContentType,
                           n.CreatedTime,
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
                        string contentType = SafeGetString(reader, 3);
                        string preview = ExtractPreviewText(content, 100, contentType);
                        int noteId = SafeGetInt(reader, 0);
                        int workCount = SafeGetInt(reader, 6);

                        // 暂存基础数据，统一批量查询关联作品
                        notes.Add(new NoteListItem
                        {
                            Id = noteId,
                            Title = title,
                            Content = content,
                            ContentType = contentType,
                            Preview = preview,
                            CreatedTime = DateTime.Parse(SafeGetString(reader, 4)),
                            Tags = SafeGetString(reader, 5) ?? "",
                            WorkCount = workCount,
                            WorkTitles = new List<string>(),
                            WorkIds = new List<int>(),
                            DisplayTitle = string.IsNullOrEmpty(title) ? "无标题" : title
                        });
                    }
                }

                // 一次查询批量获取所有笔记的关联作品标题（避免 N+1）
                if (notes.Count > 0)
                {
                    var noteIds = notes.Select(n => n.Id).ToList();
                    var idList = string.Join(",", noteIds);
                    string workBatchSql = $@"
                        SELECT nw.NoteId, w.Id, w.Title
                        FROM NoteWorks nw
                        INNER JOIN Works w ON nw.WorkId = w.Id
                        WHERE nw.NoteId IN ({idList})";

                    using (var cmdBatch = new SQLiteCommand(workBatchSql, conn))
                    using (var readerBatch = cmdBatch.ExecuteReader())
                    {
                        // 构建 noteId → (ids, titles) 字典
                        var workMap = new Dictionary<int, (List<int> ids, List<string> titles)>();
                        while (readerBatch.Read())
                        {
                            int nid = SafeGetInt(readerBatch, 0);
                            int wid = SafeGetInt(readerBatch, 1);
                            string wtitle = SafeGetString(readerBatch, 2);

                            if (!workMap.ContainsKey(nid))
                                workMap[nid] = (new List<int>(), new List<string>());
                            workMap[nid].ids.Add(wid);
                            workMap[nid].titles.Add(wtitle);
                        }

                        // 回填到 note 对象
                        foreach (var note in notes)
                        {
                            if (workMap.TryGetValue(note.Id, out var works))
                            {
                                note.WorkIds = works.ids;
                                note.WorkTitles = works.titles;
                            }
                        }
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
                string sqlNote = "SELECT Id, COALESCE(Title, '') as Title, Content, COALESCE(ContentType, 'Xaml') as ContentType, CreatedTime, ModifiedTime FROM Notes WHERE Id = @Id";
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
                                ContentType = SafeGetString(reader, 3),
                                CreatedTime = DateTime.Parse(SafeGetString(reader, 4)),
                                ModifiedTime = DateTime.Parse(SafeGetString(reader, 5))
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
                        INSERT INTO Notes (Title, Content, ContentType, CreatedTime, ModifiedTime)
                        VALUES (@Title, @Content, @ContentType, @CreatedTime, @ModifiedTime);
                        SELECT last_insert_rowid();";
                            using (var cmd = new SQLiteCommand(insertSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@Title", note.Title ?? "");
                                cmd.Parameters.AddWithValue("@Content", note.Content);
                                cmd.Parameters.AddWithValue("@ContentType", string.IsNullOrEmpty(note.ContentType) ? "Xaml" : note.ContentType);
                                cmd.Parameters.AddWithValue("@CreatedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@ModifiedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                noteId = Convert.ToInt32(cmd.ExecuteScalar());
                            }
                        }
                        else
                        {
                            // 更新
                            string updateSql = @"
                        UPDATE Notes SET Title = @Title, Content = @Content, ContentType = @ContentType, ModifiedTime = @ModifiedTime WHERE Id = @Id";
                            using (var cmd = new SQLiteCommand(updateSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@Title", note.Title ?? "");
                                cmd.Parameters.AddWithValue("@Content", note.Content);
                                cmd.Parameters.AddWithValue("@ContentType", string.IsNullOrEmpty(note.ContentType) ? "Xaml" : note.ContentType);
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
            public int LightNovelCount { get; set; }
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
                                stats.LightNovelCount = count;
                                break;
                            case "Game":
                                stats.GameCount = count;
                                break;
                        }
                    }
                }

                stats.TotalWorks = stats.AnimeCount + stats.MangaCount + stats.LightNovelCount + stats.GameCount;

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

                // 确保 AccountInfo 表存在
                using (var createCmd = new SQLiteCommand(
                    "CREATE TABLE IF NOT EXISTS AccountInfo (Id INTEGER PRIMARY KEY, Nickname TEXT, CreatedTime TEXT)", conn))
                {
                    createCmd.ExecuteNonQuery();
                }

                string sql = "SELECT Nickname, COALESCE(CreatedTime, '') FROM AccountInfo WHERE Id = 1";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string createdStr = SafeGetString(reader, 1);
                            return new AccountInfo
                            {
                                Username = _currentAccount,
                                Nickname = SafeGetString(reader, 0),
                                CreatedTime = DateTime.TryParse(createdStr, out var dt) ? dt : DateTime.Now
                            };
                        }
                    }
                }

                // 首次使用，插入默认行
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                using (var insertCmd = new SQLiteCommand(
                    "INSERT INTO AccountInfo (Id, Nickname, CreatedTime) VALUES (1, @Nick, @Time)", conn))
                {
                    insertCmd.Parameters.AddWithValue("@Nick", _currentAccount);
                    insertCmd.Parameters.AddWithValue("@Time", now);
                    insertCmd.ExecuteNonQuery();
                }

                return new AccountInfo
                {
                    Username = _currentAccount,
                    Nickname = _currentAccount,
                    CreatedTime = DateTime.Now
                };
            }
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
                            AvatarPath TEXT,
                            CreatedTime TEXT
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

    }
}
