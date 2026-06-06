using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AniTechou.Services
{
    public partial class WorkService
    {
        public async Task<string> DownloadAndSaveCoverAsync(string urlOrBangumiInfo, int workId)
        {
            if (string.IsNullOrWhiteSpace(urlOrBangumiInfo)) return "";

            // === 缓存检查：本地文件已存在则直接返回 ===
            try
            {
                using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT CoverPath FROM Works WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", workId);
                        var existingPath = cmd.ExecuteScalar() as string;
                        if (!string.IsNullOrEmpty(existingPath) && File.Exists(existingPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[WorkService] 封面缓存命中: {existingPath}");
                            return existingPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkService] 封面缓存检查异常: {ex.Message}");
            }

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

            using (var client = NetworkClientFactory.CreateHttpClient(TimeSpan.FromSeconds(30), jsonAccept: false))
            {
                client.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
                
                string jikanSearchQuery = searchTitle;
                
                // 如果没有传入 searchTitle，从数据库中获取标题和已存储的 Bangumi ID
                if (string.IsNullOrEmpty(jikanSearchQuery) || string.IsNullOrEmpty(bangumiId))
                {
                    using (var conn = DatabaseHelper.GetConnection(_currentAccount))
                    {
                        conn.Open();
                        using (var cmd = new SQLiteCommand("SELECT Title, OriginalTitle, Year, BangumiId FROM Works WHERE Id = @Id", conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", workId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string title = SafeGetString(reader, 0);
                                    string originalTitle = SafeGetString(reader, 1);
                                    string year = SafeGetString(reader, 2);
                                    string storedBangumiId = SafeGetString(reader, 3);

                                    // 如果未传入 Bangumi ID 但数据库中已存储，优先使用
                                    if (string.IsNullOrEmpty(bangumiId) && !string.IsNullOrEmpty(storedBangumiId))
                                    {
                                        bangumiId = storedBangumiId;
                                        System.Diagnostics.Debug.WriteLine($"[WorkService] 使用数据库中已存储的 Bangumi ID: {bangumiId}");
                                    }

                                    // 基础关键词
                                    jikanSearchQuery = string.IsNullOrEmpty(originalTitle) ? title : originalTitle;

                                    // 【优化】多季度作品防误伤：把年份加进搜索词
                                    if (!string.IsNullOrEmpty(year))
                                    {
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
                        System.Diagnostics.Debug.WriteLine($"[WorkService] 封面来源: Bangumi API (ID: {bangumiId})");
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
                        System.Diagnostics.Debug.WriteLine($"[WorkService] 封面来源: Bangumi 标题搜索 (关键词: {jikanSearchQuery})");
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
                        System.Diagnostics.Debug.WriteLine($"[WorkService] 封面来源: Jikan API (MAL) 备用搜索 (关键词: {jikanSearchQuery})");
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
                                            System.Diagnostics.Debug.WriteLine($"[WorkService] 封面来源: MAL 匹配成功 ({malUrl})");
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
                        System.Diagnostics.Debug.WriteLine($"[WorkService] 封面下载成功并保存: {localPath}");
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
    }
}
