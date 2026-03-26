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
        
        // 用于保存对话上下文（多轮对话）
        private static List<ChatMessage> _contextHistory = new List<ChatMessage>();
        private const int MAX_HISTORY = 10; // 最多保留5轮对话（10条消息）

        public class ChatMessage
        {
            public string role { get; set; }
            public string content { get; set; }
        }

        public AIService(string apiKey = null, string apiUrl = null, string model = null)
        {
            var config = ConfigManager.Load();
            _apiKey = apiKey ?? config.ApiKey;
            _apiUrl = apiUrl ?? config.ApiUrl;
            _model = model ?? config.Model;
            
            _httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }
        }

        public static string GetDefaultSystemPrompt()
        {
            return $@"你是一个集成了最新搜索能力的ACGN专家助手。
当前日期：{DateTime.Now:yyyy-MM-dd}

用户的个人作品列表：
{{USER_COLLECTION_CONTEXT}}

关键要求：
1. 你必须严格返回 JSON 格式。
2. 混合模式：你可以在 answer 中进行详细文字回答，同时在 works 数组中提供作品卡片数据。
3. **别名识别**：如果用户的提问使用了作品的简称或别名（如“马娘”对应《赛马娘》，“小圆”对应《魔法少女小圆》），请在 title 字段尽量使用其最正式、最通用的全称，以便系统查重匹配。
4. 意图识别：
   - WORK_SEARCH: 搜索、推荐、查找作品。请在 works 中提供完整的信息。
   - WORK_UPDATE: 更新、完善、补全信息。如果你认为用户想更新**单个**或**指定系列**的作品（如“更新咒术回战”），请在 `title` 填入该作品名。如果你认为用户想全量更新**库中所有**作品（如“完善我所有的番剧”），请设置 `isBatchUpdate: true`。如果涉及批量修改/统一标签，请在 updateInfo 中放入 action: ""TAG_UNIFY"", targetTag (要被替换的旧标签), newTag (统一后的新标签)。
    - WORK_DELETE: 删除作品。提取 title。
    - NOTE_CREATE: 快速记事、写笔记。提取 title, content, tags, relatedWorks(关联的作品标题)。
    - NOTE_SEARCH: 查找我的笔记。提取 searchTerm(关键词)。
    - NOTE_UPDATE: 更新或修改已有的笔记。提取 searchTerm(用于定位旧笔记的标题或关键词), title(新标题), content(新内容), tags, relatedWorks。
    - INFO_QUERY/GENERAL_CHAT: 详细文字回答。
 
字段规范：
- **强制要求**：当 intent 为 WORK_SEARCH 时, works 数组中的每个对象必须完整包含以下15个字段：`title`, `originalTitle`, `type` (必须是 Anime/Manga/LightNovel/Game), `year`, `season`, `company`, `author`, `originalWork`, `sourceType`, `episodes`, `synopsis`, `coverUrl`, `bangumiId`, `tags`, `voiceActorInfo`。如果某个字段没有信息，请使用空字符串 """" 作为值。
- **作者与制作公司**：对于 `Anime` 和 `Game`，重点填写 `company`（制作公司）；对于 `Manga` 和 `LightNovel`，重点填写 `author`（漫画家/插画师/执笔者）。
- **原作 (originalWork)**：指的是**原作者**。如果一部作品是改编的（比如小说改漫画，或者小说改动画），请在这里填入**小说原作者**的名字。如果是原创作品，此处留空。
- **原作类型 (sourceType)**：必须是 原创, 漫改, 小说改, 游戏改, 其他, 无 之一。如果是原创动画，填“原创”；如果是小说/漫画本身，填“无”。注意：**“游戏改”仅在作品类型为 Anime 且明确为“改编自游戏”时使用**；作品类型为 Game 时一般应为“无/原创/其他”，不要把 Game 自己标成“游戏改”。
- **封面获取 (coverUrl & bangumiId)**：
  1. 如果你能准确记得作品在 Bangumi (bgm.tv) 的数字 ID，请填入 `bangumiId`。
  2. 如果你不确定 ID 或链接，请在 `coverUrl` 中填入作品的**全称标题**（例如：“魔法少女小圆”），系统会自动根据名字去搜索并下载封面。严禁编造带有随机哈希的假链接。若确实无法提供封面信息，请留空 """"，并在 answer 中说明原因。
- **笔记操作 (noteInfo)**：
  1. 如果 intent 是 NOTE_CREATE，请在 noteInfo 对象中填入 action: ""CREATE"", title, content, tags, relatedWorks。
  2. 如果 intent 是 NOTE_SEARCH，请在 noteInfo 对象中填入 action: ""SEARCH"", searchTerm。
  3. 如果 intent 是 NOTE_UPDATE，请在 noteInfo 对象中填入 action: ""UPDATE"", searchTerm, title(如果需要修改), content(如果需要修改), tags, relatedWorks。
- **声优与角色信息**：voiceActorInfo 字段专门用于存放用户询问的声优及其配音角色信息。
  1. 如果用户询问了声优（如“悠木碧配了什么”），请在该字段填入格式为：“角色名 (声优名)” 的字符串。
  2. **严禁**将角色名放入 tags 数组。
  3. 如果用户没有询问声优信息，该字段请留空 """"。
- **标签生成指南**：tags 数组应包含 3-6 个反映作品核心特征的标签。除了类型标签（如“热血”、“治愈”），**必须**优先提取以下高价值要素作为标签：
  1. **核心主创**：知名导演（如“新海诚”、“山田尚子”）、编剧（如“虚渊玄”、“冈田麿里”）。
  2. **知名声优**：如果某位声优的表现是该作的重要卖点（如“花泽香菜”、“悠木碧”）。
  3. **风格/题材**：独特的艺术风格或小众题材（如“赛博朋克”、“公路片”）。
- type 必须是 Anime, Manga, LightNovel, Game 之一。
- season 必须是 春, 夏, 秋, 冬 之一。
- sourceType 必须是 原创, 漫改, 小说改, 游戏改, 其他, 无 之一。
- updateInfo 字典结构：`title`（指定更新的作品名），`type`（指定更新的作品类型），`isBatchUpdate`（是否更新全库所有作品，布尔值），`updates`（要更新的具体字段，键名：status, progress, rating, episodes, season, sourceType, synopsis, coverUrl, company, author, originalWork, bangumiId）。对于 originalWork，指的是原作者名字。除非用户明确要求更新“原作类型”，否则不要在 updates 里输出 sourceType。对于 coverUrl，如果已有封面且不需要更新，请留空。

返回 JSON 示例：
{{
  ""intent"": ""WORK_SEARCH"",
  ""answer"": ""为你找到以下作品..."",
  ""works"": [{{
    ""title"": ""作品名"", ""originalTitle"": ""原名"", ""type"": ""Anime"", ""year"": ""2024"", ""season"": ""春"", 
    ""company"": ""制作公司"", ""author"": ""执笔者/漫画家"", ""originalWork"": ""原作者名"", ""sourceType"": ""漫改"", ""episodes"": ""12"", ""synopsis"": ""简介内容"", 
    ""coverUrl"": ""https://..."", ""bangumiId"": ""364291"", ""tags"": [""标签1""], ""voiceActorInfo"": ""角色A (悠木碧)""
  }}],
  ""updateInfo"": null
}}";
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
        /// 专门用于完善特定作品的详细信息
        /// </summary>
        public async Task<AIWorkSearchResult> GetEnhancedWorkInfo(string title, string originalTitle, string type)
        {
            var prompt = $@"你是一个ACGN专家。请为以下作品提供最准确、最新的详细信息。
作品标题：{title}
原名：{originalTitle}
类型：{type}

关键要求：
1. 必须提供真实的封面图片 URL。
2. 优先从 Bangumi 获取，格式通常为：https://lain.bgm.tv/pic/cover/l/xx/xx/xxxx.jpg
3. 如果找不到，请尝试 MyAnimeList。

要求返回JSON格式：
{{
  ""title"": ""{title}"",
  ""originalTitle"": ""{originalTitle}"",
  ""type"": ""{type}"",
  ""year"": ""2024"",
  ""season"": ""春"",
  ""company"": ""制作公司"",
  ""sourceType"": ""漫改"",
  ""episodes"": ""12"",
  ""synopsis"": ""50字左右简介"",
  ""coverUrl"": ""优先使用 Bangumi 的图片地址"",
  ""tags"": [""标签1"", ""标签2""]
}}

字段规范：
- sourceType: 必须是以下之一：原创, 漫改, 小说改, 游戏改, 其他
- season: 必须是以下之一：春, 夏, 秋, 冬
";

            try
            {
                var request = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = "你是一个ACGN专家，必须返回纯JSON格式" },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.3
                };

                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_apiUrl}/chat/completions", content);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var resultText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                
                // 更健壮的 JSON 提取逻辑
                int start = resultText.IndexOf("{");
                int end = resultText.LastIndexOf("}");
                if (start >= 0 && end >= start)
                {
                    resultText = resultText.Substring(start, end - start + 1);
                }

                return JsonSerializer.Deserialize<AIWorkSearchResult>(resultText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEnhancedWorkInfo异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 智能对话 - 自动识别意图并返回结构化数据
        /// </summary>
        public async Task<AIResponse> SmartChat(string userMessage, string userCollectionContext = "")
        {
            // 1. 更新上下文
            _contextHistory.Add(new ChatMessage { role = "user", content = userMessage });
            if (_contextHistory.Count > MAX_HISTORY) _contextHistory.RemoveAt(0);

            var config = ConfigManager.Load();
            string systemPromptTemplate = string.IsNullOrEmpty(config.CustomSystemPrompt) ? GetDefaultSystemPrompt() : config.CustomSystemPrompt;
            var systemPrompt = systemPromptTemplate
                .Replace("{DateTime.Now:yyyy-MM-dd}", DateTime.Now.ToString("yyyy-MM-dd"))
                .Replace("{USER_COLLECTION_CONTEXT}", userCollectionContext);

            try
            {
                var messages = new List<object> { new { role = "system", content = systemPrompt } };
                foreach (var msg in _contextHistory)
                {
                    messages.Add(new { role = msg.role, content = msg.content });
                }

                var request = new
                {
                    model = _model,
                    messages = messages,
                    temperature = 0.3,
                    response_format = new { type = "json_object" } // 强制 JSON 输出
                };

                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_apiUrl}/chat/completions", content);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var resultText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                
                // 更健壮的 JSON 提取逻辑
                int start = resultText.IndexOf("{");
                int end = resultText.LastIndexOf("}");
                if (start >= 0 && end >= start)
                {
                    resultText = resultText.Substring(start, end - start + 1);
                }

                AIResponse aiResponse;
                try
                {
                    aiResponse = JsonSerializer.Deserialize<AIResponse>(resultText);
                }
                catch
                {
                    aiResponse = new AIResponse { intent = "GENERAL_CHAT", answer = resultText };
                }
                
                if (aiResponse != null)
                {
                    _contextHistory.Add(new ChatMessage { role = "assistant", content = aiResponse.answer });
                    if (_contextHistory.Count > MAX_HISTORY) _contextHistory.RemoveAt(0);
                }

                return aiResponse ?? new AIResponse { intent = "GENERAL_CHAT", answer = "抱歉，解析回复时出错了。" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SmartChat异常: {ex.Message}");
                return new AIResponse { intent = "GENERAL_CHAT", answer = "抱歉，出了点问题，请稍后再试。" };
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
        public string author { get; set; } = ""; // 漫画/轻小说适用
        public string originalWork { get; set; } = ""; // 动画改编适用，表示原作名称
        public string episodes { get; set; } = "";
        public string synopsis { get; set; } = "";
        public List<string> tags { get; set; } = new List<string>();
        public string coverUrl { get; set; } = "";
        public string bangumiId { get; set; } = ""; // 必填：该作品在 Bangumi(番组计划) 上的数字ID，用于精准获取封面
        public string sourceType { get; set; } = "";
        public string season { get; set; } = "";
        public string voiceActorInfo { get; set; } = ""; // 格式示例: "角色名 (声优名)"
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
        public AINoteInfo noteInfo { get; set; } // 新增笔记信息
    }

    public class AINoteInfo
    {
        public string action { get; set; } = ""; // "CREATE", "SEARCH" 或 "UPDATE"
        public int noteId { get; set; } = 0; // 用于更新笔记时指定 ID
        public string title { get; set; } = "";
        public string content { get; set; } = "";
        public List<string> tags { get; set; } = new List<string>();
        public List<string> relatedWorks { get; set; } = new List<string>(); // 相关作品标题
        public string searchTerm { get; set; } = ""; // 搜索关键词
    }

    public class AIUpdateInfo
    {
        public string action { get; set; } = ""; // 新增，用于区分操作类型，如 TAG_UNIFY
        public string title { get; set; } = "";
        public string type { get; set; } = ""; // 辅助匹配类型 (Anime, Manga 等)
        public bool isBatchUpdate { get; set; } = false; // 新增：明确标识是否为全量更新
        public Dictionary<string, string> updates { get; set; } = new Dictionary<string, string>();
        public string targetTag { get; set; } = ""; // 批量修改标签时的目标标签
        public string newTag { get; set; } = ""; // 批量修改标签时的新标签
    }
}
