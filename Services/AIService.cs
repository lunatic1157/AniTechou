using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AniTechou.Services
{
    public class AIService
    {
        private readonly string _apiKey;
        private readonly string _apiUrl;
        private readonly string _model;
        private readonly HttpClient _httpClient;

        public AIService()
        {
            var config = ConfigManager.Load();
            _apiKey = config.ApiKey;
            _apiUrl = config.ApiUrl;
            _model = config.Model;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public AIService(string apiKey, string apiUrl, string model)
        {
            _apiKey = apiKey;
            _apiUrl = apiUrl;
            _model = model;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        /// <summary>
        /// 测试连接
        /// </summary>
        public async Task<bool> TestConnection()
        {
            try
            {
                var request = new
                {
                    model = _model,
                    messages = new[] { new { role = "user", content = "hi" } },
                    max_tokens = 5
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync($"{_apiUrl}/chat/completions", content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<AIWorkSearchResult>> SearchWorks(string query)
        {
            return await BatchSearchWorks(query);
        }

        /// <summary>
        /// 批量作品搜索推荐
        /// </summary>
        public async Task<List<AIWorkSearchResult>> BatchSearchWorks(string query)
        {
            var prompt = $@"请根据用户的需求，返回符合条件的作品列表。要求：
1. 返回JSON数组格式
2. 每个作品包含：
   - title(标题)
   - originalTitle(原名可选)
   - type(类型:Anime/Manga/LightNovel/Game)
   - year(年份可选，仅数字，如2023)
   - company(制作公司可选)
   - coverUrl(封面图片URL，优先使用高质量大图)
   - sourceType(原作类型，必须是以下之一：原创, 漫改, 小说改, 游戏改, 其他)
   - season(季度，必须是以下之一：春, 夏, 秋, 冬)
3. 如果用户指定了制作公司（如""京阿尼""），只返回该公司的作品
4. 如果用户指定了年份范围，只返回该年份的作品
5. 返回5-10个最相关的作品

用户需求：{query}";

            try
            {
                var request = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = "你是一个ACGN专家，必须返回JSON格式的数据" },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync($"{_apiUrl}/chat/completions", content);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var resultText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                // 提取JSON部分
                int start = resultText.IndexOf("[");
                int end = resultText.LastIndexOf("]");
                if (start >= 0 && end > start)
                {
                    resultText = resultText.Substring(start, end - start + 1);
                }

                return JsonSerializer.Deserialize<List<AIWorkSearchResult>>(resultText) ?? new List<AIWorkSearchResult>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchWorks异常: {ex.Message}");
                return new List<AIWorkSearchResult>();
            }
        }

        /// <summary>
        /// 智能对话 - 自动识别意图并返回结构化数据
        /// </summary>
        public async Task<AIResponse> SmartChat(string userMessage)
        {
            var prompt = $@"你是一个集成了最新搜索能力的ACGN专家助手。你的知识库实时更新，能够准确识别2026年及以前的最新番剧、漫画、轻小说和游戏。

当前日期：{DateTime.Now:yyyy-MM-dd}

根据用户的问题，判断意图并返回相应格式。

意图类型：
1. WORK_SEARCH - 用户想要搜索、推荐、查找作品（请务必提供最新作品信息，如果用户提到""今年""或""最近""）
2. INFO_QUERY - 用户询问关于作品的具体信息
3. WORK_UPDATE - 用户想要更新某个作品的状态、进度、评分或基本信息（例如：'把芙莉莲的集数改成28集'，'完善芙莉莲的季度和封面'）
4. GENERAL_CHAT - 普通聊天

如果是 WORK_SEARCH，返回3-5个作品。
如果是 WORK_UPDATE，提取作品名称和想要更新的字段。如果用户要求“完善信息”或“更新信息”，请尽可能补全该作品的所有缺失字段（如季度、封面、简介等）。
如果是 INFO_QUERY 或 GENERAL_CHAT，返回回答。

字段规范：
- sourceType: 必须是以下之一：原创, 漫改, 小说改, 游戏改, 其他
- season: 必须是以下之一：春, 夏, 秋, 冬
- coverUrl: 必须返回真实有效的图片URL，优先从常见动漫数据库(如Bangumi, MyAnimeList)的CDN地址获取。

返回JSON格式：
{{
  ""intent"": ""WORK_SEARCH"",
  ""works"": [{{ ""title"": """", ""originalTitle"": """", ""type"": """", ""year"": """", ""season"": """", ""company"": """", ""sourceType"": """", ""episodes"": """", ""synopsis"": """", ""coverUrl"": """", ""tags"": [] }}]
}}
或
{{
  ""intent"": ""WORK_UPDATE"",
  ""updateInfo"": {{ 
    ""title"": ""作品名"", 
    ""updates"": {{
      ""status"": ""wish/doing/done"",
      ""progress"": ""12/24"",
      ""rating"": ""9"",
      ""episodes"": ""24"",
      ""season"": ""春"",
      ""sourceType"": ""漫改"",
      ""coverUrl"": ""https://..."",
      ""synopsis"": ""...""
    }}
  }},
  ""answer"": ""已为你准备好更新信息""
}}
或
{{
  ""intent"": ""INFO_QUERY"",
  ""answer"": """"
}}
或
{{
  ""intent"": ""GENERAL_CHAT"",
  ""answer"": """"
}}

用户问题：{userMessage}";

            try
            {
                var request = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = "你是ACGN专家，返回JSON格式" },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.3
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync($"{_apiUrl}/chat/completions", content);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var resultText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                resultText = resultText.Replace("```json", "").Replace("```", "").Trim();

                return JsonSerializer.Deserialize<AIResponse>(resultText) ?? new AIResponse { intent = "GENERAL_CHAT", answer = "抱歉，我无法理解你的问题" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SmartChat异常: {ex.Message}");
                return new AIResponse { intent = "GENERAL_CHAT", answer = "抱歉，出了点问题，请稍后再试" };
            }
        }
    }

    public class AIWorkSearchResult
    {
        public string title { get; set; } = "";
        public string originalTitle { get; set; } = "";
        public string type { get; set; } = "Anime";
        public string year { get; set; } = "";
        public string company { get; set; } = "";
        public string episodes { get; set; } = "";
        public string synopsis { get; set; } = "";
        public List<string> tags { get; set; } = new List<string>();
        public string coverUrl { get; set; } = "";
        public string sourceType { get; set; } = "";
        public string season { get; set; } = "";
    }

    public class AIWorkSearchResponse
    {
        public List<AIWorkSearchResult> works { get; set; } = new List<AIWorkSearchResult>();
    }

    public class AIResponse
    {
        public string intent { get; set; } = "GENERAL_CHAT";
        public string answer { get; set; } = "";
        public List<AIWorkSearchResult> works { get; set; } = new List<AIWorkSearchResult>();
        public AIUpdateInfo updateInfo { get; set; }
    }

    public class AIUpdateInfo
    {
        public string title { get; set; } = "";
        public Dictionary<string, string> updates { get; set; } = new Dictionary<string, string>();
    }
}
