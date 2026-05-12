using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using AniTechou.Services;

namespace AniTechou.Services.SearchProviders
{
    /// <summary>
    /// MyAnimeList 搜索提供者 — 通过 Jikan v4 API
    /// 文档: https://docs.api.jikan.moe/
    /// 速率限制: 3 req/s
    /// </summary>
    public class MALSearchProvider : ISearchProvider
    {
        private readonly HttpClient _httpClient;

        public string ProviderName => "MAL";

        public MALSearchProvider()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "AniTechou/1.0 (https://github.com/lunatic1157/AniTechou)");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<List<ExternalSearchResult>> SearchAsync(string query, string typeHint = null)
        {
            var results = new List<ExternalSearchResult>();

            try
            {
                // 根据类型提示搜索动画或漫画
                bool searchAnime = true;
                bool searchManga = false;

                if (!string.IsNullOrEmpty(typeHint))
                {
                    string t = typeHint.ToLower();
                    if (t == "manga" || t == "lightnovel")
                    {
                        searchAnime = false;
                        searchManga = true;
                    }
                }

                if (searchAnime)
                {
                    string url = $"https://api.jikan.moe/v4/anime?q={Uri.EscapeDataString(query)}&limit=5&sfw=true";
                    var animeResults = await SearchTypeAsync(url, "anime");
                    results.AddRange(animeResults);
                }

                if (searchManga)
                {
                    string url = $"https://api.jikan.moe/v4/manga?q={Uri.EscapeDataString(query)}&limit=5&sfw=true";
                    var mangaResults = await SearchTypeAsync(url, "manga");
                    results.AddRange(mangaResults);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MALSearch] 搜索异常: {ex.Message}");
            }

            return results;
        }

        private async Task<List<ExternalSearchResult>> SearchTypeAsync(string url, string mediaType)
        {
            var results = new List<ExternalSearchResult>();

            try
            {
                System.Diagnostics.Debug.WriteLine($"[MALSearch] 搜索: {url}");

                var json = await RetryHelper.RetryAsync(async () =>
                {
                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MALSearch] HTTP {response.StatusCode}");
                        return null;
                    }
                    return await response.Content.ReadAsStringAsync();
                }, $"MAL搜索({mediaType})");

