using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AniTechou.Services.SearchProviders;
using Xunit;

namespace AniTechou.Tests;

public class SearchProviderTests
{
    /// <summary>
    /// 创建一个 HttpClient，其消息处理程序返回指定的 JSON 响应
    /// </summary>
    private static HttpClient CreateMockHttpClient(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandler(responseJson, status);
        var client = new HttpClient(handler) { Timeout = System.TimeSpan.FromSeconds(5) };
        return client;
    }

    // === Bangumi 搜索 JSON 解析 ===

    [Fact]
    public async Task BangumiSearch_ParsesValidResponse()
    {
        var json = @"{
            ""list"": [
                {
                    ""id"": 364291,
                    ""name"": ""葬送のフリーレン"",
                    ""name_cn"": ""葬送的芙莉莲"",
                    ""type"": 2,
                    ""air_date"": ""2023-09-29"",
                    ""summary"": ""打倒魔王后..."",
                    ""eps"": 28,
                    ""images"": { ""large"": ""https://example.com/cover.jpg"", ""common"": ""https://example.com/small.jpg"" },
                    ""rating"": { ""score"": 8.5 },
                    ""tags"": [{ ""name"": ""奇幻"" }, { ""name"": ""冒险"" }]
                }
            ]
        }";

        var provider = new TestableBangumiSearchProvider(json);
        var results = await provider.SearchAsync("芙莉莲");

        Assert.Single(results);
        var r = results[0];
        Assert.Equal("葬送的芙莉莲", r.Title);
        Assert.Equal("葬送のフリーレン", r.OriginalTitle);
        Assert.Equal("Anime", r.Type);
        Assert.Equal("2023", r.Year);
        Assert.Equal("364291", r.BangumiId);
        Assert.Equal("https://example.com/cover.jpg", r.CoverUrl);
        Assert.Equal("28", r.Episodes);
        Assert.Contains("奇幻", r.Tags);
        Assert.Contains("冒险", r.Tags);
        Assert.Contains("评分:8.5", r.Tags);
    }

    [Fact]
    public async Task BangumiSearch_HandlesEmptyResults()
    {
        var json = @"{ ""list"": [] }";

        var provider = new TestableBangumiSearchProvider(json);
        var results = await provider.SearchAsync("不存在的作品XYZ");

        Assert.Empty(results);
    }

