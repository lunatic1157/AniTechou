using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AniTechou.Services.SearchProviders
{
    /// <summary>
    /// 聚合搜索提供者 — 合并多个搜索源的搜索结果
    /// 按优先级: Bangumi > AniList > MAL
    /// </summary>
    public class CompositeSearchProvider
    {
        private readonly List<ISearchProvider> _providers;

        // 简单内存缓存：key → (results, expireTime)
        private static readonly Dictionary<string, (List<ExternalSearchResult> results, DateTime expireAt)> _cache = new();
        private static readonly TimeSpan CacheTTL = TimeSpan.FromMinutes(10);

        public CompositeSearchProvider(
            bool enableBangumi = true,
            bool enableMAL = false,
            bool enableAniList = false)
        {
            _providers = new List<ISearchProvider>();

            if (enableBangumi)
                _providers.Add(new BangumiSearchProvider());
            if (enableMAL)
                _providers.Add(new MALSearchProvider());
            if (enableAniList)
                _providers.Add(new AniListSearchProvider());

            // 始终至少保留一个搜索源
            if (_providers.Count == 0)
                _providers.Add(new BangumiSearchProvider());
        }

        /// <summary>
        /// 跨多个数据源搜索作品（并发执行，按优先级: Bangumi > AniList > MAL）
        /// </summary>
        /// <param name="query">用户搜索词</param>
        /// <param name="typeHint">类型提示 (Anime/Manga/Game/LightNovel)</param>
        /// <param name="maxResults">最大结果数</param>
        public async Task<List<ExternalSearchResult>> SearchAsync(string query, string typeHint = null, int maxResults = 8)
        {
            // 缓存命中 → 直接返回
            string cacheKey = $"{query}|{typeHint ?? "all"}|{maxResults}";
            lock (_cache)
            {
                if (_cache.TryGetValue(cacheKey, out var entry) && DateTime.Now < entry.expireAt)
                {
                    System.Diagnostics.Debug.WriteLine($"[CompositeSearch] 缓存命中: {query}");
                    return entry.results;
                }
            }

            List<ExternalSearchResult> allResults;

            if (_providers.Count == 1)
            {
                try
                {
                    allResults = (await _providers[0].SearchAsync(query, typeHint)).Take(maxResults).ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CompositeSearch] {_providers[0].ProviderName} 搜索失败: {ex.Message}");
                    allResults = new List<ExternalSearchResult>();
                }
            }
            else
            {
                // 多源并发搜索
                var tasks = _providers.Select(p => SearchProviderSafe(p, query, typeHint)).ToArray();
                var resultsArrays = await Task.WhenAll(tasks);

                // 合并去重（保持优先级顺序）
                allResults = new List<ExternalSearchResult>();
                var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < resultsArrays.Length; i++)
                {
                    foreach (var result in resultsArrays[i])
                    {
                        string key = (result.Title + result.OriginalTitle).ToLowerInvariant();
                        if (!seenTitles.Contains(key))
                        {
                            seenTitles.Add(key);
                            allResults.Add(result);
                        }
                    }
                }

                allResults = allResults.Take(maxResults).ToList();
            }

            // 写入缓存
            if (allResults.Count > 0)
            {
                lock (_cache)
                {
                    _cache[cacheKey] = (allResults, DateTime.Now.Add(CacheTTL));
                }
            }

            return allResults;
        }

        private static async Task<List<ExternalSearchResult>> SearchProviderSafe(
            ISearchProvider provider, string query, string typeHint)
        {
            try
            {
                return await provider.SearchAsync(query, typeHint);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CompositeSearch] {provider.ProviderName} 搜索失败: {ex.Message}");
                return new List<ExternalSearchResult>();
            }
        }

        /// <summary>
        /// 根据 Bangumi ID 获取作品详情
        /// </summary>
        public async Task<ExternalSearchResult> GetByBangumiIdAsync(string bangumiId)
        {
            var bgmProvider = _providers.OfType<BangumiSearchProvider>().FirstOrDefault();
            if (bgmProvider != null)
                return await bgmProvider.GetByIdAsync(bangumiId);
            return null;
        }

        /// <summary>
        /// 将搜索结果格式化为 LLM 可用的上下文文本
        /// </summary>
        public static string FormatForLLMPrompt(List<ExternalSearchResult> results)
        {
            if (results == null || results.Count == 0)
                return "";

            var lines = new List<string>
            {
                "【以下是从 ACGN 数据库实时搜索到的作品信息，请基于这些真实数据回答用户】",
                ""
            };

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var info = new List<string>
                {
                    $"{i + 1}. 《{r.Title}》",
                    $"   原名: {r.OriginalTitle}",
                    $"   类型: {r.Type}",
                    $"   年份: {r.Year}",
                    $"   Bangumi ID: {r.BangumiId}",
                };

                if (!string.IsNullOrEmpty(r.Company)) info.Add($"   制作公司: {r.Company}");
                if (!string.IsNullOrEmpty(r.Author)) info.Add($"   作者: {r.Author}");
                if (!string.IsNullOrEmpty(r.OriginalWork)) info.Add($"   原作: {r.OriginalWork}");
                if (!string.IsNullOrEmpty(r.SourceType)) info.Add($"   原作类型: {r.SourceType}");
                if (!string.IsNullOrEmpty(r.Episodes) && r.Episodes != "0") info.Add($"   集数: {r.Episodes}");
                if (!string.IsNullOrEmpty(r.Synopsis)) info.Add($"   简介: {r.Synopsis}");
                if (!string.IsNullOrEmpty(r.VoiceActorInfo)) info.Add($"   声优: {r.VoiceActorInfo}");

                string coverHint = !string.IsNullOrEmpty(r.BangumiId)
                    ? $"bgm_id:{r.BangumiId}|{r.CoverUrl}"
                    : r.CoverUrl;
                if (!string.IsNullOrEmpty(coverHint)) info.Add($"   封面/ID: {coverHint}");

                if (r.Tags.Count > 0) info.Add($"   标签: {string.Join(", ", r.Tags)}");

                lines.AddRange(info);
                lines.Add("");
            }

            lines.Add("【请在上面的真实搜索数据中选择最匹配用户需求的作品，并按要求返回 JSON】");
            return string.Join("\n", lines);
        }
    }
}
