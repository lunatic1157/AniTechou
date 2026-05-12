using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AniTechou.Utilities;

namespace AniTechou.Services
{
    public partial class WorkService
    {
        public class ImportResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = "";
            public int NewWorks { get; set; }
            public int UpdatedWorks { get; set; }
            public int SkippedWorks { get; set; }
            public int InvalidWorks { get; set; }
            public int NewNotes { get; set; }
            public int UpdatedNotes { get; set; }
            public int SkippedNotes { get; set; }
            public int InvalidNotes { get; set; }
        }

        private sealed class ExportDataDto
        {
            public List<WorkExportDto> Works { get; set; }
            public List<NoteExportDto> Notes { get; set; }
            public DateTime ExportTime { get; set; }
        }

        private sealed class WorkExportDto
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string OriginalTitle { get; set; }
            public string Type { get; set; }
            public string Company { get; set; }
            public string Year { get; set; }
            public string Season { get; set; }
            public string SourceType { get; set; }
            public string EpisodesVolumes { get; set; }
            public string Synopsis { get; set; }
            public string CoverPath { get; set; }
            public string AddedTime { get; set; }
        }

        private sealed class NoteExportDto
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public string CreatedTime { get; set; }
            public string ModifiedTime { get; set; }
        }

        private sealed class PortableBackupManifest
        {
            public string Version { get; set; }
            public DateTime CreatedTime { get; set; }
            public List<PortableAssetEntry> Assets { get; set; }
        }

        private sealed class PortableAssetEntry
        {
            public string Kind { get; set; }
            public string OriginalPath { get; set; }
            public string ArchivePath { get; set; }
        }

        private sealed class ExistingWorkRecord
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string OriginalTitle { get; set; } = "";
            public string Type { get; set; } = "";
            public string Company { get; set; } = "";
            public string Year { get; set; } = "";
            public string Season { get; set; } = "";
            public string SourceType { get; set; } = "";
            public string EpisodesVolumes { get; set; } = "";
            public string Synopsis { get; set; } = "";
            public string CoverPath { get; set; } = "";
            public string AddedTime { get; set; } = "";
        }

        public ImportResult ImportAllData(string json)
        {
            var result = new ImportResult();
            if (string.IsNullOrWhiteSpace(json))
            {
                result.Success = false;
                result.ErrorMessage = "导入内容为空";
                return result;
            }

            ExportDataDto data;
            try
            {
                data = System.Text.Json.JsonSerializer.Deserialize<ExportDataDto>(
                    json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"JSON 解析失败：{ex.Message}";
                return result;
            }

            if (data == null)
            {
                result.Success = false;
                result.ErrorMessage = "JSON 解析失败：数据为空";
                return result;
            }

            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        var existingWorks = LoadExistingWorks(conn);
                        ImportWorks(conn, existingWorks, data.Works, result);
                        ImportNotes(conn, data.Notes, result);

                        transaction.Commit();
                        result.Success = true;
                        return result;
                    }
                    catch (Exception ex)
                    {
                        try { transaction.Rollback(); } catch { }
                        result.Success = false;
                        result.ErrorMessage = $"导入失败：{ex.Message}";
                        return result;
                    }
                }
            }
        }

        private List<ExistingWorkRecord> LoadExistingWorks(SQLiteConnection conn)
        {
            var works = new List<ExistingWorkRecord>();
            string sql = "SELECT Id, Title, OriginalTitle, Type, Company, Year, Season, SourceType, EpisodesVolumes, Synopsis, CoverPath, AddedTime FROM Works";
            using (var cmd = new SQLiteCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    works.Add(new ExistingWorkRecord
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

        private static void ImportWorks(SQLiteConnection conn, List<ExistingWorkRecord> existingWorks, List<WorkExportDto> works, ImportResult result)
        {
            if (works == null || works.Count == 0) return;

            foreach (var work in works)
            {
                if (work == null)
                {
                    result.InvalidWorks++;
                    continue;
                }

                string title = (work.Title ?? "").Trim();
                string originalTitle = (work.OriginalTitle ?? "").Trim();
                string type = WorkDataRules.NormalizeTypeToEnglish(work.Type ?? "");
                if (string.IsNullOrWhiteSpace(type) || !IsAllowedWorkType(type))
                {
                    result.InvalidWorks++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(originalTitle))
                {
                    result.InvalidWorks++;
                    continue;
                }

                string company = (work.Company ?? "").Trim();
                string year = NormalizeYear(work.Year);
                string season = (work.Season ?? "").Trim();
                string sourceType = WorkDataRules.NormalizeSourceType(work.SourceType);
                string episodesVolumes = (work.EpisodesVolumes ?? "").Trim();
                string synopsis = (work.Synopsis ?? "").Trim();
                string coverPath = (work.CoverPath ?? "").Trim();

                var match = FindWorkMatch(existingWorks, title, originalTitle, type);
                if (match != null)
                {
                    EnsureUserListRow(conn, match.Id);

                    bool changed =
                        !StringEquals(match.Title, title)
                        || !StringEquals(match.OriginalTitle, originalTitle)
                        || !StringEquals(WorkDataRules.NormalizeTypeToEnglish(match.Type), type)
                        || !StringEquals(match.Company, company)
                        || !StringEquals(NormalizeYear(match.Year), year)
                        || !StringEquals(WorkDataRules.NormalizeSourceType(match.SourceType), sourceType)
                        || !StringEquals(match.Season, season)
                        || !StringEquals(match.EpisodesVolumes, episodesVolumes)
                        || !StringEquals(match.Synopsis, synopsis)
                        || !StringEquals(match.CoverPath, coverPath);

                    if (!changed)
                    {
                        result.SkippedWorks++;
                        continue;
                    }

                    string updateSql = @"
                        UPDATE Works
                        SET Title = @Title,
                            OriginalTitle = @OriginalTitle,
                            Type = @Type,
                            Company = @Company,
                            Year = @Year,
                            Season = @Season,
                            SourceType = @SourceType,
                            EpisodesVolumes = @EpisodesVolumes,
                            Synopsis = @Synopsis,
                            CoverPath = @CoverPath,
                            LastModified = @LastModified
                        WHERE Id = @Id";
                    using (var cmd = new SQLiteCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@OriginalTitle", originalTitle);
                        cmd.Parameters.AddWithValue("@Type", type);
                        cmd.Parameters.AddWithValue("@Company", company);
                        cmd.Parameters.AddWithValue("@Year", year);
                        cmd.Parameters.AddWithValue("@Season", season);
                        cmd.Parameters.AddWithValue("@SourceType", sourceType);
                        cmd.Parameters.AddWithValue("@EpisodesVolumes", episodesVolumes);
                        cmd.Parameters.AddWithValue("@Synopsis", synopsis);
                        cmd.Parameters.AddWithValue("@CoverPath", coverPath);
                        cmd.Parameters.AddWithValue("@LastModified", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@Id", match.Id);
                        cmd.ExecuteNonQuery();
                    }

                    match.Title = title;
                    match.OriginalTitle = originalTitle;
                    match.Type = type;
                    match.Company = company;
                    match.Year = year;
                    match.Season = season;
                    match.SourceType = sourceType;
                    match.EpisodesVolumes = episodesVolumes;
                    match.Synopsis = synopsis;
                    match.CoverPath = coverPath;

                    result.UpdatedWorks++;
                    continue;
                }

                string addedTime = ParseDateTimeOrNow(work.AddedTime).ToString("yyyy-MM-dd HH:mm:ss");
                string insertSql = @"
                    INSERT INTO Works (Title, OriginalTitle, Type, Company, Year, Season, SourceType, EpisodesVolumes, Synopsis, CoverPath, Author, OriginalWork, AddedTime, LastModified)
                    VALUES (@Title, @OriginalTitle, @Type, @Company, @Year, @Season, @SourceType, @EpisodesVolumes, @Synopsis, @CoverPath, @Author, @OriginalWork, @AddedTime, @LastModified);
                    SELECT last_insert_rowid();";

                int workId;
                using (var cmd = new SQLiteCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", title);
                    cmd.Parameters.AddWithValue("@OriginalTitle", originalTitle);
                    cmd.Parameters.AddWithValue("@Type", type);
                    cmd.Parameters.AddWithValue("@Company", company);
                    cmd.Parameters.AddWithValue("@Year", year);
                    cmd.Parameters.AddWithValue("@Season", season);
                    cmd.Parameters.AddWithValue("@SourceType", sourceType);
                    cmd.Parameters.AddWithValue("@EpisodesVolumes", episodesVolumes);
                    cmd.Parameters.AddWithValue("@Synopsis", synopsis);
                    cmd.Parameters.AddWithValue("@CoverPath", coverPath);
                    cmd.Parameters.AddWithValue("@Author", "");
                    cmd.Parameters.AddWithValue("@OriginalWork", "");
                    cmd.Parameters.AddWithValue("@AddedTime", addedTime);
                    cmd.Parameters.AddWithValue("@LastModified", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    workId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                string insertUserListSql = @"
                    INSERT INTO UserList (WorkId, Status, Progress, Rating, LastUpdated)
                    VALUES (@WorkId, @Status, @Progress, @Rating, @LastUpdated)";
                using (var cmd = new SQLiteCommand(insertUserListSql, conn))
                {
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    cmd.Parameters.AddWithValue("@Status", "wish");
                    cmd.Parameters.AddWithValue("@Progress", "");
                    cmd.Parameters.AddWithValue("@Rating", DBNull.Value);
                    cmd.Parameters.AddWithValue("@LastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }

                existingWorks.Add(new ExistingWorkRecord
                {
                    Id = workId,
                    Title = title,
                    OriginalTitle = originalTitle,
                    Type = type,
                    Company = company,
                    Year = year,
                    Season = season,
                    SourceType = sourceType,
                    EpisodesVolumes = episodesVolumes,
                    Synopsis = synopsis,
                    CoverPath = coverPath,
                    AddedTime = addedTime
                });

                result.NewWorks++;
            }
        }

        private static ExistingWorkRecord FindWorkMatch(List<ExistingWorkRecord> existingWorks, string title, string originalTitle, string type)
        {
            foreach (var existing in existingWorks)
            {
                if (WorkDataRules.IsSameWork(existing.Title, existing.OriginalTitle, existing.Type, title, originalTitle, type))
                {
                    return existing;
                }
            }
            return null;
        }

        private static void EnsureUserListRow(SQLiteConnection conn, int workId)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(1) FROM UserList WHERE WorkId = @WorkId", conn))
            {
                cmd.Parameters.AddWithValue("@WorkId", workId);
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                if (count > 0) return;
            }

            string insertUserListSql = @"
                INSERT INTO UserList (WorkId, Status, Progress, Rating, LastUpdated)
                VALUES (@WorkId, @Status, @Progress, @Rating, @LastUpdated)";
            using (var cmd = new SQLiteCommand(insertUserListSql, conn))
            {
                cmd.Parameters.AddWithValue("@WorkId", workId);
                cmd.Parameters.AddWithValue("@Status", "wish");
                cmd.Parameters.AddWithValue("@Progress", "");
                cmd.Parameters.AddWithValue("@Rating", DBNull.Value);
                cmd.Parameters.AddWithValue("@LastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
        }

        private static void ImportNotes(SQLiteConnection conn, List<NoteExportDto> notes, ImportResult result)
        {
            if (notes == null || notes.Count == 0) return;

            foreach (var note in notes)
            {
                if (note == null)
                {
                    result.InvalidNotes++;
                    continue;
                }

                string title = (note.Title ?? "").Trim();
                string content = note.Content ?? "";
                if (string.IsNullOrWhiteSpace(content))
                {
                    result.InvalidNotes++;
                    continue;
                }

                if (NoteExistsByTitleAndContent(conn, title, content))
                {
                    result.SkippedNotes++;
                    continue;
                }

                string insertSql = @"
                    INSERT INTO Notes (Title, Content, CreatedTime, ModifiedTime)
                    VALUES (@Title, @Content, @CreatedTime, @ModifiedTime)";
                using (var cmd = new SQLiteCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", title);
                    cmd.Parameters.AddWithValue("@Content", content);
                    cmd.Parameters.AddWithValue("@CreatedTime", ParseDateTimeOrNow(note.CreatedTime).ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@ModifiedTime", ParseDateTimeOrNow(note.ModifiedTime).ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }

                result.NewNotes++;
            }
        }

        private static bool NoteExistsByTitleAndContent(SQLiteConnection conn, string title, string content)
        {
            using (var cmd = new SQLiteCommand("SELECT 1 FROM Notes WHERE COALESCE(Title,'') = @Title AND Content = @Content LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@Title", title ?? "");
                cmd.Parameters.AddWithValue("@Content", content ?? "");
                var found = cmd.ExecuteScalar();
                return found != null && found != DBNull.Value;
            }
        }

        private static bool IsAllowedWorkType(string type)
        {
            return type == "Anime" || type == "Manga" || type == "LightNovel" || type == "Game";
        }

        private static string NormalizeYear(string year)
        {
            var value = (year ?? "").Trim();
            if (string.IsNullOrEmpty(value)) return "";
            var match = System.Text.RegularExpressions.Regex.Match(value, @"\d+");
            return match.Success ? match.Value : value;
        }

        private static DateTime ParseDateTimeOrNow(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DateTime.Now;
            return DateTime.TryParse(value, out var dt) ? dt : DateTime.Now;
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.Ordinal);
        }

        /// <summary>
        /// 导出所有数据
        /// </summary>
        public string ExportAllData()
        {
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();

                var data = new ExportDataDto
                {
                    Works = GetWorksForExport(conn),
                    Notes = GetNotesForExport(conn),
                    ExportTime = DateTime.Now
                };

                return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
        }

        public void ExportPortableBackup(string zipFilePath)
        {
            if (string.IsNullOrWhiteSpace(zipFilePath)) throw new Exception("导出路径为空");

            ExportDataDto data;
            using (var conn = DatabaseHelper.GetConnection(_currentAccount))
            {
                conn.Open();
                data = new ExportDataDto
                {
                    Works = GetWorksForExport(conn),
                    Notes = GetNotesForExport(conn),
                    ExportTime = DateTime.Now
                };
            }

            var manifest = new PortableBackupManifest
            {
                Version = "1",
                CreatedTime = DateTime.Now,
                Assets = new List<PortableAssetEntry>()
            };

            var assetsToWrite = new List<(string kind, string originalPath, string archivePath, string sourceFilePath)>();

            foreach (var work in data.Works ?? new List<WorkExportDto>())
            {
                if (work == null) continue;
                string coverPath = (work.CoverPath ?? "").Trim();
                if (string.IsNullOrWhiteSpace(coverPath)) continue;

                string resolvedCover = ResolveCoverFilePath(coverPath);
                if (string.IsNullOrWhiteSpace(resolvedCover) || !File.Exists(resolvedCover)) continue;

                string fileName = Path.GetFileName(resolvedCover);
                if (string.IsNullOrWhiteSpace(fileName)) continue;

                string archivePath = $"assets/covers/{fileName}";
                assetsToWrite.Add(("cover", coverPath, archivePath, resolvedCover));
            }

            foreach (var note in data.Notes ?? new List<NoteExportDto>())
            {
                if (note == null) continue;
                string content = note.Content ?? "";
                foreach (var imgPath in ExtractNoteImagePaths(content))
                {
                    string resolved = imgPath;
                    if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved)) continue;

                    string fileName = Path.GetFileName(resolved);
                    if (string.IsNullOrWhiteSpace(fileName)) continue;

                    string archivePath = $"assets/note-images/{fileName}";
                    assetsToWrite.Add(("note-image", resolved, archivePath, resolved));
                }
            }

            var dedup = new Dictionary<string, (string kind, string originalPath, string archivePath, string sourceFilePath)>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in assetsToWrite)
            {
                string key = $"{a.kind}|{a.archivePath}";
                if (dedup.ContainsKey(key)) continue;
                dedup[key] = a;
            }

            foreach (var a in dedup.Values)
            {
                manifest.Assets.Add(new PortableAssetEntry { Kind = a.kind, OriginalPath = a.originalPath, ArchivePath = a.archivePath });
            }

            var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string dataJson = System.Text.Json.JsonSerializer.Serialize(data, jsonOptions);
            string manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest, jsonOptions);

            if (File.Exists(zipFilePath)) File.Delete(zipFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(zipFilePath) ?? ".");

            using var fs = new FileStream(zipFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

            WriteTextEntry(zip, "data.json", dataJson);
            WriteTextEntry(zip, "manifest.json", manifestJson);

            foreach (var a in dedup.Values)
            {
                var entry = zip.CreateEntry(a.archivePath, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var fileStream = new FileStream(a.sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                fileStream.CopyTo(entryStream);
            }
        }

        public ImportResult ImportPortableBackup(string zipFilePath)
        {
            if (string.IsNullOrWhiteSpace(zipFilePath)) return new ImportResult { Success = false, ErrorMessage = "导入路径为空" };
            if (!File.Exists(zipFilePath)) return new ImportResult { Success = false, ErrorMessage = "备份文件不存在" };

            using var fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

            var dataEntry = zip.GetEntry("data.json");
            if (dataEntry == null) return new ImportResult { Success = false, ErrorMessage = "备份文件缺少 data.json" };

            string dataJson;
            using (var sr = new StreamReader(dataEntry.Open(), Encoding.UTF8))
            {
                dataJson = sr.ReadToEnd();
            }

            ExportDataDto data;
            try
            {
                data = System.Text.Json.JsonSerializer.Deserialize<ExportDataDto>(
                    dataJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch (Exception ex)
            {
                return new ImportResult { Success = false, ErrorMessage = $"备份解析失败：{ex.Message}" };
            }

            if (data == null) return new ImportResult { Success = false, ErrorMessage = "备份解析失败：数据为空" };

            var manifestEntry = zip.GetEntry("manifest.json");
            PortableBackupManifest manifest = null;
            if (manifestEntry != null)
            {
                try
                {
                    using var sr = new StreamReader(manifestEntry.Open(), Encoding.UTF8);
                    string manifestJson = sr.ReadToEnd();
                    manifest = System.Text.Json.JsonSerializer.Deserialize<PortableBackupManifest>(
                        manifestJson,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                }
                catch
                {
                    manifest = null;
                }
            }

            var mapByOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var mapByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var asset in manifest?.Assets ?? new List<PortableAssetEntry>())
            {
                if (asset == null) continue;
                string kind = (asset.Kind ?? "").Trim();
                string originalPath = (asset.OriginalPath ?? "").Trim();
                string archivePath = (asset.ArchivePath ?? "").Trim();
                if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(archivePath)) continue;

                var entry = zip.GetEntry(archivePath);
                if (entry == null) continue;

                string fileName = Path.GetFileName(archivePath);
                if (string.IsNullOrWhiteSpace(fileName)) continue;

                string destPath = kind == "cover"
                    ? Path.Combine(GetCoversDirectory(), fileName)
                    : kind == "note-image"
                        ? Path.Combine(GetNotesImagesDirectory(), fileName)
                        : "";

                if (string.IsNullOrWhiteSpace(destPath)) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? ".");
                using (var entryStream = entry.Open())
                using (var outStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    entryStream.CopyTo(outStream);
                }

                if (!string.IsNullOrWhiteSpace(originalPath)) mapByOriginal[originalPath] = destPath;
                mapByFileName[fileName] = destPath;
            }

            foreach (var work in data.Works ?? new List<WorkExportDto>())
            {
                if (work == null) continue;
                string coverPath = (work.CoverPath ?? "").Trim();
                if (string.IsNullOrWhiteSpace(coverPath)) continue;

                if (mapByOriginal.TryGetValue(coverPath, out var mapped))
                {
                    work.CoverPath = mapped;
                    continue;
                }

                string fileName = Path.GetFileName(coverPath);
                if (!string.IsNullOrWhiteSpace(fileName) && mapByFileName.TryGetValue(fileName, out var mappedByName))
                {
                    work.CoverPath = mappedByName;
                }
            }

            foreach (var note in data.Notes ?? new List<NoteExportDto>())
            {
                if (note == null) continue;
                note.Content = RewriteNoteContentPaths(note.Content ?? "", mapByOriginal, mapByFileName);
            }

            string rewrittenJson = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            return ImportAllData(rewrittenJson);
        }

        private List<WorkExportDto> GetWorksForExport(SQLiteConnection conn)
        {
            var works = new List<WorkExportDto>();
            string sql = "SELECT Id, Title, OriginalTitle, Type, Company, Year, Season, SourceType, EpisodesVolumes, Synopsis, CoverPath, AddedTime FROM Works";
            using (var cmd = new SQLiteCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    works.Add(new WorkExportDto
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

        private List<NoteExportDto> GetNotesForExport(SQLiteConnection conn)
        {
            var notes = new List<NoteExportDto>();
            string sql = "SELECT Id, Title, Content, CreatedTime, ModifiedTime FROM Notes";
            using (var cmd = new SQLiteCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    notes.Add(new NoteExportDto
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

        private static void WriteTextEntry(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(content ?? "");
        }

        private static string GetAppDataRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniTechou");
        }

        private static string GetCoversDirectory()
        {
            string dir = Path.Combine(GetAppDataRoot(), "covers");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string GetNotesImagesDirectory()
        {
            string dir = Path.Combine(GetAppDataRoot(), "Images", "Notes");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static IEnumerable<string> ExtractNoteImagePaths(string content)
        {
            if (string.IsNullOrEmpty(content)) yield break;

            foreach (Match match in Regex.Matches(content, "ani-image:(?<path>[^\"]+)", RegexOptions.IgnoreCase))
            {
                var p = (match.Groups["path"]?.Value ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(p)) yield return p;
            }
        }

        private static string RewriteNoteContentPaths(string content, Dictionary<string, string> mapByOriginal, Dictionary<string, string> mapByFileName)
        {
            if (string.IsNullOrEmpty(content)) return content;
            if ((mapByOriginal == null || mapByOriginal.Count == 0) && (mapByFileName == null || mapByFileName.Count == 0)) return content;

            return Regex.Replace(
                content,
                "ani-image:(?<path>[^\"]+)",
                m =>
                {
                    string p = (m.Groups["path"]?.Value ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(p)) return m.Value;

                    if (mapByOriginal != null && mapByOriginal.TryGetValue(p, out var mapped))
                    {
                        return $"ani-image:{mapped}";
                    }

                    string fileName = Path.GetFileName(p);
                    if (!string.IsNullOrWhiteSpace(fileName) && mapByFileName != null && mapByFileName.TryGetValue(fileName, out var mappedByName))
                    {
                        return $"ani-image:{mappedByName}";
                    }

                    return m.Value;
                },
                RegexOptions.IgnoreCase
            );
        }

        private static string ResolveCoverFilePath(string coverPath)
        {
            string p = (coverPath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(p)) return "";
            if (File.Exists(p)) return p;

            string fileName = Path.GetFileName(p);
            if (string.IsNullOrWhiteSpace(fileName)) return "";
            string fallback = Path.Combine(GetCoversDirectory(), fileName);
            return File.Exists(fallback) ? fallback : "";
        }
    }
}
