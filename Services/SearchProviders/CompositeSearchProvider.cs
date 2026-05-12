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

        public CompositeSearchProvider()
        {
            _providers = new List<ISearchProvider>
            {
                new BangumiSearchProvider(),
                new MALSearchProvider(),
                new AniListSearchProvider()
            };
        }

        /// <summary>
        /// 跨多个数据源搜索作品
        /// </summary>
        /// <param name="query">用户搜索词</param>
        /// <param name="typeHint">类型提示 (Anime/Manga/Game/LightNovel)</param>
        /// <param name="maxResults">最大结果数</param>
        public async Task<List<ExternalSearchResult>> SearchAsync(string query, string typeHint = null, int maxResults = 8)
        {
            var allResults = new List<ExternalSearchResult>();
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in _providers)
            {
                try
                {
                    var results = await provider.SearchAsync(query, typeHint);
                    foreach (var result in results)
                    {
                        string key = (result.Title + result.OriginalTitle).ToLowerInvariant();
                        if (!seenTitles.Contains(key))
                        {
                            seenTitles.Add(key);
                            allResults.Add(result);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[CompositeSearch] {provider.ProviderName} 搜索失败: {ex.Message}");
                }
            }

            return allResults.Take(maxResults).ToList();
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
                if (!string.IsNullOrEmpty(r.Episodes) && r.Episodes != "0") info.Add($"   集数: {r.Episodes}");
                if (!string.IsNullOrEmpty(r.Synopsis)) info.Add($"   简介: {r.Synopsis}");

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
