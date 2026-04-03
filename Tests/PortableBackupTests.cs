using System;
using System.Data.SQLite;
using System.IO;
using Xunit;
using AniTechou.Services;

namespace AniTechou.Tests
{
    public class PortableBackupTests
    {
        [Fact]
        public void ExportImportPortableBackup_IncludesCoverAndNoteImages()
        {
            string sourceAccount = "__portable_src_" + Guid.NewGuid().ToString("N");
            string targetAccount = "__portable_dst_" + Guid.NewGuid().ToString("N");

            DatabaseHelper.InitializeForAccount(sourceAccount);
            DatabaseHelper.InitializeForAccount(targetAccount);

            string appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniTechou");
            string coversDir = Path.Combine(appDataRoot, "covers");
            string noteImagesDir = Path.Combine(appDataRoot, "Images", "Notes");
            Directory.CreateDirectory(coversDir);
            Directory.CreateDirectory(noteImagesDir);

            string coverFileName = "portable_test_cover_" + Guid.NewGuid().ToString("N") + ".jpg";
            string coverPath = Path.Combine(coversDir, coverFileName);
            File.WriteAllBytes(coverPath, new byte[] { 1, 2, 3, 4, 5 });

            string noteImageFileName = "portable_test_noteimg_" + Guid.NewGuid().ToString("N") + ".png";
            string noteImagePath = Path.Combine(noteImagesDir, noteImageFileName);
            File.WriteAllBytes(noteImagePath, new byte[] { 9, 8, 7, 6 });

            int workId;
            using (var conn = DatabaseHelper.GetConnection(sourceAccount))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(@"
INSERT INTO Works (Title, OriginalTitle, Type, Company, Year, Season, SourceType, EpisodesVolumes, Synopsis, CoverPath, Author, OriginalWork, AddedTime, LastModified)
VALUES (@Title, @OriginalTitle, @Type, @Company, @Year, @Season, @SourceType, @EpisodesVolumes, @Synopsis, @CoverPath, @Author, @OriginalWork, @AddedTime, @LastModified);
SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@Title", "Portable Test Work");
                    cmd.Parameters.AddWithValue("@OriginalTitle", "");
                    cmd.Parameters.AddWithValue("@Type", "Anime");
                    cmd.Parameters.AddWithValue("@Company", "");
                    cmd.Parameters.AddWithValue("@Year", "2026");
                    cmd.Parameters.AddWithValue("@Season", "春");
                    cmd.Parameters.AddWithValue("@SourceType", "原创");
                    cmd.Parameters.AddWithValue("@EpisodesVolumes", "1");
                    cmd.Parameters.AddWithValue("@Synopsis", "");
                    cmd.Parameters.AddWithValue("@CoverPath", coverPath);
                    cmd.Parameters.AddWithValue("@Author", "");
                    cmd.Parameters.AddWithValue("@OriginalWork", "");
                    cmd.Parameters.AddWithValue("@AddedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@LastModified", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    workId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                using (var cmd = new SQLiteCommand(@"
INSERT INTO UserList (WorkId, Status, Progress, Rating, LastUpdated)
VALUES (@WorkId, @Status, @Progress, @Rating, @LastUpdated);", conn))
                {
                    cmd.Parameters.AddWithValue("@WorkId", workId);
                    cmd.Parameters.AddWithValue("@Status", "wish");
                    cmd.Parameters.AddWithValue("@Progress", "");
                    cmd.Parameters.AddWithValue("@Rating", DBNull.Value);
                    cmd.Parameters.AddWithValue("@LastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }

                string noteContent = $"<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Paragraph><InlineUIContainer><Grid><Image Uid=\"ani-image:{noteImagePath}\" /></Grid></InlineUIContainer></Paragraph></Section>";
                using (var cmd = new SQLiteCommand(@"
INSERT INTO Notes (Title, Content, CreatedTime, ModifiedTime)
VALUES (@Title, @Content, @CreatedTime, @ModifiedTime);", conn))
                {
                    cmd.Parameters.AddWithValue("@Title", "Portable Test Note");
                    cmd.Parameters.AddWithValue("@Content", noteContent);
                    cmd.Parameters.AddWithValue("@CreatedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@ModifiedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }

            string zipPath = Path.Combine(Path.GetTempPath(), "anitechou_portable_backup_" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                var exporter = new WorkService(sourceAccount);
                exporter.ExportPortableBackup(zipPath);
                Assert.True(File.Exists(zipPath));

                var importer = new WorkService(targetAccount);
                var result = importer.ImportPortableBackup(zipPath);
                Assert.True(result.Success, result.ErrorMessage);

                using (var conn = DatabaseHelper.GetConnection(targetAccount))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT CoverPath FROM Works WHERE Title=@Title LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", "Portable Test Work");
                        var importedCover = Convert.ToString(cmd.ExecuteScalar()) ?? "";
                        Assert.False(string.IsNullOrWhiteSpace(importedCover));
                        Assert.True(File.Exists(importedCover));
                        Assert.Equal(coverFileName, Path.GetFileName(importedCover));
                    }

                    using (var cmd = new SQLiteCommand("SELECT Content FROM Notes WHERE Title=@Title LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", "Portable Test Note");
                        var importedContent = Convert.ToString(cmd.ExecuteScalar()) ?? "";
                        Assert.Contains("ani-image:", importedContent, StringComparison.OrdinalIgnoreCase);
                        Assert.Contains(noteImageFileName, importedContent, StringComparison.OrdinalIgnoreCase);
                        Assert.True(File.Exists(Path.Combine(noteImagesDir, noteImageFileName)));
                    }
                }
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                try { if (File.Exists(coverPath)) File.Delete(coverPath); } catch { }
                try { if (File.Exists(noteImagePath)) File.Delete(noteImagePath); } catch { }
            }
        }
    }
}

