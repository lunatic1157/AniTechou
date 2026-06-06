using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AniTechou.Services.SearchProviders;
using AniTechou.Utilities;

namespace AniTechou.Services
{
    public class AIService
    {
        private readonly string _apiKey;
        private readonly string _apiUrl;
        private readonly string _model;
        private readonly HttpClient _httpClient;
        private readonly CompositeSearchProvider _searchProvider;
        private readonly bool _enableWebSearch;

        // 用于保存对话上下文（多轮对话）
        private List<ChatMessage> _contextHistory = new List<ChatMessage>();
        private const int MAX_HISTORY = 10;

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
            _enableWebSearch = config.EnableWebSearch;

            // DeepSeek API 响应慢（尤其是高峰时段），给足时间
            double timeoutSec = config.EnableWebSearch ? 120 : 60;
            _httpClient = NetworkClientFactory.CreateHttpClient(TimeSpan.FromSeconds(timeoutSec));
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }

            _searchProvider = new CompositeSearchProvider(
                config.EnableBangumiSearch,
                config.EnableMALSearch,
                config.EnableAniListSearch);
        }

        public static string GetDefaultSystemPrompt()
        {
            return $@"你是 AniTechou 的 AI 助手，既能搜索推荐 ACGN 作品，也能管理本地收藏。当前日期：{DateTime.Now:yyyy-MM-dd}

=== 数据源 ===
【本地收藏】{{USER_COLLECTION_CONTEXT}}
【在线搜索】{{SEARCH_CONTEXT}}

=== 核心规则 ===
1. 只返回 JSON，不要 Markdown 标记。
2. 🔴 在线搜索数据中的标题/类型/封面/原作是真实数据，必须原样采用。
3. 🔴 coverUrl 用 bgm_id 格式。禁止自编 URL。没有真实数据就说未找到。
4. 🔴 推荐新作品时必须用【在线搜索】数据，且排除【本地收藏】中已存在的作品。
5. 🔴 本地管理(统计/口味分析/查重)时必须用【本地收藏】数据回答。
6. works 条目含 17 个字段(无则为""): title, originalTitle, type, year, season, company, author, originalWork, sourceType, episodes, synopsis, coverUrl, bangumiId, tags, voiceActorInfo, recommendationCategory, recommendationReason
   - author: 漫画/轻小说作者；originalWork: 改编作品的原著；coverUrl: ""bgm_id:{{id}}|{{url}}""
   - sourceType: 原创/漫改/小说改/游戏改/其他；season: 春/夏/秋/冬
   - recommendationCategory: 只允许 相似推荐/口味推荐/拓展推荐/补课推荐
   - recommendationReason: 1-2 句具体理由，必须说明使用了哪些信号，不要泛泛说“很好看”
7. 标签只写有筛选价值的口味/题材/人员标签。禁止把年份、评分、制作公司、季度、状态、类型、原作类型写进 tags。
8. 人员标签必须用统一前缀：cv:声优名、导演:姓名、脚本:姓名、音乐:姓名、角色设计:姓名、原作:姓名、插画:姓名。
9. 推荐作品要给出不同理由类型：相似推荐、口味推荐、拓展推荐、补课推荐；理由要说明用到了本地高分作品、常见标签、人员偏好或类型偏好中的哪些信号。

=== 意图分类 ===
- WORK_SEARCH: 搜索/推荐新作品(用在线搜索) → works 数组
- WORK_UPDATE: 更新本地作品信息 → updateInfo
  · 定向更新: 指定作品名，只改用户提的字段
  · 全量补全: ""补全/完善所有"" → isBatchUpdate=true
  · 标签统一: ""A改成B"" → action:""TAG_UNIFY""
- WORK_DELETE: 删除本地作品
- NOTE_CREATE/SEARCH/UPDATE: 笔记操作
- GENERAL_CHAT: 闲聊/问答/本地统计/口味分析

=== 本地管理 ===
- 统计类(几部/多少/有哪些) → 统计【本地收藏】数据
- 口味分析/查重/进度 → 基于【本地收藏】
- 🔴 推荐新作品绝不能推荐已经在【本地收藏】里的

示例：{{ ""intent"":""WORK_SEARCH"", ""answer"":""找到以下作品"", ""works"":[{{
  ""title"":""葬送的芙莉莲"", ""originalTitle"":""葬送のフリーレン"", ""type"":""Anime"",
  ""year"":""2023"", ""season"":""秋"", ""company"":""MADHOUSE"", ""author"":"""", ""originalWork"":""山田钟人"",
  ""sourceType"":""漫改"", ""episodes"":""28"", ""synopsis"":"""", ""coverUrl"":""bgm_id:364291|"",
  ""bangumiId"":""364291"", ""tags"":[""治愈"",""奇幻"",""原作:山田钟人""], ""voiceActorInfo"":"""",
  ""recommendationCategory"":""口味推荐"", ""recommendationReason"":""基于你常看的治愈、奇幻和高分冒险作品推荐。""
  }}], ""updateInfo"":null }}";
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

        /// <summary>
        /// 判断用户输入是否可能需要外部搜索
        /// </summary>
        private static bool NeedsExternalSearch(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            string lower = message.ToLower();
            // 搜索意图关键词
            string[] searchKeywords = {
                "搜索", "搜", "推荐", "找", "有没有", "有哪些", "什么",
                "哪些", "介绍", "看看", "帮我", "想要", "有没有",
                "search", "find", "recommend", "suggest",
                "动漫", "动画", "漫画", "番剧", "游戏", "轻小说",
                "anime", "manga", "game", "novel"
            };

            foreach (var kw in searchKeywords)
            {
                if (lower.Contains(kw)) return true;
            }

            // 如果消息中包含书名号，很可能是作品相关查询
            if (message.Contains("《") || message.Contains("》")) return true;

            return false;
        }

        /// <summary>
        /// 带重试的 LLM API 调用
        /// </summary>
        private async Task<string> PostToLLMAsync(object request, int maxRetries = 2)
        {
            return await RetryHelper.RetryAsync(async () =>
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(request, request.GetType()),
                    Encoding.UTF8,
                    "application/json");
                var response = await _httpClient.PostAsync($"{_apiUrl}/chat/completions", content);
                return await response.Content.ReadAsStringAsync();
            }, "LLM API", maxRetries);
        }

        // API 可能返回 HTML 错误页而非 JSON
        private static bool IsValidJsonResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            string trimmed = json.TrimStart();
            return trimmed.StartsWith("{");
        }

        /// <summary>
        /// 从自然语言消息中提取搜索关键词（去掉口语词）
        /// </summary>
        private static string ExtractSearchKeywords(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return message;

            // 去掉所有口语/问句/语气词，只留内容词
            string[] stopWords = {
                "有什么", "有没有", "能不能", "可以", "帮我", "我想", "我想要",
                "好看的", "值得看", "推荐几部", "推荐一些", "介绍", "给我",
                "哪些", "怎么", "为什么", "是什么", "意思是", "请问",
                "告诉我", "想了解", "想知道", "请", "一下", "一些",
                "推荐", "搜索", "看看", "想要",
                "吗", "呢", "啊", "吧", "了", "的", "哈", "哦",
                "这", "那", "个", "部", "些", "和", "与", "或",
                "年", "中", "里", "上", "下", "前", "后"
            };

            string result = message;
            foreach (var w in stopWords)
                result = result.Replace(w, " ");

            // 去掉标点
            result = result.Replace("《", " ").Replace("》", " ")
                           .Replace("？", " ").Replace("！", " ")
                           .Replace("，", " ").Replace("。", " ");

            // 合并空格
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

            return result.Length >= 2 ? result : message;
        }

        /// <summary>
        /// 构建 LLM 请求体，根据平台自适应注入联网搜索参数
        /// 每个分支返回独立匿名类型，确保 JSON 序列化时属性不丢失
        /// </summary>
        private object BuildRequest(List<object> messages)
        {
            // 对话回复可能包含多个作品，需要更多 token
            const int maxTokens = 4096;

            if (!_enableWebSearch)
            {
                return new
                {
                    model = _model,
                    messages = messages,
                    temperature = 0.3,
                    response_format = new { type = "json_object" },
                    max_tokens = maxTokens
                };
            }

            // DeepSeek: enable_search 参数
            if (_apiUrl.Contains("deepseek"))
            {
                System.Diagnostics.Debug.WriteLine("[AIService] 联网搜索: DeepSeek enable_search");
                return new
                {
                    model = _model,
                    messages = messages,
                    temperature = 0.3,
                    response_format = new { type = "json_object" },
                    max_tokens = maxTokens,
                    enable_search = true
                };
            }

            // Kimi (Moonshot): 使用 web_search 工具
            if (_apiUrl.Contains("moonshot") || _apiUrl.Contains("kimi"))
            {
                System.Diagnostics.Debug.WriteLine("[AIService] 联网搜索: Kimi web_search tool");
                return new
                {
                    model = _model,
                    messages = messages,
                    temperature = 0.3,
                    response_format = new { type = "json_object" },
                    max_tokens = maxTokens,
                    tools = new[]
                    {
                        new
                        {
                            type = "builtin_function",
                            function = new { name = "web_search" }
                        }
                    }
                };
            }

            // OpenAI 兼容: web_search 工具
            if (_apiUrl.Contains("openai"))
            {
                System.Diagnostics.Debug.WriteLine("[AIService] 联网搜索: OpenAI web_search tool");
                return new
                {
                    model = _model,
                    messages = messages,
                    temperature = 0.3,
                    response_format = new { type = "json_object" },
                    max_tokens = maxTokens,
                    tools = new[]
                    {
                        new
                        {
                            type = "web_search",
                            web_search = new { }
                        }
                    }
                };
            }

            // 未知平台：尝试 DeepSeek 兼容参数（多数国内 API 兼容）
            System.Diagnostics.Debug.WriteLine("[AIService] 联网搜索: 未知平台，尝试通用 enable_search");
            return new
            {
                model = _model,
                messages = messages,
                temperature = 0.3,
                response_format = new { type = "json_object" },
                max_tokens = maxTokens,
                enable_search = true
            };
        }

        public async Task<List<AIWorkSearchResult>> SearchWorks(string query)
        {
            return await BatchSearchWorks(query);
        }

        public static string BuildRecommendationPrompt(
            string query,
            string searchContext,
            string recommendationProfileContext)
        {
            string profileContext = string.IsNullOrWhiteSpace(recommendationProfileContext)
                ? "（未提供本地画像信号）"
                : recommendationProfileContext;
            string externalContext = string.IsNullOrWhiteSpace(searchContext)
                ? "（无外部搜索数据）"
                : searchContext;

            return $@"请根据用户需求和本地画像信号，返回符合条件的 ACGN 作品推荐列表。要求：
1. 返回 JSON 数组格式。
2. 每个作品包含 title、originalTitle、type、year、company、author、originalWork、coverUrl、bangumiId、sourceType、season、voiceActorInfo、tags、recommendationCategory、recommendationReason。
3. recommendationCategory 只允许 相似推荐/口味推荐/拓展推荐/补课推荐。
4. recommendationReason 必须说明使用了哪些本地画像信号，不要泛泛说“很好看”。
5. 四类推荐的使用方式：
   - 相似推荐: 说明与哪些高评分作品或看过/在看作品相似。
   - 口味推荐: 说明命中了哪些常见普通标签、类型偏好或人员标签。
   - 拓展推荐: 说明从既有口味延伸到了哪个新方向。
   - 补课推荐: 说明它为什么是经典、缺口或值得补的作品。
6. 必须排除本地收藏中已经存在的作品。
7. 返回 5-10 个最相关作品，尽量覆盖 相似推荐、口味推荐、拓展推荐、补课推荐。

{profileContext}

【外部搜索数据】
{externalContext}

用户需求：{query}";
        }

        public static List<AIWorkSearchResult> ExcludeExistingWorks(
            List<AIWorkSearchResult> candidates,
            List<WorkService.WorkCardData> localWorks)
        {
            if (candidates == null || candidates.Count == 0)
                return new List<AIWorkSearchResult>();
            if (localWorks == null || localWorks.Count == 0)
                return candidates;

            return candidates
                .Where(candidate => !localWorks.Any(local =>
                    WorkDataRules.IsSameWork(
                        local.Title,
                        local.OriginalTitle,
                        local.Type,
                        candidate.title,
                        candidate.originalTitle,
                        candidate.type)))
                .ToList();
        }

        /// <summary>
        /// 批量作品搜索推荐（第1层改进：先搜索真实 API）
        /// </summary>
        public async Task<List<AIWorkSearchResult>> BatchSearchWorks(string query)
        {
            // === 第1层改进：预搜索真实 ACGN 数据库（5 秒超时，慢则跳过） ===
            string searchContext = "";
            try
            {
                string kw = ExtractSearchKeywords(query);
                var searchTask = _searchProvider.SearchAsync(kw, null, 10, skipCache: _enableWebSearch);
                if (await Task.WhenAny(searchTask, Task.Delay(5000)) == searchTask)
                {
                    var externalResults = await searchTask;
                    searchContext = CompositeSearchProvider.FormatForLLMPrompt(externalResults);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[AIService] 外部搜索超时(5s)，降级为纯 LLM");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AIService] 外部搜索失败，降级为纯 LLM: {ex.Message}");
            }

            var prompt = $@"请根据用户的需求，返回符合条件的作品列表。要求：
1. 返回JSON数组格式
2. 每个作品包含以下字段（有信息就填，没有则空字符串）：
   - title(标题)
   - originalTitle(原名可选)
   - type(类型:Anime/Manga/LightNovel/Game)
   - year(年份可选，仅数字，如2023)
   - company(制作公司，动画填公司、漫画/轻小说填出版社)
   - author(作者，漫画/轻小说必填，动画可选)
   - originalWork(原作名，改编作品必填原著作名称)
   - coverUrl(封面图片URL，优先使用下方真实数据)
   - bangumiId(Bangumi ID，优先使用下方真实数据)
   - sourceType(原作类型，必须是以下之一：原创, 漫改, 小说改, 游戏改, 其他)
   - season(季度，必须是以下之一：春, 夏, 秋, 冬)
   - voiceActorInfo(主要声优，格式: ""角色A(CV:声优A)、角色B(CV:声优B)"")
   - tags(标签数组，只放口味/题材/人员标签；人员必须使用 cv:/导演:/脚本:/音乐:/角色设计:/原作:/插画: 前缀；不要放年份、评分、公司、季度、状态、类型、原作类型)
   - recommendationCategory(推荐分类: 相似推荐/口味推荐/拓展推荐/补课推荐)
   - recommendationReason(推荐理由: 说明相似作品、常见口味、人员偏好、高分作品或补课缺口等具体信号)
3. 如果用户指定了制作公司（如""京阿尼""），只返回该公司的作品
4. 如果用户指定了年份范围，只返回该年份的作品
5. 返回5-10个最相关的作品；推荐时尽量覆盖相似推荐、口味推荐、拓展推荐、补课推荐，并填写 recommendationCategory / recommendationReason

{searchContext}

用户需求：{query}";

            prompt = BuildRecommendationPrompt(query, searchContext, "");

            try
            {
                var request = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = "你是一个ACGN专家，必须返回JSON格式的数据。如果提供了实时搜索数据，必须优先使用其中的真实作品信息。" },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7,
                    max_tokens = 2000
                };

                var json = await PostToLLMAsync(request);

                if (!IsValidJsonResponse(json))
                    return new List<AIWorkSearchResult>();

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
        /// 专门用于完善特定作品的详细信息（第1层改进：从真实 API 获取数据）
        /// </summary>
        public async Task<AIWorkSearchResult> GetEnhancedWorkInfo(string title, string originalTitle, string type)
        {
            // === 第1层改进：预搜索真实数据 ===
            string searchContext = "";
            try
            {
                string searchQuery = !string.IsNullOrEmpty(originalTitle) ? originalTitle : title;
                var externalResults = await _searchProvider.SearchAsync(searchQuery, type, 3, skipCache: _enableWebSearch);
                if (externalResults.Count > 0)
                {
                    // 如果找到精确匹配的 Bangumi ID，获取详细信息
                    var bestMatch = externalResults[0];
                    if (!string.IsNullOrEmpty(bestMatch.BangumiId))
                    {
                        var detail = await _searchProvider.GetByBangumiIdAsync(bestMatch.BangumiId);
                        if (detail != null)
                            externalResults[0] = detail;
                    }
                }
                searchContext = CompositeSearchProvider.FormatForLLMPrompt(externalResults);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AIService] GetEnhancedWorkInfo 外部搜索失败: {ex.Message}");
            }

            var prompt = $@"你是一个ACGN专家。请为以下作品提供最准确、最新的详细信息。
作品标题：{title}
原名：{originalTitle}
类型：{type}

{searchContext}

关键要求：
1. **优先使用上方实时搜索数据中的信息**。
2. 如果实时数据中有 Bangumi ID，必须将其填入 bangumiId 字段。
3. coverUrl 填入格式: ""bgm_id:{{bangumiId}}|{{封面URL}}""。

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
  ""bangumiId"": ""优先使用实时数据中的 Bangumi ID"",
  ""tags"": [""标签1"", ""标签2""],
  ""recommendationCategory"": ""口味推荐"",
  ""recommendationReason"": ""基于用户常见口味或指定作品相似性给出具体理由""
}}

字段规范：
- sourceType: 必须是以下之一：原创, 漫改, 小说改, 游戏改, 其他
- season: 必须是以下之一：春, 夏, 秋, 冬
- bangumiId: 填入真实的 Bangumi 数字 ID（从上方实时数据获取）
- tags: 不要写年份、评分、制作公司、季度、状态、类型、原作类型；声优/导演/脚本/音乐/角色设计/原作/插画必须使用标准前缀
";

            try
            {
                var request = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = "你是一个ACGN专家，必须返回纯JSON格式。优先使用实时数据中的真实信息。" },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.3,
                    max_tokens = 2000
                };

                var json = await PostToLLMAsync(request);

                using var doc = JsonDocument.Parse(json);
                var resultText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

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
        /// 智能对话 - 自动识别意图并返回结构化数据（第1层改进：注入实时搜索上下文）
        /// </summary>
        public async Task<AIResponse> SmartChat(string userMessage, string userCollectionContext = "")
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 1. 更新上下文
            _contextHistory.Add(new ChatMessage { role = "user", content = userMessage });
            if (_contextHistory.Count > MAX_HISTORY) _contextHistory.RemoveAt(0);

            var config = ConfigManager.Load();
            string systemPromptTemplate = string.IsNullOrEmpty(config.CustomSystemPrompt)
                ? GetDefaultSystemPrompt()
                : config.CustomSystemPrompt;

            // === 第1层改进：预搜索真实数据 ===
            string searchContext = "";
            if (NeedsExternalSearch(userMessage))
            {
                try
                {
                    // 提取关键词而非整句查询（"2025年有什么值得看的新番"→"2025 新番"）
                    string searchQuery = ExtractSearchKeywords(userMessage);
                    System.Diagnostics.Debug.WriteLine($"[AIService] 搜索关键词: {searchQuery}");
                    var externalResults = await _searchProvider.SearchAsync(searchQuery, null, 8, skipCache: _enableWebSearch);
                    if (externalResults.Count > 0)
                    {
                        searchContext = CompositeSearchProvider.FormatForLLMPrompt(externalResults);
                        System.Diagnostics.Debug.WriteLine(
                            $"[AIService] SmartChat 预搜索获得 {externalResults.Count} 条结果");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AIService] SmartChat 外部搜索失败: {ex.Message}");
                }
            }

            var systemPrompt = systemPromptTemplate
                .Replace("{DateTime.Now:yyyy-MM-dd}", DateTime.Now.ToString("yyyy-MM-dd"))
                .Replace("{SEARCH_CONTEXT}", string.IsNullOrEmpty(searchContext) ? "（无实时搜索数据，请使用你的训练知识）" : searchContext)
                .Replace("{USER_COLLECTION_CONTEXT}", string.IsNullOrEmpty(userCollectionContext) ? "（未提供）" : userCollectionContext);

            try
            {
                var messages = new List<object> { new { role = "system", content = systemPrompt } };
                foreach (var msg in _contextHistory)
                {
                    messages.Add(new { role = msg.role, content = msg.content });
                }

                // === 第3层改进：平台自适应联网搜索 ===
                var request = BuildRequest(messages);

                // 联网搜索不重试：每次调用成本高，重试只会让用户等更久
                int retries = _enableWebSearch ? 0 : 2;
                var json = await PostToLLMAsync(request, retries);

                if (!IsValidJsonResponse(json))
                {
                    System.Diagnostics.Debug.WriteLine($"[AIService] API 返回非 JSON: {json?.Substring(0, Math.Min(200, json?.Length ?? 0))}");
                    return new AIResponse { intent = "GENERAL_CHAT", answer = "服务暂时不可用，请稍后重试。" };
                }

                using var doc = JsonDocument.Parse(json);
                var resultText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                // 剥离可能的 Markdown 代码块
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
                    // JSON 解析失败 → 尝试提取 answer 字段
                    var answerMatch = System.Text.RegularExpressions.Regex.Match(
                        resultText, @"""answer"":\s*""([^""]*(?:\\.[^""]*)*)""");
                    if (answerMatch.Success)
                    {
                        aiResponse = new AIResponse { intent = "GENERAL_CHAT", answer = answerMatch.Groups[1].Value };
                    }
                    else
                    {
                        // 实在不行就显示纯净文本（去掉 JSON 特殊字符）
                        aiResponse = new AIResponse { intent = "GENERAL_CHAT", answer = resultText };
                    }
                }

                if (aiResponse != null)
                {
                    _contextHistory.Add(new ChatMessage { role = "assistant", content = aiResponse.answer });
                    if (_contextHistory.Count > MAX_HISTORY) _contextHistory.RemoveAt(0);
                }

                sw.Stop();
                if (aiResponse != null && !string.IsNullOrEmpty(aiResponse.answer))
                    aiResponse.answer += $"\n\n⏱️ 耗时 {sw.Elapsed.TotalSeconds:F1} 秒";
                return aiResponse ?? new AIResponse { intent = "GENERAL_CHAT", answer = "抱歉，解析回复时出错了。" };
            }
            catch (Exception ex)
            {
                sw.Stop();
                System.Diagnostics.Debug.WriteLine($"SmartChat异常: {ex.Message}");
                bool isTimeout = ex is TaskCanceledException || ex.InnerException is TaskCanceledException;
                string hint = isTimeout && _enableWebSearch
                    ? $"\n\n💡 联网搜索超时（{sw.Elapsed.TotalSeconds:F0}秒），试试在设置里关掉「AI 联网搜索」。"
                    : "";
                return new AIResponse { intent = "GENERAL_CHAT", answer = $"抱歉，出了点问题，请稍后再试。{hint}" };
            }
        }

        /// <summary>
        /// Build a structured summary of the user's entire collection for AI context
        /// </summary>
        public static string BuildCollectionContext(List<WorkService.WorkCardData> allWorks, WorkService workService)
        {
            if (allWorks == null || allWorks.Count == 0)
                return "（用户收藏为空）";

            var sb = new StringBuilder();

            // Summary stats by type
            var byType = allWorks.GroupBy(w => w.Type).ToDictionary(g => g.Key, g => g.Count());
            sb.AppendLine($"用户共有 {allWorks.Count} 部作品：");
            foreach (var kv in byType)
            {
                string typeName = kv.Key switch
                {
                    "Anime" => "动画", "Manga" => "漫画", "LightNovel" => "轻小说", "Game" => "游戏", _ => kv.Key
                };
                sb.AppendLine($"  {typeName}: {kv.Value} 部");
            }
            sb.AppendLine();

            var workSnapshots = allWorks
                .Take(Math.Min(allWorks.Count, 100))
                .Select(w => new
                {
                    Work = w,
                    UserWork = workService?.GetUserWorkByWorkId(w.Id),
                    Tags = workService?.GetWorkTags(w.Id) ?? new List<string>()
                })
                .ToList();

            var profile = RecommendationProfile.Build(workSnapshots.Select(x => new RecommendationProfile.WorkSnapshot
            {
                Work = x.Work,
                UserWork = x.UserWork,
                Tags = x.Tags
            }));
            sb.AppendLine(profile.FormatForPrompt());

            var highRated = workSnapshots
                .Where(x => x.UserWork?.Rating >= 8)
                .OrderByDescending(x => x.UserWork.Rating)
                .Take(8)
                .Select(x => $"《{x.Work.Title}》{x.UserWork.Rating:F1}")
                .ToList();
            if (highRated.Count > 0)
                sb.AppendLine($"高分偏好: {string.Join("、", highRated)}");

            var commonTags = workSnapshots
                .SelectMany(x => x.Tags)
                .Where(t => !string.IsNullOrWhiteSpace(t) && !t.Contains(':') && !t.Contains('：'))
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(10)
                .Select(g => $"{g.Key}({g.Count()})")
                .ToList();
            if (commonTags.Count > 0)
                sb.AppendLine($"常见口味标签: {string.Join("、", commonTags)}");

            var peopleTags = workSnapshots
                .SelectMany(x => x.Tags)
                .Where(t => t.StartsWith("cv:", StringComparison.OrdinalIgnoreCase) ||
                            t.StartsWith("导演:", StringComparison.Ordinal) ||
                            t.StartsWith("脚本:", StringComparison.Ordinal) ||
                            t.StartsWith("音乐:", StringComparison.Ordinal) ||
                            t.StartsWith("角色设计:", StringComparison.Ordinal) ||
                            t.StartsWith("原作:", StringComparison.Ordinal) ||
                            t.StartsWith("插画:", StringComparison.Ordinal))
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(10)
                .Select(g => $"{g.Key}({g.Count()})")
                .ToList();
            if (peopleTags.Count > 0)
                sb.AppendLine($"常见人员/创作偏好: {string.Join("、", peopleTags)}");

            sb.AppendLine("推荐时请排除下方已收藏作品，并说明使用了相似作品、常见口味、人员偏好或补课缺口中的哪些信号。");
            sb.AppendLine();

            // Per-work details (limit to 100 for token budget)
            int limit = workSnapshots.Count;
            sb.AppendLine("作品列表（部分）：");
            for (int i = 0; i < limit; i++)
            {
                var w = workSnapshots[i].Work;
                var userWork = workSnapshots[i].UserWork;
                var tags = workSnapshots[i].Tags;
                string status = userWork?.Status switch
                {
                    "wish" => "想看", "doing" => "在看", "done" => "看过",
                    "on_hold" => "搁置", "dropped" => "抛弃", _ => "?"
                };
                string rating = userWork?.Rating > 0 ? $" 评分{userWork.Rating:F1}" : "";
                string tagStr = tags.Count > 0 ? $" 标签: {string.Join(", ", tags.Take(5))}" : "";
                sb.AppendLine($"  [{GetTypeName(w.Type)}]《{w.Title}》({status}){rating}{tagStr}");
            }

            if (allWorks.Count > 100)
                sb.AppendLine($"  ... 还有 {allWorks.Count - 100} 部作品未列出");

            return sb.ToString();
        }

        private static string GetTypeName(string type) => type switch
        {
            "Anime" => "动画",
            "Manga" => "漫画",
            "LightNovel" => "轻小说",
            "Game" => "游戏",
            _ => type
        };
    }

    public class AIWorkSearchResult
    {
        public string title { get; set; } = "";
        public string originalTitle { get; set; } = "";
        public string type { get; set; } = "Anime";
        public string year { get; set; } = "";
        public string company { get; set; } = "";
        public string author { get; set; } = "";
        public string originalWork { get; set; } = "";
        public string episodes { get; set; } = "";
        public string synopsis { get; set; } = "";
        public List<string> tags { get; set; } = new List<string>();
        public string coverUrl { get; set; } = "";
        public string bangumiId { get; set; } = "";
        public string sourceType { get; set; } = "";
        public string season { get; set; } = "";
        public string voiceActorInfo { get; set; } = "";
        public string recommendationCategory { get; set; } = "";
        public string recommendationReason { get; set; } = "";
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
        public AINoteInfo noteInfo { get; set; }
    }

    public class AINoteInfo
    {
        public string action { get; set; } = "";
        public int noteId { get; set; } = 0;
        public string title { get; set; } = "";
        public string content { get; set; } = "";
        public List<string> tags { get; set; } = new List<string>();
        public List<string> relatedWorks { get; set; } = new List<string>();
        public string searchTerm { get; set; } = "";
    }

    public class AIUpdateInfo
    {
        public string action { get; set; } = "";
        public string title { get; set; } = "";
        public string type { get; set; } = "";
        public bool isBatchUpdate { get; set; } = false;
        public Dictionary<string, string> updates { get; set; } = new Dictionary<string, string>();
        public string targetTag { get; set; } = "";
        public string newTag { get; set; } = "";
    }
}
   
