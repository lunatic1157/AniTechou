using System;
using System.Data.SQLite;
using System.IO;

namespace AniTechou.Services
{
    /// <summary>
    /// 数据库辅助类 - 负责数据库的创建、初始化和连接
    /// </summary>
    public static class DatabaseHelper
    {
        /// <summary>
        /// 获取当前账号的数据库文件路径
        /// </summary>
        /// <param name="accountName">账号名</param>
        /// <returns>完整的数据库文件路径</returns>
        public static string GetDatabasePath(string accountName)
        {
            // 数据库存放位置：%AppData%/AniTechou/accounts/用户名.db
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AniTechou",
                "accounts"
            );

            // 确保目录存在
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }

            return Path.Combine(appDataDir, $"{accountName}.db");
        }

        /// <summary>
        /// 获取当前账号的数据库连接
        /// </summary>
        /// <param name="accountName">账号名</param>
        /// <returns>SQLite数据库连接对象</returns>
        public static SQLiteConnection GetConnection(string accountName)
        {
            string dbPath = GetDatabasePath(accountName);
            return new SQLiteConnection($"Data Source={dbPath};Version=3;");
        }

        /// <summary>
        /// 创建新的数据库文件并初始化所有表
        /// </summary>
        /// <param name="dbPath">数据库文件完整路径</param>
        public static void CreateNewDatabase(string dbPath)
        {
            try
            {
                // 创建数据库文件
                SQLiteConnection.CreateFile(dbPath);

                using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    conn.Open();

                    // 创建所有表的SQL语句
                    string createTablesSql = @"
                        -- 作品表
                        CREATE TABLE Works (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Title TEXT NOT NULL,
                            OriginalTitle TEXT,
                            Type TEXT CHECK(Type IN ('Anime','Manga','LightNovel','Game')),
                            Company TEXT,
                            Year INTEGER,
                            Season TEXT,
                            EpisodesVolumes TEXT,
                            Synopsis TEXT,
                            CoverPath TEXT,
                            AddedTime DATETIME DEFAULT CURRENT_TIMESTAMP,
                            LastModified DATETIME DEFAULT CURRENT_TIMESTAMP
                        );

                        -- 用户列表状态表
                        CREATE TABLE UserList (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            WorkId INTEGER NOT NULL,
                            Status TEXT CHECK(Status IN ('wish','doing','done')) DEFAULT 'wish',
                            Progress TEXT,
                            Rating INTEGER CHECK(Rating BETWEEN 1 AND 10),
                            StartedDate DATETIME,
                            FinishedDate DATETIME,
                            LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP,
                            FOREIGN KEY(WorkId) REFERENCES Works(Id) ON DELETE CASCADE
                        );

                        -- 作品标签表
                        CREATE TABLE WorkTags (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            WorkId INTEGER NOT NULL,
                            TagName TEXT NOT NULL,
                            Source TEXT CHECK(Source IN ('AI','Manual')) DEFAULT 'Manual',
                            Category TEXT,
                            FOREIGN KEY(WorkId) REFERENCES Works(Id) ON DELETE CASCADE
                        );

                        -- 笔记表
                        CREATE TABLE Notes (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Content TEXT NOT NULL,
                            CreatedTime DATETIME DEFAULT CURRENT_TIMESTAMP,
                            ModifiedTime DATETIME DEFAULT CURRENT_TIMESTAMP
                        );

                        -- 笔记-作品关联表
                        CREATE TABLE NoteWorks (
                            NoteId INTEGER NOT NULL,
                            WorkId INTEGER NOT NULL,
                            PRIMARY KEY (NoteId, WorkId),
                            FOREIGN KEY(NoteId) REFERENCES Notes(Id) ON DELETE CASCADE,
                            FOREIGN KEY(WorkId) REFERENCES Works(Id) ON DELETE CASCADE
                        );

                        -- 笔记标签表
                        CREATE TABLE NoteTags (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            NoteId INTEGER NOT NULL,
                            TagName TEXT NOT NULL,
                            FOREIGN KEY(NoteId) REFERENCES Notes(Id) ON DELETE CASCADE
                        );

                        -- 创建索引提高查询性能
                        CREATE INDEX idx_works_type ON Works(Type);
                        CREATE INDEX idx_userlist_status ON UserList(Status);
                        CREATE INDEX idx_noteworks_note ON NoteWorks(NoteId);
                        CREATE INDEX idx_noteworks_work ON NoteWorks(WorkId);
                        CREATE INDEX idx_worktags_work ON WorkTags(WorkId);
                    ";

                    using (var cmd = new SQLiteCommand(createTablesSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"创建数据库失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查账号的数据库是否存在
        /// </summary>
        /// <param name="accountName">账号名</param>
        /// <returns>是否存在</returns>
        public static bool DatabaseExists(string accountName)
        {
            string dbPath = GetDatabasePath(accountName);
            return File.Exists(dbPath);
        }

        /// <summary>
        /// 初始化新账号的数据库（如果不存在则创建）
        /// </summary>
        /// <param name="accountName">账号名</param>
        public static void InitializeForAccount(string accountName)
        {
            string dbPath = GetDatabasePath(accountName);
            if (!File.Exists(dbPath))
            {
                CreateNewDatabase(dbPath);
            }
        }
    }
}