                if (json == null) return results;

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var dataArray))
                    return results;

                foreach (var item in dataArray.EnumerateArray())
                {
                    try
                    {
                        var result = ParseJikanItem(item, mediaType);
                        if (result != null) results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MALSearch] 解析条目失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MALSearch] SearchTypeAsync 异常: {ex.Message}");
            }

            return results;
        }

        public async Task<ExternalSearchResult> GetByIdAsync(string externalId)
        {
            try
            {
                // MAL ID 存储在 AniListId 或 MALId 字段中
                string url = $"https://api.jikan.moe/v4/anime/{externalId}/full";
                System.Diagnostics.Debug.WriteLine($"[MALSearch] 获取详情: {url}");

                var json = await RetryHelper.RetryAsync(async () =>
                {
                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode) return null;
                    return await response.Content.ReadAsStringAsync();
                }, "MAL详情");

                if (json == null) return null;

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data))
                    return null;

                return ParseJikanItem(data, "anime");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MALSearch] GetById 异常: {ex.Message}");
                return null;
            }
        }

        private ExternalSearchResult ParseJikanItem(JsonElement item, string mediaType)
        {
            var result = new ExternalSearchResult { Source = "mal" };

            // 标题
            result.Title = SafeGetString(item, "title_english");
            if (string.IsNullOrEmpty(result.Title))
                result.Title = SafeGetString(item, "title");

            // 日文原名
            result.OriginalTitle = SafeGetString(item, "title_japanese");
            if (result.OriginalTitle == result.Title)
                result.OriginalTitle = "";

            // MAL ID
            result.MALId = SafeGetInt(item, "mal_id").ToString();

            // 类型映射
            result.Type = mediaType switch
            {
                "anime" => "Anime",
                "manga" => "Manga",
                _ => SafeGetString(item, "type") switch
                {
                    "anime" => "Anime",
                    "manga" => "Manga",
                    "novel" => "LightNovel",
                    _ => "Anime"
                }
            };

            // 年份
            int yearValue = SafeGetInt(item, "year");
            if (yearValue > 0)
                result.Year = yearValue.ToString();

            // 或从 aired/from 中提取
            if (string.IsNullOrEmpty(result.Year) && item.TryGetProperty("aired", out var aired))
            {
                string from = SafeGetString(aired, "from");
                if (from.Length >= 4) result.Year = from[..4];
            }
            if (string.IsNullOrEmpty(result.Year) && item.TryGetProperty("published", out var published))
            {
                string from = SafeGetString(published, "from");
                if (from.Length >= 4) result.Year = from[..4];
            }

            // 季节
            if (item.TryGetProperty("season", out var seasonProp))
            {
                result.Season = seasonProp.GetString() switch
                {
                    "spring" => "春",
                    "summer" => "夏",
                    "fall" => "秋",
                    "winter" => "冬",
                    _ => ""
                };
            }

            // 简介
            result.Synopsis = SafeGetString(item, "synopsis");
            if (result.Synopsis.Length > 300)
                result.Synopsis = result.Synopsis[..300] + "...";

            // 集数/卷数
            int epCount = SafeGetInt(item, "episodes");
            if (epCount > 0)
                result.Episodes = epCount.ToString();
            if (string.IsNullOrEmpty(result.Episodes))
            {
                int volCount = SafeGetInt(item, "volumes");
                if (volCount > 0)
                    result.Episodes = $"{volCount}卷";
            }

            // 封面
            if (item.TryGetProperty("images", out var images))
            {
                if (images.TryGetProperty("jpg", out var jpg))
                {
                    result.CoverUrl = SafeGetString(jpg, "large_image_url");
                    if (string.IsNullOrEmpty(result.CoverUrl))
                        result.CoverUrl = SafeGetString(jpg, "image_url");
                }
            }

            // 制作公司
            if (item.TryGetProperty("studios", out var studios))
            {
                var studioNames = new List<string>();
                foreach (var studio in studios.EnumerateArray().Take(3))
                {
                    string name = SafeGetString(studio, "name");
                    if (!string.IsNullOrEmpty(name) && !studioNames.Contains(name))
                        studioNames.Add(name);
                }
                result.Company = string.Join(" / ", studioNames);
            }

            // 原作类型 (来自 source 字段)
            result.SourceType = SafeGetString(item, "source") switch
            {
                "Original" => "原创",
                "Manga" => "漫改",
                "Light novel" => "小说改",
                "Novel" => "小说改",
                "Visual novel" => "游戏改",
                "Game" => "游戏改",
                _ => ""
            };

            // 作者信息（漫画/小说适用）
            if (item.TryGetProperty("authors", out var authors))
            {
                var authorNames = new List<string>();
                foreach (var author in authors.EnumerateArray().Take(3))
                {
                    string name = SafeGetString(author, "name");
                    if (!string.IsNullOrEmpty(name) && !authorNames.Contains(name))
                        authorNames.Add(name);
                }
                result.Author = string.Join(" / ", authorNames);
            }

            // 评分
            double score = SafeGetDouble(item, "score");
            if (score > 0)
                result.Tags.Add($"MAL评分:{score:F1}");

            // 类型/流派标签
            if (item.TryGetProperty("genres", out var genres))
            {
                foreach (var genre in genres.EnumerateArray().Take(5))
                {
                    string genreName = SafeGetString(genre, "name");
                    if (!string.IsNullOrEmpty(genreName) && result.Tags.Count < 8)
                        result.Tags.Add(genreName);
                }
            }

            return result;
        }

        private static string SafeGetString(JsonElement element, string property)
        {
            if (element.TryGetProperty(property, out var prop))
            {
                string val = prop.GetString();
                return string.IsNullOrWhiteSpace(val) ? "" : val;
            }
            return "";
        }

        // 安全获取整数（Null 值不会抛异常）
        private static int SafeGetInt(JsonElement element, string property)
        {
            if (element.TryGetProperty(property, out var prop) &&
                prop.ValueKind != JsonValueKind.Null &&
                prop.TryGetInt32(out int val))
                return val;
            return 0;
        }

        // 安全获取浮点数（Null 值不会抛异常）
        private static double SafeGetDouble(JsonElement element, string property)
        {
            if (element.TryGetProperty(property, out var prop) &&
                prop.ValueKind != JsonValueKind.Null &&
                prop.TryGetDouble(out double val))
                return val;
            return 0;
        }
    }
}
