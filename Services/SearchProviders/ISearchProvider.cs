using System.Collections.Generic;
using System.Threading.Tasks;

namespace AniTechou.Services.SearchProviders
{
    /// <summary>
    /// 外部 ACGN 数据库搜索提供者接口
    /// </summary>
    public interface ISearchProvider
    {
        /// <summary>搜索作品，返回匹配列表</summary>
        Task<List<ExternalSearchResult>> SearchAsync(string query, string typeHint = null);

        /// <summary>根据外部 ID 获取作品详情</summary>
        Task<ExternalSearchResult> GetByIdAsync(string externalId);

        /// <summary>提供者名称（用于日志和结果标记）</summary>
        string ProviderName { get; }
    }
}
