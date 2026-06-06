using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AniTechou.Services
{
    public static class NetworkClientFactory
    {
        public const string ProxyModeSystem = "System";
        public const string ProxyModeCustom = "Custom";
        public const string ProxyModeNone = "None";

        public class DiagnosticItem
        {
            public string Name { get; set; } = "";
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public long ElapsedMilliseconds { get; set; }
        }

        public static HttpClient CreateHttpClient(TimeSpan timeout, bool jsonAccept = true)
        {
            var client = new HttpClient(CreateHandler())
            {
                Timeout = timeout
            };

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "AniTechou/1.0 (https://github.com/lunatic1157/AniTechou)");
            if (jsonAccept)
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            return client;
        }

        public static HttpClientHandler CreateHandler()
        {
            var config = ConfigManager.Load();
            string mode = NormalizeProxyMode(config.ProxyMode);
            var handler = new HttpClientHandler();

            if (mode == ProxyModeNone)
            {
                handler.UseProxy = false;
                return handler;
            }

            handler.UseProxy = true;
            if (mode == ProxyModeCustom &&
                TryNormalizeProxyAddress(config.ProxyAddress, out var proxyAddress))
            {
                handler.Proxy = new WebProxy(proxyAddress);
            }

            return handler;
        }

        public static string NormalizeProxyMode(string mode)
        {
            if (string.Equals(mode, ProxyModeCustom, StringComparison.OrdinalIgnoreCase))
                return ProxyModeCustom;
            if (string.Equals(mode, ProxyModeNone, StringComparison.OrdinalIgnoreCase))
                return ProxyModeNone;
            return ProxyModeSystem;
        }

        public static bool TryNormalizeProxyAddress(string address, out string normalized)
        {
            normalized = "";
            if (string.IsNullOrWhiteSpace(address)) return false;

            string candidate = address.Trim();
            if (!candidate.Contains("://"))
                candidate = "http://" + candidate;

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps &&
                uri.Scheme != "socks5" &&
                uri.Scheme != "socks4")
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(uri.Host) || uri.Port <= 0)
                return false;

            normalized = uri.ToString().TrimEnd('/');
            return true;
        }

        public static async Task<List<DiagnosticItem>> RunDiagnosticsAsync(string apiUrl = "", string apiKey = "", string model = "")
        {
            var items = new List<DiagnosticItem>
            {
                await ProbeGetAsync("Bangumi API", "https://api.bgm.tv/v0/subjects/364291"),
                await ProbeBangumiImageAsync(),
                await ProbeGetAsync("MAL / Jikan", "https://api.jikan.moe/v4/anime?q=frieren&limit=1"),
                await ProbeAniListAsync()
            };

            if (!string.IsNullOrWhiteSpace(apiUrl) && !string.IsNullOrWhiteSpace(apiKey))
                items.Add(await ProbeAiAsync(apiUrl, apiKey, model));
            else
                items.Add(new DiagnosticItem { Name = "AI API", Success = false, Message = "未填写 API 地址或 Key，已跳过" });

            return items;
        }

        private static async Task<DiagnosticItem> ProbeGetAsync(string name, string url, bool jsonAccept = true)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var client = CreateHttpClient(TimeSpan.FromSeconds(12), jsonAccept);
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                sw.Stop();
                return new DiagnosticItem
                {
                    Name = name,
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ? $"HTTP {(int)response.StatusCode}" : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new DiagnosticItem { Name = name, Success = false, Message = ex.Message, ElapsedMilliseconds = sw.ElapsedMilliseconds };
            }
        }

        private static async Task<DiagnosticItem> ProbeAniListAsync()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var client = CreateHttpClient(TimeSpan.FromSeconds(12));
                var request = new
                {
                    query = "query { Page(perPage: 1) { media(search: \"Frieren\", type: ANIME) { id } } }"
                };
                using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("https://graphql.anilist.co", content);
                sw.Stop();
                return new DiagnosticItem
                {
                    Name = "AniList",
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ? $"HTTP {(int)response.StatusCode}" : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new DiagnosticItem { Name = "AniList", Success = false, Message = ex.Message, ElapsedMilliseconds = sw.ElapsedMilliseconds };
            }
        }

        private static async Task<DiagnosticItem> ProbeBangumiImageAsync()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var client = CreateHttpClient(TimeSpan.FromSeconds(12));
                using var apiResponse = await client.GetAsync("https://api.bgm.tv/v0/subjects/364291");
                if (!apiResponse.IsSuccessStatusCode)
                {
                    sw.Stop();
                    return new DiagnosticItem
                    {
                        Name = "Bangumi 图片域",
                        Success = false,
                        Message = $"无法读取封面信息：HTTP {(int)apiResponse.StatusCode}",
                        ElapsedMilliseconds = sw.ElapsedMilliseconds
                    };
                }

                var json = await apiResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                string imageUrl = "";
                if (doc.RootElement.TryGetProperty("images", out var images))
                {
                    if (images.TryGetProperty("large", out var large)) imageUrl = large.GetString() ?? "";
                    if (string.IsNullOrEmpty(imageUrl) && images.TryGetProperty("common", out var common)) imageUrl = common.GetString() ?? "";
                }

                if (string.IsNullOrEmpty(imageUrl))
                {
                    sw.Stop();
                    return new DiagnosticItem
                    {
                        Name = "Bangumi 图片域",
                        Success = false,
                        Message = "API 未返回可测试的封面 URL",
                        ElapsedMilliseconds = sw.ElapsedMilliseconds
                    };
                }

                using var imageClient = CreateHttpClient(TimeSpan.FromSeconds(12), jsonAccept: false);
                using var imageResponse = await imageClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
                sw.Stop();
                return new DiagnosticItem
                {
                    Name = "Bangumi 图片域",
                    Success = imageResponse.IsSuccessStatusCode,
                    Message = imageResponse.IsSuccessStatusCode ? $"HTTP {(int)imageResponse.StatusCode}" : $"HTTP {(int)imageResponse.StatusCode} {imageResponse.ReasonPhrase}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new DiagnosticItem { Name = "Bangumi 图片域", Success = false, Message = ex.Message, ElapsedMilliseconds = sw.ElapsedMilliseconds };
            }
        }

        private static async Task<DiagnosticItem> ProbeAiAsync(string apiUrl, string apiKey, string model)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var client = CreateHttpClient(TimeSpan.FromSeconds(15));
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
                var request = new
                {
                    model = string.IsNullOrWhiteSpace(model) ? "deepseek-chat" : model,
                    messages = new[] { new { role = "user", content = "hi" } },
                    max_tokens = 5
                };
                using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                using var response = await client.PostAsync($"{apiUrl.TrimEnd('/')}/chat/completions", content);
                sw.Stop();
                return new DiagnosticItem
                {
                    Name = "AI API",
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ? $"HTTP {(int)response.StatusCode}" : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new DiagnosticItem { Name = "AI API", Success = false, Message = ex.Message, ElapsedMilliseconds = sw.ElapsedMilliseconds };
            }
        }
    }
}
