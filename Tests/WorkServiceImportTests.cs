using System;
using System.Data.SQLite;
using System.IO;
using AniTechou.Services;
using Xunit;

namespace AniTechou.Tests;

public class WorkServiceImportTests
{
    [Fact]
    public void ImportAllData_InsertsWork_AndEnsuresUserListRow()
    {
        var accountName = NewAccountName();
        try
        {
            var service = new WorkService(accountName);

            var json = """
                       {
                         "Works": [
                           {
                             "Id": 1,
                             "Title": "测试作品",
                             "OriginalTitle": "",
                             "Type": "Anime",
                             "Company": "测试制作",
                             "Year": "2020",
                             "Season": "春",
                             "SourceType": "原创",
                             "EpisodesVolumes": "12",
                             "Synopsis": "简介",
                             "CoverPath": "",
                             "AddedTime": "2020-01-01 00:00:00"
                           }
                         ],
                         "Notes": [],
                         "ExportTime": "2020-01-01T00:00:00"
                       }
                       """;

            var result = service.ImportAllData(json);

            Assert.True(result.Success);
            Assert.Equal(1, result.NewWorks);
            Assert.Equal(0, result.UpdatedWorks);
            Assert.Equal(0, result.InvalidWorks);

            using var conn = DatabaseHelper.GetConnection(accountName);
            conn.Open();

            Assert.Equal(1, ScalarInt(conn, "SELECT COUNT(1) FROM Works"));
            Assert.Equal(1, ScalarInt(conn, "SELECT COUNT(1) FROM UserList"));
            Assert.Equal("wish", ScalarString(conn, "SELECT Status FROM UserList LIMIT 1"));
        }
        finally
        {
            CleanupDatabaseFile(accountName);
        }
    }

    [Fact]
    public void ImportAllData_UpdatesExistingWork_WhenMatchedByRules()
    {
        var accountName = NewAccountName();
        try
        {
            var service = new WorkService(accountName);
            service.AddWork(
                title: "Odd Taxi",
                originalTitle: "奇巧计程车",
                type: "Anime",
                company: "Old Studio",
                year: "2021",
                season: "春",
                sourceType: "原创",
                episodesVolumes: "13",
                progress: "",
                status: "wish",
                rating: 0,
                synopsis: "",
                coverPath: ""
            );

            var json = """
                       {
                         "Works": [
                           {
                             "Id": 99,
                             "Title": "奇巧计程车",
                             "OriginalTitle": "",
                             "Type": "动画",
                             "Company": "New Studio",
                             "Year": "2021",
                             "Season": "春",
                             "SourceType": "原创",
                             "EpisodesVolumes": "13",
                             "Synopsis": "",
                             "CoverPath": "",
                             "AddedTime": "2021-01-01 00:00:00"
                           }
                         ],
                         "Notes": [],
                         "ExportTime": "2021-01-01T00:00:00"
                       }
                       """;

            var result = service.ImportAllData(json);

            Assert.True(result.Success);
            Assert.Equal(0, result.NewWorks);
            Assert.Equal(1, result.UpdatedWorks);

            using var conn = DatabaseHelper.GetConnection(accountName);
            conn.Open();
            Assert.Equal("New Studio", ScalarString(conn, "SELECT Company FROM Works LIMIT 1"));
        }
        finally
        {
            CleanupDatabaseFile(accountName);
        }
    }

    [Fact]
    public void ImportAllData_SkipsDuplicatedNotes_ByTitleAndContent()
    {
        var accountName = NewAccountName();
        try
        {
            var service = new WorkService(accountName);

            var json = """
                       {
                         "Works": [],
                         "Notes": [
                           {
                             "Id": 1,
                             "Title": "标题",
                             "Content": "内容",
                             "CreatedTime": "2022-01-01 00:00:00",
                             "ModifiedTime": "2022-01-02 00:00:00"
                           }
                         ],
                         "ExportTime": "2022-01-03T00:00:00"
                       }
                       """;

            var r1 = service.ImportAllData(json);
            var r2 = service.ImportAllData(json);

            Assert.True(r1.Success);
            Assert.True(r2.Success);
            Assert.Equal(1, r1.NewNotes);
            Assert.Equal(1, r2.SkippedNotes);

            using var conn = DatabaseHelper.GetConnection(accountName);
            conn.Open();
            Assert.Equal(1, ScalarInt(conn, "SELECT COUNT(1) FROM Notes"));
        }
        finally
        {
            CleanupDatabaseFile(accountName);
        }
    }

    private static int ScalarInt(SQLiteConnection conn, string sql)
    {
        using var cmd = new SQLiteCommand(sql, conn);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static string ScalarString(SQLiteConnection conn, string sql)
    {
        using var cmd = new SQLiteCommand(sql, conn);
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private static string NewAccountName()
    {
        return $"test_import_{Guid.NewGuid():N}";
    }

    private static void CleanupDatabaseFile(string accountName)
    {
        try
        {
            var path = DatabaseHelper.GetDatabasePath(accountName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}