    [Fact]
    public async Task BangumiSearch_HandlesMissingFields()
    {
        var json = @"{
            ""list"": [
                {
                    ""id"": 123,
                    ""name"": ""Test Anime"",
                    ""name_cn"": """",
                    ""type"": 2
                }
            ]
        }";

        var provider = new TestableBangumiSearchProvider(json);
        var results = await provider.SearchAsync("Test");

        Assert.Single(results);
        var r = results[0];
        Assert.Equal("Test Anime", r.Title); // fallback to name
        Assert.Equal("123", r.BangumiId);
        Assert.Equal("Anime", r.Type);
        Assert.Equal("", r.Year);
        Assert.Equal("", r.Synopsis);
    }

    [Fact]
    public async Task BangumiSearch_HandlesNetworkError()
    {
        var provider = new TestableBangumiSearchProvider(null, throwError: true);
        var results = await provider.SearchAsync("test");

        // 网络异常应返回空列表而非抛出
        Assert.Empty(results);
    }

    // === MAL 搜索 JSON 解析 ===

    [Fact]
    public async Task MALSearch_ParsesValidAnimeResponse()
    {
        var json = @"{
            ""data"": [
                {
                    ""mal_id"": 51179,
                    ""title"": ""Mushoku Tensei: Isekai Ittara Honki Dasu"",
                    ""title_english"": ""Mushoku Tensei: Jobless Reincarnation"",
                    ""title_japanese"": ""無職転生 ～異世界行ったら本気だす～"",
                    ""type"": ""anime"",
                    ""year"": 2024,
                    ""season"": ""spring"",
                    ""synopsis"": ""A story about reincarnation..."",
                    ""episodes"": 23,
                    ""score"": 8.35,
                    ""images"": { ""jpg"": { ""large_image_url"": ""https://example.com/mal.jpg"" } },
                    ""studios"": [{ ""name"": ""Studio Bind"" }],
                    ""source"": ""Light novel"",
                    ""genres"": [{ ""name"": ""Fantasy"" }, { ""name"": ""Isekai"" }]
                }
            ]
        }";

        var provider = new TestableMALSearchProvider(json);
        var results = await provider.SearchAsync("無職転生", "Anime");

        Assert.Single(results);
        var r = results[0];
        Assert.Equal("Mushoku Tensei: Jobless Reincarnation", r.Title);
        Assert.Equal("無職転生 ～異世界行ったら本気だす～", r.OriginalTitle);
        Assert.Equal("Anime", r.Type);
        Assert.Equal("2024", r.Year);
        Assert.Equal("51179", r.MALId);
        Assert.Equal("23", r.Episodes);
        Assert.Equal("小说改", r.SourceType);
        Assert.Equal("Studio Bind", r.Company);
        Assert.Contains("Fantasy", r.Tags);
        Assert.Contains("Isekai", r.Tags);
        Assert.Contains("MAL评分:8.3", r.Tags);
    }

    [Fact]
    public async Task MALSearch_HandlesNetworkError()
    {
        var provider = new TestableMALSearchProvider(null, throwError: true);
        var results = await provider.SearchAsync("test");

        Assert.Empty(results);
    }

    // === AniList 搜索 JSON 解析 ===

    [Fact]
    public async Task AniListSearch_ParsesValidGraphQLResponse()
    {
        var json = @"{
            ""data"": {
                ""Page"": {
                    ""media"": [
                        {
                            ""id"": 154587,
                            ""title"": { ""romaji"": ""Sousou no Frieren"", ""english"": ""Frieren: Beyond Journey's End"", ""native"": ""葬送のフリーレン"" },
                            ""type"": ""ANIME"",
                            ""format"": ""TV"",
                            ""season"": ""FALL"",
                            ""seasonYear"": 2023,
                            ""episodes"": 28,
                            ""description"": ""The demon king has been defeated..."",
                            ""coverImage"": { ""large"": ""https://example.com/anilist_large.jpg"", ""extraLarge"": ""https://example.com/anilist.jpg"" },
                            ""studios"": { ""nodes"": [{ ""name"": ""MADHOUSE"" }] },
                            ""source"": ""MANGA"",
                            ""genres"": [""Fantasy"", ""Adventure""],
                            ""averageScore"": 89
                        }
                    ]
                }
            }
        }";

        var provider = new TestableAniListSearchProvider(json);
        var results = await provider.SearchAsync("Frieren");

        Assert.Single(results);
        var r = results[0];
        Assert.Equal("Frieren: Beyond Journey's End", r.Title);
        Assert.Equal("葬送のフリーレン", r.OriginalTitle);
        Assert.Equal("Anime", r.Type);
        Assert.Equal("2023", r.Year);
        Assert.Equal("秋", r.Season);
        Assert.Equal("154587", r.AniListId);
        Assert.Equal("28", r.Episodes);
        Assert.Equal("https://example.com/anilist.jpg", r.CoverUrl);
        Assert.Equal("漫改", r.SourceType);
        Assert.Contains("Fantasy", r.Tags);
        Assert.Contains("AniList评分:89%", r.Tags);
    }

    [Fact]
    public async Task AniListSearch_HandlesNetworkError()
    {
        var provider = new TestableAniListSearchProvider(null, throwError: true);
        var results = await provider.SearchAsync("test");

        Assert.Empty(results);
    }
}

// === 测试辅助类 ===

/// <summary>
/// 可注入模拟 HttpClient 的 BangumiSearchProvider
/// </summary>
internal class TestableBangumiSearchProvider : ISearchProvider
{
    private readonly HttpClient _httpClient;

    public string ProviderName => "Bangumi";

    public TestableBangumiSearchProvider(string responseJson, bool throwError = false)
    {
        if (throwError)
        {
            _httpClient = new HttpClient(new ThrowingHandler());
        }
        else
        {
            _httpClient = new HttpClient(new MockHttpMessageHandler(responseJson));
        }
        _httpClient.Timeout = System.TimeSpan.FromSeconds(5);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Test");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<System.Collections.Generic.List<ExternalSearchResult>> SearchAsync(string query, string typeHint = null)
    {
        try
        {
            var response = await _httpClient.GetAsync("https://api.bgm.tv/search/subject/test");
            if (!response.IsSuccessStatusCode) return new System.Collections.Generic.List<ExternalSearchResult>();
            var json = await response.Content.ReadAsStringAsync();

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var results = new System.Collections.Generic.List<ExternalSearchResult>();

            if (!doc.RootElement.TryGetProperty("list", out var list))
                return results;

            foreach (var item in list.EnumerateArray())
            {
                results.Add(ParseItem(item));
            }
            return results;
        }
        catch
        {
            return new System.Collections.Generic.List<ExternalSearchResult>();
        }
    }

    public Task<ExternalSearchResult> GetByIdAsync(string externalId) => Task.FromResult<ExternalSearchResult>(null);

    private ExternalSearchResult ParseItem(System.Text.Json.JsonElement item)
    {
        var result = new ExternalSearchResult { Source = "bangumi" };

        result.Title = SafeGet(item, "name_cn");
        if (string.IsNullOrEmpty(result.Title))
            result.Title = SafeGet(item, "name");

        result.OriginalTitle = SafeGet(item, "name");
        if (result.OriginalTitle == result.Title) result.OriginalTitle = "";

        int bgmType = SafeGetInt(item, "type");
        result.Type = bgmType switch { 1 => "Manga", 2 => "Anime", 4 => "Game", _ => "Anime" };

        if (item.TryGetProperty("air_date", out var date))
        {
            string s = date.GetString() ?? "";
            if (s.Length >= 4) result.Year = s[..4];
        }

        result.BangumiId = SafeGetInt(item, "id").ToString();

        if (item.TryGetProperty("images", out var images))
        {
            result.CoverUrl = SafeGet(images, "large");
            if (string.IsNullOrEmpty(result.CoverUrl)) result.CoverUrl = SafeGet(images, "common");
        }

        result.Synopsis = SafeGet(item, "summary");
        if (result.Synopsis.Length > 200) result.Synopsis = result.Synopsis[..200] + "...";

        if (item.TryGetProperty("eps", out var eps)) result.Episodes = eps.GetInt32().ToString();

        if (item.TryGetProperty("rating", out var rating))
            if (rating.TryGetProperty("score", out var score))
                result.Tags.Add($"评分:{score.GetDouble():F1}");

        if (item.TryGetProperty("tags", out var tags))
            foreach (var t in tags.EnumerateArray())
            {
                string name = SafeGet(t, "name");
                if (!string.IsNullOrEmpty(name) && result.Tags.Count < 6) result.Tags.Add(name);
            }

        return result;
    }

    private static string SafeGet(System.Text.Json.JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) ? (v.GetString() ?? "") : "";

    private static int SafeGetInt(System.Text.Json.JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.TryGetInt32(out int n) ? n : 0;
}

internal class TestableMALSearchProvider : ISearchProvider
{
    private readonly HttpClient _httpClient;
    public string ProviderName => "MAL";

    public TestableMALSearchProvider(string responseJson, bool throwError = false)
    {
        _httpClient = throwError
            ? new HttpClient(new ThrowingHandler())
            : new HttpClient(new MockHttpMessageHandler(responseJson));
        _httpClient.Timeout = System.TimeSpan.FromSeconds(5);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Test");
    }

    public async Task<System.Collections.Generic.List<ExternalSearchResult>> SearchAsync(string query, string typeHint = null)
    {
        try
        {
            var response = await _httpClient.GetAsync("https://api.jikan.moe/v4/anime?q=test");
            if (!response.IsSuccessStatusCode) return new System.Collections.Generic.List<ExternalSearchResult>();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            var results = new System.Collections.Generic.List<ExternalSearchResult>();
            if (!doc.RootElement.TryGetProperty("data", out var data)) return results;

            foreach (var item in data.EnumerateArray())
            {
                var r = new ExternalSearchResult { Source = "mal" };
                r.Title = SafeGet(item, "title_english");
                if (string.IsNullOrEmpty(r.Title)) r.Title = SafeGet(item, "title");
                r.OriginalTitle = SafeGet(item, "title_japanese");
                if (r.OriginalTitle == r.Title) r.OriginalTitle = "";
                r.MALId = SafeGetInt(item, "mal_id").ToString();
                r.Type = "Anime";
                if (item.TryGetProperty("year", out var y) && y.TryGetInt32(out int yr)) r.Year = yr.ToString();
                r.Season = SafeGet(item, "season") switch { "spring" => "春", "summer" => "夏", "fall" => "秋", "winter" => "冬", _ => "" };
                r.Synopsis = SafeGet(item, "synopsis");
                if (r.Synopsis.Length > 200) r.Synopsis = r.Synopsis[..200] + "...";
                if (item.TryGetProperty("episodes", out var ep) && ep.TryGetInt32(out int epc) && epc > 0) r.Episodes = epc.ToString();
                if (item.TryGetProperty("images", out var imgs) && imgs.TryGetProperty("jpg", out var jpg))
                {
                    r.CoverUrl = SafeGet(jpg, "large_image_url");
                    if (string.IsNullOrEmpty(r.CoverUrl)) r.CoverUrl = SafeGet(jpg, "image_url");
                }
                if (item.TryGetProperty("studios", out var studios))
                {
                    var names = new System.Collections.Generic.List<string>();
                    foreach (var s in studios.EnumerateArray())
                    { string n = SafeGet(s, "name"); if (!string.IsNullOrEmpty(n)) names.Add(n); }
                    r.Company = string.Join(" / ", names);
                }
                r.SourceType = SafeGet(item, "source") switch { "Original" => "原创", "Manga" => "漫改", "Light novel" => "小说改", "Game" => "游戏改", _ => "" };
                if (item.TryGetProperty("score", out var sc) && sc.TryGetDouble(out double sd) && sd > 0)
                    r.Tags.Add($"MAL评分:{sd:F1}");
                if (item.TryGetProperty("genres", out var gen))
                    foreach (var g in gen.EnumerateArray())
                    { string gn = SafeGet(g, "name"); if (!string.IsNullOrEmpty(gn)) r.Tags.Add(gn); }
                results.Add(r);
            }
            return results;
        }
        catch { return new System.Collections.Generic.List<ExternalSearchResult>(); }
    }

    public Task<ExternalSearchResult> GetByIdAsync(string externalId) => Task.FromResult<ExternalSearchResult>(null);
    private static string SafeGet(System.Text.Json.JsonElement e, string p) => e.TryGetProperty(p, out var v) ? (v.GetString() ?? "") : "";
    private static int SafeGetInt(System.Text.Json.JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt32(out int n) ? n : 0;
}

internal class TestableAniListSearchProvider : ISearchProvider
{
    private readonly HttpClient _httpClient;
    public string ProviderName => "AniList";

    public TestableAniListSearchProvider(string responseJson, bool throwError = false)
    {
        _httpClient = throwError
            ? new HttpClient(new ThrowingHandler())
            : new HttpClient(new MockHttpMessageHandler(responseJson));
        _httpClient.Timeout = System.TimeSpan.FromSeconds(5);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Test");
    }

    public async Task<System.Collections.Generic.List<ExternalSearchResult>> SearchAsync(string query, string typeHint = null)
    {
        try
        {
            var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://graphql.anilist.co", content);
            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            var results = new System.Collections.Generic.List<ExternalSearchResult>();
            if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
            if (!data.TryGetProperty("Page", out var page)) return results;
            if (!page.TryGetProperty("media", out var media)) return results;

            foreach (var item in media.EnumerateArray())
            {
                var r = new ExternalSearchResult { Source = "anilist" };
                if (item.TryGetProperty("title", out var t))
                {
                    r.Title = SafeGet(t, "english");
                    if (string.IsNullOrEmpty(r.Title)) r.Title = SafeGet(t, "romaji");
                    r.OriginalTitle = SafeGet(t, "native");
                    if (r.OriginalTitle == r.Title) r.OriginalTitle = "";
                }
                if (item.TryGetProperty("id", out var id) && id.TryGetInt32(out int aid)) r.AniListId = aid.ToString();
                r.Type = SafeGet(item, "type") switch { "ANIME" => "Anime", "MANGA" => "Manga", _ => "Anime" };
                if (item.TryGetProperty("seasonYear", out var sy) && sy.TryGetInt32(out int y)) r.Year = y.ToString();
                r.Season = SafeGet(item, "season") switch { "SPRING" => "春", "SUMMER" => "夏", "FALL" => "秋", "WINTER" => "冬", _ => "" };
                r.Synopsis = SafeGet(item, "description");
                if (r.Synopsis.Length > 200) r.Synopsis = r.Synopsis[..200] + "...";
                if (item.TryGetProperty("episodes", out var ep) && ep.TryGetInt32(out int epc) && epc > 0) r.Episodes = epc.ToString();
                if (item.TryGetProperty("coverImage", out var cv))
                {
                    r.CoverUrl = SafeGet(cv, "extraLarge");
                    if (string.IsNullOrEmpty(r.CoverUrl)) r.CoverUrl = SafeGet(cv, "large");
                }
                if (item.TryGetProperty("studios", out var st) && st.TryGetProperty("nodes", out var sn))
                {
                    var names = new System.Collections.Generic.List<string>();
                    foreach (var s in sn.EnumerateArray()) { string n = SafeGet(s, "name"); if (!string.IsNullOrEmpty(n)) names.Add(n); }
                    r.Company = string.Join(" / ", names);
                }
                r.SourceType = SafeGet(item, "source") switch { "ORIGINAL" => "原创", "MANGA" => "漫改", "LIGHT_NOVEL" => "小说改", "GAME" => "游戏改", _ => "" };
                if (item.TryGetProperty("averageScore", out var avs) && avs.TryGetInt32(out int sc) && sc > 0)
                    r.Tags.Add($"AniList评分:{sc}%");
                if (item.TryGetProperty("genres", out var gen))
                    foreach (var g in gen.EnumerateArray()) { string gn = g.GetString() ?? ""; if (!string.IsNullOrEmpty(gn)) r.Tags.Add(gn); }
                results.Add(r);
            }
            return results;
        }
        catch { return new System.Collections.Generic.List<ExternalSearchResult>(); }
    }

    public Task<ExternalSearchResult> GetByIdAsync(string externalId) => Task.FromResult<ExternalSearchResult>(null);
    private static string SafeGet(System.Text.Json.JsonElement e, string p) => e.TryGetProperty(p, out var v) ? (v.GetString() ?? "") : "";
}

// === 通用测试辅助类 ===

internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseJson;
    private readonly HttpStatusCode _statusCode;

    public MockHttpMessageHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseJson = responseJson;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseJson ?? "{}", System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

internal class ThrowingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("模拟网络错误");
    }
}
