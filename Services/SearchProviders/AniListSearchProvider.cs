using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AniTechou.Services;
using AniTechou.Utilities;

namespace AniTechou.Services.SearchProviders
{
    /// <summary>
    /// AniList 搜索提供者 — 通过 AniList GraphQL API
    /// 文档: https://anilist.gitbook.io/anilist-apiv2-docs/
    /// 速率限制: 90 req/min
    /// </summary>
    public class AniListSearchProvider : ISearchProvider
    {
        private readonly HttpClient _httpClient;

        public string ProviderName => "AniList";

        public AniListSearchProvider()
        {
            _httpClient = NetworkClientFactory.CreateHttpClient(TimeSpan.FromSeconds(15));
        }

        public async Task<List<ExternalSearchResult>> SearchAsync(string query, string typeHint = null)
        {
            var results = new List<ExternalSearchResult>();

            try
            {
                string mediaType = typeHint switch
                {
                    "Anime" => "ANIME",
                    "Manga" => "MANGA",
                    "LightNovel" => "MANGA",
                    "Game" => null,
                    _ => null
                };

                string graphqlQuery = @"
                query ($search: String, $type: MediaType) {
                  Page(perPage: 5) {
                    media(search: $search, type: $type) {
                      id
                      title { romaji english native }
                      type
                      format
                      season
                      seasonYear
                      episodes
                      volumes
                      description
                      coverImage { large extraLarge }
                      studios { nodes { name } }
                      source
                      genres
                      averageScore
                      tags { name }
                    }
                  }
                }";

                var requestBody = new
                {
                    query = graphqlQuery,
                    variables = new { search = query, type = mediaType }
                };

                var json = await PostGraphQLAsync(requestBody, "AniList搜索");

                if (json == null) return results;

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data))
                    return results;
                if (!data.TryGetProperty("Page", out var page))
                    return results;
                if (!page.TryGetProperty("media", out var mediaArray))
                    return results;

                foreach (var item in mediaArray.EnumerateArray())
                {
                    try
                    {
                        var result = ParseAniListMedia(item);
                        if (result != null) results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AniListSearch] 解析条目失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AniListSearch] 搜索异常: {ex.Message}");
            }

            return results;
        }

        public async Task<ExternalSearchResult> GetByIdAsync(string externalId)
        {
            try
            {
                if (!int.TryParse(externalId, out int anilistId))
                    return null;

                string graphqlQuery = @"
                query ($id: Int) {
                  Media(id: $id) {
                    id
                    title { romaji english native }
                    type
                    format
                    season
                    seasonYear
                    episodes
                    volumes
                    description
                    coverImage { large extraLarge }
                    studios { nodes { name } }
                    source
                    genres
                    averageScore
                    tags { name }
                  }
                }";

                var requestBody = new
                {
                    query = graphqlQuery,
                    variables = new { id = anilistId }
                };

                var json = await PostGraphQLAsync(requestBody, "AniList详情");

                if (json == null) return null;

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data))
                    return null;
                if (!data.TryGetProperty("Media", out var media))
                    return null;

                return ParseAniListMedia(media);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AniListSearch] GetById 异常: {ex.Message}");
                return null;
            }
        }

        private async Task<string> PostGraphQLAsync(object requestBody, string context)
        {
            return await RetryHelper.RetryAsync(async () =>
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                System.Diagnostics.Debug.WriteLine($"[AniListSearch] POST graphql.anilist.co ({context})");

                var response = await _httpClient.PostAsync("https://graphql.anilist.co", content);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[AniListSearch] HTTP {response.StatusCode}");
                    return null;
                }
                return await response.Content.ReadAsStringAsync();
            }, context);
        }

        private ExternalSearchResult ParseAniListMedia(JsonElement item)
        {
            var result = new ExternalSearchResult { Source = "anilist" };

            // 标题
            if (item.TryGetProperty("title", out var titleNode))
            {
                result.Title = SafeGetString(titleNode, "english");
                if (string.IsNullOrEmpty(result.Title))
                    result.Title = SafeGetString(titleNode, "romaji");
                result.OriginalTitle = SafeGetString(titleNode, "native");
                if (result.OriginalTitle == result.Title)
                    result.OriginalTitle = "";
            }

            // AniList ID
            int anilistId = SafeGetInt(item, "id");
            if (anilistId > 0)
                result.AniListId = anilistId.ToString();

            // 类型
            string mediaType = SafeGetString(item, "type"); // ANIME or MANGA
            result.Type = mediaType switch
            {
                "ANIME" => "Anime",
                "MANGA" => "Manga",
                _ => "Anime"
            };

            // 如果 format 是 NOVEL，覆盖为 LightNovel
            string format = SafeGetString(item, "format");
            if (format is "NOVEL" or "LIGHT_NOVEL")
                result.Type = "LightNovel";

            // 年份和季节
            int year = SafeGetInt(item, "seasonYear");
            if (year > 0)
                result.Year = year.ToString();

            result.Season = SafeGetString(item, "season") switch
            {
                "SPRING" => "春",
                "SUMMER" => "夏",
                "FALL" => "秋",
                "WINTER" => "冬",
                _ => ""
            };

            // 简介
            result.Synopsis = SafeGetString(item, "description");
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
            if (item.TryGetProperty("coverImage", out var cover))
            {
                result.CoverUrl = SafeGetString(cover, "extraLarge");
                if (string.IsNullOrEmpty(result.CoverUrl))
                    result.CoverUrl = SafeGetString(cover, "large");
            }

            // 制作公司
            if (item.TryGetProperty("studios", out var studios) &&
                studios.TryGetProperty("nodes", out var studioNodes))
            {
                var studioNames = new List<string>();
                foreach (var studio in studioNodes.EnumerateArray().Take(3))
                {
                    string name = SafeGetString(studio, "name");
                    if (!string.IsNullOrEmpty(name) && !studioNames.Contains(name))
                        studioNames.Add(name);
                }
                result.Company = string.Join(" / ", studioNames);
            }

            // 原作类型
            result.SourceType = SafeGetString(item, "source") switch
            {
                "ORIGINAL" => "原创",
                "MANGA" => "漫改",
                "LIGHT_NOVEL" => "小说改",
                "NOVEL" => "小说改",
                "VISUAL_NOVEL" => "游戏改",
                "GAME" => "游戏改",
                _ => ""
            };

            // 流派
            if (item.TryGetProperty("genres", out var genres))
            {
                foreach (var genre in genres.EnumerateArray().Take(5))
                {
                    string genreName = genre.GetString() ?? "";
                    if (!string.IsNullOrEmpty(genreName) && result.Tags.Count < 8)
                        result.Tags.Add(genreName);
                }
            }

            // 标签
            if (item.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tags.EnumerateArray())
                {
                    string tagName = SafeGetString(tag, "name");
                    if (!string.IsNullOrEmpty(tagName) && result.Tags.Count < 8)
                        result.Tags.Add(tagName);
                }
            }

            result.Tags = TagPolicy.NormalizeAutomaticTags(result.Tags, TagPolicy.FromExternalResult(result));
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

        // 安全获取整数（Null 不会抛异常）
        private static int SafeGetInt(JsonElement element, string property)
        {
            if (element.TryGetProperty(property, out var prop) &&
                prop.ValueKind != System.Text.Json.JsonValueKind.Null &&
                prop.TryGetInt32(out int val))
                return val;
            return 0;
        }
    }
}
