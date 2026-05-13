using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AniTechou.Services
{
    /// <summary>
    /// 从 Bangumi / Bilibili 同步追番状态到本地
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

                    if (!doc.RootElement.TryGetProperty("data", out var dataArray))
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
                            subjectType = subType switch { 2 => "Anime", 3 => "Anime", _ => "Anime" };
                        }

                        allItems.Add(new BangumiCollectionItem
                        {
                            SubjectId = subjectId,
                            Name = name,
                            NameCn = nameCn,
                            Type = subjectType,
                            Status = SafeGetString(item, "type"),
                            Rate = SafeGetInt(item, "rate"),
                            EpStatus = SafeGetInt(item, "ep_status"),
                            UpdatedAt = SafeGetString(item, "updated_at")
                        });
                    }

                    if (count < limit) break;
                    offset += limit;
                    await Task.Delay(500); // 限速
                }

                System.Diagnostics.Debug.WriteLine($"[SyncService] Bangumi 获取 {allItems.Count} 条收藏");

                result = ApplyBangumiResults(allItems);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"同步失败：{ex.Message}";
            }

            return result;
        }

        private SyncResult ApplyBangumiResults(List<BangumiCollectionItem> items)
        {
            var result = new SyncResult();
            var workService = new WorkService(_accountName);
            var allWorks = workService.GetAllWorksForSearch();

            foreach (var item in items)
            {
                // 优先用 Bangumi ID 匹配
                var match = allWorks.FirstOrDefault(w =>
                    !string.IsNullOrEmpty(w.BangumiId) && w.BangumiId == item.SubjectId.ToString());
                // 退而求其次按标题匹配
                if (match == null)
                    match = allWorks.FirstOrDefault(w =>
                        !string.IsNullOrEmpty(w.Title) && w.Title == item.NameCn)
                        ?? allWorks.FirstOrDefault(w =>
                        !string.IsNullOrEmpty(w.Title) && w.Title == item.Name);

                if (match == null)
                {
                    result.Unmatched++;
                    result.Details.Add($"未匹配: {item.NameCn ?? item.Name}");
                    continue;
                }

                var userWork = workService.GetUserWorkByWorkId(match.Id);
                if (userWork == null) continue;

                string newStatus = MapBangumiStatus(item.Status);
                int newRating = item.Rate > 0 ? item.Rate * 2 : userWork.Rating; // Bangumi 1-10 → 2-10

                bool changed = userWork.Status != newStatus || userWork.Rating != newRating;
                if (changed)
                {
                    workService.UpdateUserWork(userWork.Id, newStatus, userWork.Progress, newRating);
                    // 有 BangumiId 就更新到作品
                    if (string.IsNullOrEmpty(match.BangumiId) && item.SubjectId > 0)
                        workService.UpdateWorkBangumiId(match.Id, item.SubjectId.ToString());
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

        private static string MapBangumiStatus(string bgmType) => bgmType switch
        {
            "wish" => "wish",
            "do" => "doing",
            "collect" => "done",
            "on_hold" => "doing",
            "dropped" => "wish",
            _ => "wish"
        };

        private static string MapStatusDisplay(string status) => status switch
        {
            "wish" => "想看",
            "doing" => "在看",
            "done" => "看过",
            _ => status
        };

        // === Bilibili ===

        public async Task<SyncResult> SyncFromBilibiliAsync(string uid)
        {
            var result = new SyncResult();
            if (string.IsNullOrWhiteSpace(uid))
            {
                result.ErrorMessage = "请输入 B站 UID";
                return result;
            }

            try
            {
                var allItems = new List<BilibiliBangumiItem>();
                int pn = 1;

                while (true)
                {
                    string url = $"https://api.bilibili.com/x/space/bangumi/follow/list?vmid={uid}&type=1&pn={pn}&ps=50";
                    System.Diagnostics.Debug.WriteLine($"[SyncService] B站 请求: {url}");

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Referer", "https://space.bilibili.com/");
                    var response = await _http.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        result.ErrorMessage = $"B站 API 返回 {response.StatusCode}，UID 可能设有隐私";
                        return result;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() != 0)
                    {
                        string msg = "未知错误";
                        if (doc.RootElement.TryGetProperty("message", out var msgEl)) msg = msgEl.GetString();
                        result.ErrorMessage = $"B站 API 错误: {msg}";
                        return result;
                    }

                    if (!doc.RootElement.TryGetProperty("data", out var data)) break;
                    if (!data.TryGetProperty("list", out var list)) break;

                    int count = 0;
                    foreach (var item in list.EnumerateArray())
                    {
                        count++;
                        allItems.Add(new BilibiliBangumiItem
                        {
                            SeasonId = SafeGetInt(item, "season_id"),
                            Title = SafeGetString(item, "title"),
                            Cover = SafeGetString(item, "cover"),
                            FollowStatus = SafeGetInt(item, "follow_status"), // 1=想看 2=在看 3=看过
                            Progress = SafeGetString(item, "progress"),
                            NewEpId = SafeGetString(item, "new_ep"),
                            Evaluate = SafeGetString(item, "evaluate"),
                            Rating = SafeGetInt(item, "rating") // B站评分 1-10
                        });
                    }

                    if (count < 50) break;
                    pn++;
                    await Task.Delay(500);
                }

                System.Diagnostics.Debug.WriteLine($"[SyncService] B站 获取 {allItems.Count} 条追番");

                result = ApplyBilibiliResults(allItems);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"同步失败：{ex.Message}";
            }

            return result;
        }

        private SyncResult ApplyBilibiliResults(List<BilibiliBangumiItem> items)
        {
            var result = new SyncResult();
            var workService = new WorkService(_accountName);
            var allWorks = workService.GetAllWorksForSearch();

            foreach (var item in items)
            {
                // 按标题匹配（B站返回中文标题）
                var match = allWorks.FirstOrDefault(w =>
                    !string.IsNullOrEmpty(w.Title) && w.Title == item.Title)
                    ?? allWorks.FirstOrDefault(w =>
                    !string.IsNullOrEmpty(w.OriginalTitle) && w.OriginalTitle == item.Title);

                // 模糊匹配：B站标题通常包含季节信息如"咒术回战 第二季"
                if (match == null && item.Title.Length > 2)
                {
                    string shortTitle = item.Title.Split(' ', '第', '（', '(').FirstOrDefault() ?? "";
                    if (shortTitle.Length >= 2)
                        match = allWorks.FirstOrDefault(w =>
                            !string.IsNullOrEmpty(w.Title) && w.Title.StartsWith(shortTitle, StringComparison.OrdinalIgnoreCase));
                }

                if (match == null)
                {
                    result.Unmatched++;
                    result.Details.Add($"未匹配: {item.Title}");
                    continue;
                }

                var userWork = workService.GetUserWorkByWorkId(match.Id);
                if (userWork == null) continue;

                string newStatus = MapBilibiliStatus(item.FollowStatus);
                int newRating = item.Rating > 0 ? item.Rating * 2 : userWork.Rating;
                string newProgress = !string.IsNullOrEmpty(item.Progress) ? item.Progress : userWork.Progress;

                bool changed = userWork.Status != newStatus
                    || userWork.Rating != newRating
                    || userWork.Progress != newProgress;

                if (changed)
                {
                    workService.UpdateUserWork(userWork.Id, newStatus, newProgress, newRating);
                    result.UpdatedWorks++;
                    result.Details.Add($"更新: {item.Title} → {MapStatusDisplay(newStatus)}");
                }
                else
                {
                    result.SkippedWorks++;
                }
            }

            result.Success = true;
            return result;
        }

        private static string MapBilibiliStatus(int followStatus) => followStatus switch
        {
            1 => "wish",  // 想看
            2 => "doing", // 在看
            3 => "done",  // 看过
            _ => "wish"
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
            if (el.TryGetProperty(key, out var prop) && prop.ValueKind != JsonValueKind.Null && prop.TryGetInt32(out int v))
                return v;
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
        }

        private class BilibiliBangumiItem
        {
            public int SeasonId { get; set; }
            public string Title { get; set; } = "";
            public string Cover { get; set; } = "";
            public int FollowStatus { get; set; }
            public string Progress { get; set; } = "";
            public string NewEpId { get; set; } = "";
            public string Evaluate { get; set; } = "";
            public int Rating { get; set; }
        }
    }
}
