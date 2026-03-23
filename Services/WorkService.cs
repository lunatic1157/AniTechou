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

            // 显示属性
            public string DisplayTitle => string.IsNullOrEmpty(Title) ? "无标题" : Title;
            public string DisplayContent => string.IsNullOrEmpty(Content) ? "" :
                (Content.Length > 80 ? Content.Substring(0, 80) + "..." : Content);
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

                        // 获取关联作品标题
                        var workTitles = new List<string>();
                        if (workCount > 0)
                        {
                            using (var cmdWorks = new SQLiteCommand("SELECT w.Title FROM Works w INNER JOIN NoteWorks nw ON w.Id = nw.WorkId WHERE nw.NoteId = @NoteId", conn))
                            {
                                cmdWorks.Parameters.AddWithValue("@NoteId", noteId);
                                using (var readerWorks = cmdWorks.ExecuteReader())
                                {
                                    while (readerWorks.Read())
                                    {
                                        workTitles.Add(SafeGetString(readerWorks, 0));
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
                            WorkTitles = workTitles
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
    }
}