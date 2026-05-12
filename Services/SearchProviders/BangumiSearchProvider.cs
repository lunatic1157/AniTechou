using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace AniTechou.Services.SearchProviders
{
    /// <summary>
    /// Bangumi (bgm.tv) 搜索提供者 — 中文 ACGN 数据库
    /// API 文档: https://bangumi.github.io/api/
    /// </summary>
    public class BangumiSearchProvider : ISearchProvider
    {
        private readonly HttpClient _httpClient;

        public string ProviderName => "Bangumi";

        public BangumiSearchProvider()
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
                // 确定搜索类型
                int? bgmType = typeHint switch
                {
                    "Anime" => 2,
                    "Manga" => 1,
                    "LightNovel" => 1, // Bangumi 把轻小说归类为书籍
                    "Game" => 4,
                    _ => null // 不限定类型
                };

                string typeParam = bgmType.HasValue ? $"&type={bgmType.Value}" : "";
                string url = $"https://api.bgm.tv/search/subject/{Uri.EscapeDataString(query)}?responseGroup=large&max_results=8{typeParam}";

                System.Diagnostics.Debug.WriteLine($"[BangumiSearch] 搜索: {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[BangumiSearch] HTTP {response.StatusCode}");
                    return results;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("list", out var list))
                    return results;

                foreach (var item in list.EnumerateArray())
                {
                    try
                    {
                        var result = ParseBangumiItem(item);
                        if (result != null)
                            results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BangumiSearch] 解析条目失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BangumiSearch] 搜索异常: {ex.Message}");
            }

            return results;
        }

        public async Task<ExternalSearchResult> GetByIdAsync(string externalId)
        {
            try
            {
                string url = $"https://api.bgm.tv/v0/subjects/{externalId}";
                System.Diagnostics.Debug.WriteLine($"[BangumiSearch] 获取详情: {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return ParseBangumiV0Subject(doc.RootElement);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BangumiSearch] GetById 异常: {ex.Message}");
                return null;
            }
        }

        private ExternalSearchResult ParseBangumiItem(JsonElement item)
        {
            var result = new ExternalSearchResult { Source = "bangumi" };

            result.Title = SafeGetString(item, "name_cn");
            if (string.IsNullOrEmpty(result.Title))
                result.Title = SafeGetString(item, "name");

            result.OriginalTitle = SafeGetString(item, "name");
            // 如果原名和中文名一样，清空原名
            if (result.OriginalTitle == result.Title)
                result.OriginalTitle = "";

            // 类型映射
            int bgmType = SafeGetInt(item, "type");
            result.Type = MapBangumiType(bgmType);

            // 日期
            if (item.TryGetProperty("air_date", out var airDate))
            {
                string dateStr = airDate.GetString() ?? "";
                if (dateStr.Length >= 4 && int.TryParse(dateStr[..4], out _))
                    result.Year = dateStr[..4];
            }

            // Bangumi ID
            result.BangumiId = SafeGetInt(item, "id").ToString();

            // 封面
            if (item.TryGetProperty("images", out var images))
            {
                result.CoverUrl = SafeGetString(images, "large");
                if (string.IsNullOrEmpty(result.CoverUrl))
                    result.CoverUrl = SafeGetString(images, "common");
            }

            // 简介
            result.Synopsis = SafeGetString(item, "summary");
            if (result.Synopsis.Length > 200)
                result.Synopsis = result.Synopsis[..200] + "...";

            // 集数
            if (item.TryGetProperty("eps", out var eps))
                result.Episodes = eps.GetInt32().ToString();

            // 评分
            if (item.TryGetProperty("rating", out var rating))
            {
                if (rating.TryGetProperty("score", out var score))
                    result.Tags.Add($"评分:{score.GetDouble():F1}");
            }

            // 标签
            if (item.TryGetProperty("tags", out var tags))
            {
                foreach (var tag in tags.EnumerateArray())
                {
                    string tagName = SafeGetString(tag, "name");
                    if (!string.IsNullOrEmpty(tagName) && result.Tags.Count < 6)
                        result.Tags.Add(tagName);
                }
            }

            return result;
        }

        private ExternalSearchResult ParseBangumiV0Subject(JsonElement root)
        {
            var result = new ExternalSearchResult { Source = "bangumi" };

            result.Title = SafeGetString(root, "name_cn");
            if (string.IsNullOrEmpty(result.Title))
                result.Title = SafeGetString(root, "name");

            result.OriginalTitle = SafeGetString(root, "name");
            if (result.OriginalTitle == result.Title)
                result.OriginalTitle = "";

            int bgmType = SafeGetInt(root, "type");
            result.Type = MapBangumiType(bgmType);

            result.BangumiId = SafeGetInt(root, "id").ToString();

            if (root.TryGetProperty("images", out var images))
            {
                result.CoverUrl = SafeGetString(images, "large");
                if (string.IsNullOrEmpty(result.CoverUrl))
                    result.CoverUrl = SafeGetString(images, "common");
            }

            result.Synopsis = SafeGetString(root, "summary");
            if (result.Synopsis.Length > 300)
                result.Synopsis = result.Synopsis[..300] + "...";

            if (root.TryGetProperty("eps", out var eps))
                result.Episodes = eps.GetInt32().ToString();

            // 日期
            if (root.TryGetProperty("date", out var date))
            {
                string dateStr = date.GetString() ?? "";
                if (dateStr.Length >= 4 && int.TryParse(dateStr[..4], out _))
                    result.Year = dateStr[..4];
            }

            // 制作人员信息 (v0 API infobox)
            if (root.TryGetProperty("infobox", out var infobox))
            {
                foreach (var item in infobox.EnumerateArray())
                {
                    string key = SafeGetString(item, "key");
                    string value = ExtractInfoboxValue(item);

                    if (string.IsNullOrEmpty(value)) continue;

                    if (key.Contains("动画制作") || key.Contains("制作公司") || key.Contains("製作"))
                        result.Company = value;
                    else if (key.Contains("原作") || key.Contains("原案"))
                        result.OriginalWork = value;
                    else if (key.Contains("导演") || key.Contains("监督") || key.Contains("監督"))
                        result.Tags.Add($"导演:{value}");
                    else if (key.Contains("脚本") || key.Contains("系列构成") || key.Contains("シリーズ構成"))
                        result.Tags.Add($"脚本:{value}");
                    else if (key.Contains("音乐") || key.Contains("音楽"))
                        result.Tags.Add($"音乐:{value}");
                }
            }

            // 标签
            if (root.TryGetProperty("tags", out var tags))
            {
                foreach (var tag in tags.EnumerateArray())
                {
                    string tagName = SafeGetString(tag, "name");
                    if (!string.IsNullOrEmpty(tagName) && result.Tags.Count < 8)
                        result.Tags.Add(tagName);
                }
            }

            return result;
        }

        private static string MapBangumiType(int bgmType) => bgmType switch
        {
            1 => "Manga",       // 书籍 → 漫画/轻小说
            2 => "Anime",       // 动画
            3 => "Anime",       // 音乐 → 暂且归入动画
            4 => "Game",        // 游戏
            6 => "Anime",       // 三次元 → 暂且归入动画
            _ => "Anime"
        };

        /// <summary>
        /// 从 Bangumi v0 infobox 的 value 数组中提取文本值
        /// infobox 格式: {"key": "...", "value": [{"v": "文本"}]}
        /// </summary>
        private static string ExtractInfoboxValue(JsonElement item)
        {
            if (!item.TryGetProperty("value", out var valueArray))
                return "";
            if (valueArray.ValueKind == JsonValueKind.Array && valueArray.GetArrayLength() > 0)
            {
                return SafeGetString(valueArray[0], "v");
            }
            return "";
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

        private static int SafeGetInt(JsonElement element, string property)
        {
            if (element.TryGetProperty(property, out var prop) && prop.TryGetInt32(out int val))
                return val;
            return 0;
        }
    }
}
