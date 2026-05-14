using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AniTechou.Services
{
    /// <summary>
    /// 从 Bangumi 同步追番状态到本地
    /// </summary>
    public class SyncService
    {
        private readonly string _accountName;
        private readonly HttpClient _http;

        public SyncService(string accountName)
        {
            _accountName = accountName;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.Add("User-Agent",
                "AniTechou/1.0 (https://github.com/lunatic1157/AniTechou)");
        }

        public class SyncResult
        {
            public bool Success { get; set; }
            public int NewWorks { get; set; }
            public int UpdatedWorks { get; set; }
            public int SkippedWorks { get; set; }
            public int Unmatched { get; set; }
            public string ErrorMessage { get; set; } = "";
            public List<string> Details { get; set; } = new List<string>();
        }

        // === Bangumi ===

        public async Task<SyncResult> SyncFromBangumiAsync(string username)
        {
            var result = new SyncResult();
            if (string.IsNullOrWhiteSpace(username))
            {
                result.ErrorMessage = "请输入 Bangumi 用户名";
                return result;
            }

            try
            {
                int offset = 0;
                int limit = 50;
                var allItems = new List<BangumiCollectionItem>();

                // 分页拉取全部收藏
                while (true)
                {
                    string url = $"https://api.bgm.tv/v0/users/{Uri.EscapeDataString(username)}/collections?limit={limit}&offset={offset}";
                    System.Diagnostics.Debug.WriteLine($"[SyncService] Bangumi 请求: {url}");

                    var response = await _http.GetAsync(url);
                    var json = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SyncService] Bangumi 错误响应: {json?.Substring(0, Math.Min(500, json?.Length ?? 0))}");
                        result.ErrorMessage = $"Bangumi 返回 {response.StatusCode}：用户名可能不对（应填 bgm.tv/user/ 后面的英文ID）";
                        return result;
                    }

                    using var doc = JsonDocument.Parse(json);

                    // Bangumi v0 API: 收藏接口返回 { data: [...], total: N }
                    if (doc.RootElement.TryGetProperty("total", out var totalEl) && totalEl.GetInt32() == 0)
                    {
                        result.ErrorMessage = "Bangumi 账户没有公开收藏，或用户名不存在";
                        return result;
                    }

                    if (!doc.RootElement.TryGetProperty("data", out var dataArray) ||
                        dataArray.ValueKind != JsonValueKind.Array)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SyncService] Bangumi 响应结构异常: {json?.Substring(0, Math.Min(300, json?.Length ?? 0))}");
                        result.ErrorMessage = "Bangumi 返回数据格式异常，请确认用户名是否正确";
                        return result;
                    }

                    int count = 0;
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        count++;
                        // Bangumi v0 API: subject 是子对象 { id, name, name_cn, type, ... }
                        int subjectId = 0;
                        string name = "", nameCn = "", subjectType = "Anime";
                        if (item.TryGetProperty("subject", out var subjectEl) && subjectEl.ValueKind == JsonValueKind.Object)
                        {
                            subjectId = SafeGetInt(subjectEl, "id");
                            name = SafeGetString(subjectEl, "name");
                            nameCn = SafeGetString(subjectEl, "name_cn");
                            int subType = SafeGetInt(subjectEl, "type");
                            subjectType = SearchProviders.BangumiSearchProvider.MapBangumiType(subType);
                        }

                        // 解析用户个人标签（Bangumi v0 API: tags 是字符串数组）
                        var userTags = new List<string>();
                        if (item.TryGetProperty("tags", out var tagsArray) && tagsArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var t in tagsArray.EnumerateArray())
                            {
                                string tagName = t.ValueKind == JsonValueKind.String
                                    ? t.GetString()
                                    : SafeGetString(t, "name");
                                if (!string.IsNullOrWhiteSpace(tagName))
                                    userTags.Add(tagName);
                            }
                        }

                        allItems.Add(new BangumiCollectionItem
                        {
                            SubjectId = subjectId,
                            Name = name,
                            NameCn = nameCn,
                            Type = subjectType,
                            Status = SafeGetInt(item, "type").ToString(),
                            Rate = SafeGetInt(item, "rate"),
                            EpStatus = SafeGetInt(item, "ep_status"),
                            UpdatedAt = SafeGetString(item, "updated_at"),
                            Tags = userTags
                        });
                    }

                    if (count < limit) break;
                    offset += limit;
                    await Task.Delay(500); // 限速
                }

                System.Diagnostics.Debug.WriteLine($"[SyncService] Bangumi 获取 {allItems.Count} 条收藏");

                result = await ApplyBangumiResultsAsync(allItems);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"同步失败：{ex.Message}";
            }

            return result;
        }

        private async Task<SyncResult> ApplyBangumiResultsAsync(List<BangumiCollectionItem> items)
        {
            var result = new SyncResult();
            var workService = new WorkService(_accountName);
            var allWorks = workService.GetAllWorksForSearch();

            foreach (var item in items)
            {
                string searchName = item.NameCn ?? item.Name;
                System.Diagnostics.Debug.WriteLine($"[SyncService] Bangumi 匹配: {searchName} (id={item.SubjectId})");

                // 1) 按 BangumiId 精确匹配
                var match = allWorks.FirstOrDefault(w =>
                    !string.IsNullOrEmpty(w.BangumiId) && w.BangumiId == item.SubjectId.ToString());
                // 2) 中文名精确
                if (match == null && !string.IsNullOrEmpty(item.NameCn))
                    match = allWorks.FirstOrDefault(w =>
                        !string.IsNullOrEmpty(w.Title) && w.Title == item.NameCn);
                // 3) 日文名精确
                if (match == null && !string.IsNullOrEmpty(item.Name))
                    match = allWorks.FirstOrDefault(w =>
                        !string.IsNullOrEmpty(w.Title) && w.Title == item.Name)
                        ?? allWorks.FirstOrDefault(w =>
                        !string.IsNullOrEmpty(w.OriginalTitle) && w.OriginalTitle == item.Name);
                // 4) 模糊：中文名包含关系
                if (match == null && !string.IsNullOrEmpty(item.NameCn) && item.NameCn.Length >= 3)
                    match = allWorks.FirstOrDefault(w =>
                        !string.IsNullOrEmpty(w.Title) && (w.Title.Contains(item.NameCn) || item.NameCn.Contains(w.Title)));
                // 5) 模糊：日文名包含关系
                if (match == null && !string.IsNullOrEmpty(item.Name) && item.Name.Length >= 3)
                    match = allWorks.FirstOrDefault(w =>
                        !string.IsNullOrEmpty(w.Title) && (w.Title.Contains(item.Name) || item.Name.Contains(w.Title)));

                if (match == null)
                {
                    // 没匹配到 → 自动从 Bangumi 导入作品详情，再设置状态
                    if (item.SubjectId > 0)
                    {
                        try
                        {
                            var bgmProvider = new SearchProviders.BangumiSearchProvider();
                            var detail = await bgmProvider.GetByIdAsync(item.SubjectId.ToString());
                            if (detail != null)
                            {
                                string displayTitle = item.NameCn ?? detail.Title;
                                int bgmStatusInt = int.TryParse(item.Status, out int s) ? s : 1;
                                string mappedStatus = MapBangumiStatus(bgmStatusInt);
                                int newId = workService.AddWork(
                                    displayTitle, detail.OriginalTitle, detail.Type,
                                    detail.Company, detail.Year, detail.Season ?? "",
                                    detail.SourceType ?? "", detail.Episodes ?? "",
                                    "", mappedStatus,
                                    item.Rate,
                                    detail.Synopsis ?? "", "", detail.Author ?? "",
                                    detail.OriginalWork ?? "", item.SubjectId.ToString());

                                if (newId > 0)
                                {
                                    // Save Bangumi tags (public tags from detail + user's personal tags from item)
                                    if (detail.Tags != null)
                                    {
                                        foreach (var tag in detail.Tags)
                                        {
                                            if (!string.IsNullOrWhiteSpace(tag))
                                                workService.AddWorkTag(newId, tag, "Bangumi");
                                        }
                                    }
                                    if (item.Tags != null)
                                    {
                                        foreach (var tag in item.Tags)
                                        {
                                            if (!string.IsNullOrWhiteSpace(tag) && (detail.Tags == null || !detail.Tags.Contains(tag)))
                                                workService.AddWorkTag(newId, tag, "Bangumi");
                                        }
                                    }
                                    // 下载封面
                                    if (!string.IsNullOrEmpty(detail.CoverUrl))
                                        _ = workService.DownloadAndSaveCoverAsync(
                                            string.IsNullOrEmpty(detail.BangumiId) ? detail.CoverUrl
                                            : $"bgm_id:{detail.BangumiId}|{detail.CoverUrl}", newId);

                                    result.NewWorks++;
                                    result.Details.Add($"新增: {displayTitle} → {MapStatusDisplay(mappedStatus)}");
                                    continue;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SyncService] 自动导入失败 {searchName}: {ex.Message}");
                        }
                    }

                    result.Unmatched++;
                    result.Details.Add($"未匹配: {searchName}");
                    continue;
                }

                var userWork = workService.GetUserWorkByWorkId(match.Id);
                if (userWork == null) continue;

                int bgmStatusInt2 = int.TryParse(item.Status, out int s2) ? s2 : 1;
                string newStatus = MapBangumiStatus(bgmStatusInt2);
                double newRating = item.Rate > 0 ? item.Rate : userWork.Rating;

                bool changed = userWork.Status != newStatus || userWork.Rating != newRating;
                if (changed)
                {
                    workService.UpdateUserWork(userWork.Id, newStatus, userWork.Progress, newRating);
                    if (string.IsNullOrEmpty(match.BangumiId) && item.SubjectId > 0)
                        workService.UpdateWorkBangumiId(match.Id, item.SubjectId.ToString());
                    // Save user tags
                    if (item.Tags != null)
                    {
                        foreach (var tag in item.Tags)
                        {
                            if (!string.IsNullOrWhiteSpace(tag))
                                workService.AddWorkTag(match.Id, tag, "Bangumi");
                        }
                    }
                    result.UpdatedWorks++;
                    result.Details.Add($"更新: {item.NameCn ?? item.Name} → {MapStatusDisplay(newStatus)}");
                }
                else
                {
                    result.SkippedWorks++;
                }
            }

            result.Success = true;
            return result;
        }

        private static string MapBangumiStatus(int bgmCollectionType) => bgmCollectionType switch
        {
            1 => "wish",       // 想看
            2 => "done",       // 看过
            3 => "doing",      // 在看
            4 => "on_hold",    // 搁置
            5 => "dropped",    // 抛弃
            _ => "wish"
        };

        private static string MapStatusDisplay(string status) => status switch
        {
            "wish" => "想看",
            "doing" => "在看",
            "done" => "看过",
            "on_hold" => "搁置",
            "dropped" => "抛弃",
            _ => status
        };

        // === Helpers ===

        private static string SafeGetString(JsonElement el, string key)
        {
            if (!el.TryGetProperty(key, out var prop)) return "";
            return prop.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => "",
                JsonValueKind.String => prop.GetString() ?? "",
                JsonValueKind.Number => prop.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => ""
            };
        }

        private static int SafeGetInt(JsonElement el, string key)
        {
            if (!el.TryGetProperty(key, out var prop) || prop.ValueKind == JsonValueKind.Null) return 0;
            if (prop.TryGetInt32(out int v)) return v;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out int sv)) return sv;
            return 0;
        }

        private class BangumiCollectionItem
        {
            public int SubjectId { get; set; }
            public string Name { get; set; } = "";
            public string NameCn { get; set; } = "";
            public string Type { get; set; } = "";
            public string Status { get; set; } = "";
            public int Rate { get; set; }
            public int EpStatus { get; set; }
            public string UpdatedAt { get; set; } = "";
            public List<string> Tags { get; set; } = new List<string>();
        }
    }
}
