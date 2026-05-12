# AniTechou 开发指引

> 本文档是 AniTechou 项目的持续开发说明书。Claude Code 启动后读取此文档，即可了解进度并继续工作。

---

## 会话启动指令

**Claude Code 启动后，请按以下步骤自动执行：**

1. 读取本文件了解当前状态
2. 运行 `dotnet build -c Release` 确认仓库可编译
3. 查看下方「待执行任务」，从第一个未完成项开始
4. 每完成一个任务：构建验证 → 跑测试 → git commit → git push
5. 更新本文件中对应任务的状态和备注

---

## 项目概览

- **项目**：AniTechou — Windows 桌面端 ACGN 作品管理工具
- **技术栈**：WPF + .NET 8 + SQLite + xUnit
- **仓库**：`F:\AniTechou\AniTechouRepo`（GitHub: lunatic1157/AniTechou）
- **用户**：nebura，CS 专业学生，中文交流，偏好渐进式引导
- **当前版本**：v0.9.2+

核心差异点：AI 辅助搜索推荐 + 富文本笔记系统。不是单纯的追番清单。

---

## 架构速查

```
Services/
  AIService.cs           — AI 对话、搜索、意图识别
  WorkService.cs         — 作品 CRUD、导入导出、封面下载（125KB，待拆分）
  ConfigManager.cs       — 配置读写（JSON 落盘到 %LocalAppData%\AniTechou）
  DatabaseHelper.cs      — SQLite 建库、迁移、连接
  AccountManager.cs      — 多账号管理（按账号分库）
  ThemeManager.cs        — 主题切换
  SearchProviders/       — 外部 API 搜索（新模块 v0.9.3+）
    ISearchProvider.cs
    SearchProviderModels.cs
    BangumiSearchProvider.cs
    CompositeSearchProvider.cs

Models/
  WorkInfo.cs            — 作品模型（含外部 ID）
  UserWorkInfo.cs        — 用户作品状态

Views/                   — WPF 视图（Settings, Works, Notes, Profile 等）
MainWindow.xaml.cs       — 主窗口、AI 对话面板、意图分发
```

---

## 已完成：AI 搜索三层次改进（v0.9.3 开发中）

### 第1层 — 真实 API 搜索集成 ✅

新增 SearchProviders 模块。三个核心方法（`BatchSearchWorks`、`GetEnhancedWorkInfo`、`SmartChat`）在调用 LLM 前先查 Bangumi API，把真实数据注入 prompt，LLM 基于真实数据回答而非凭空编造。

关键文件：
- `Services/SearchProviders/ISearchProvider.cs`
- `Services/SearchProviders/SearchProviderModels.cs`
- `Services/SearchProviders/BangumiSearchProvider.cs` — 搜索 `api.bgm.tv/search/subject/{query}`，详情 `api.bgm.tv/v0/subjects/{id}`
- `Services/SearchProviders/CompositeSearchProvider.cs` — 聚合、去重、格式化
- `Services/AIService.cs` — 新增 `NeedsExternalSearch()`，prompt 新增 `{SEARCH_CONTEXT}` 占位符

### 第2层 — 数据模型增强 ✅

`WorkInfo` 新增四个字段：`BangumiId`、`MALId`、`AniListId`、`VoiceActorInfo`。数据库自动迁移旧库。`AddWork()` 新增可选参数。AI 批量添加时自动保存 Bangumi ID。对话更新时真实写入数据库。

关键文件：
- `Models/WorkInfo.cs`
- `Services/WorkService.cs`（`AddWork` 签名变更、新增 `UpdateWorkBangumiId`）
- `Services/DatabaseHelper.cs`（`MigrateWorksExternalIds`）
- `Views/AIBatchAddView.xaml.cs`
- `MainWindow.xaml.cs`（HandleWorkUpdate bangumiid case）

### 第3层 — DeepSeek 联网搜索 ✅

设置页新增「启用 DeepSeek 联网搜索」开关。开启后 `SmartChat` 调用 DeepSeek 时附加 `enable_search: true`，让 LLM 实时联网获取最新作品信息。

关键文件：
- `Services/ConfigManager.cs`（`EnableWebSearch` 属性）
- `Services/AIService.cs`（SmartChat 中条件性添加 `enable_search`）
- `Views/SettingsView.xaml` + `.cs`

---

## 待执行任务

按优先级排列。每个任务完成后需要：构建 → 测试 → commit → push。

### 任务 1：添加重试与超时机制 ✅

**优先级**：P0（影响稳定性）  
**预计**：30 分钟  
**文件**：`Services/AIService.cs`、`Services/SearchProviders/BangumiSearchProvider.cs`

当前 `HttpClient` 调用没有任何重试逻辑，网络波动会导致搜索直接失败（虽有 try-catch 降级到纯 LLM，但非理想）。

**要求**：
1. 在 `AIService` 和 `BangumiSearchProvider` 的 HTTP 调用处，添加最多 2 次重试（指数退避：1s → 2s）
2. 重试仅针对瞬时错误（`HttpRequestException`、`TaskCanceledException`），4xx/5xx 不重试
3. 在 `AIService` 构造函数中设置 `_httpClient.Timeout = TimeSpan.FromSeconds(30)`
4. 添加 Debug 日志记录每次重试

**验收**：`dotnet build -c Release` 通过

---

### 任务 2：修复 SmartChat 上下文串扰 ✅

**优先级**：P0（Bug 修复）  
**预计**：15 分钟  
**文件**：`Services/AIService.cs`

当前 `_contextHistory` 是 `static` 变量。如果用户切换账号，A 账号的对话历史会污染 B 账号的 AI 对话。

**要求**：
1. 将 `_contextHistory` 从 `static` 改为实例变量
2. 确认 `MAX_HISTORY` 常量逻辑不变
3. 检查所有引用处，确保编译通过

**验收**：`dotnet build -c Release` 通过

---

### 任务 3：拆分 WorkService.cs ✅

**优先级**：P1（架构改善）  
**预计**：90 分钟  
**文件**：`Services/WorkService.cs` → 拆分为 3-4 个文件

当前 125KB、3000+ 行，职责混杂（查询、导入导出、封面下载、关联作品全在一起）。

**拆分方案**：
```
Services/
  WorkService.cs              → 保留：CRUD（AddWork, GetWorkById, DeleteWork 等）
  WorkQueryService.cs         → 提取：GetWorksAsync, SearchWorksByNameAsync, GetWorksByTag 等查询方法
  WorkCoverService.cs         → 提取：DownloadAndSaveCoverAsync 及封面降级策略
  WorkImportExportService.cs  → 提取：ImportAllData, ExportAllData, ImportPortableBackup 等
```

**要求**：
1. 每个新类通过构造函数或方法参数接收 `_currentAccount`
2. `WorkService` 可以内部创建其他 Service 实例或保持直接依赖
3. 所有原有调用方（MainWindow、Views）不需改动公共 API 签名
4. 如果某个方法的调用方太多导致改动量大，可以在 `WorkService` 中保留转发方法（`=> _queryService.GetWorksAsync(...)`）

**验收**：
- `dotnet build -c Release` 通过
- `dotnet test Tests\AniTechou.Tests.csproj -c Release` 通过

---

### 任务 4：增加单元测试覆盖 ✅

**优先级**：P1（质量保障）  
**预计**：60 分钟  
**文件**：`Tests/` 目录下新增测试文件

当前仅覆盖导入规则测试。需要补齐：

**要求**：
1. `WorkDataRulesTests.cs`（已有，扩充）：
   - 测试 `IsSameWork` 中日文名混用的去重场景
   - 测试 `NormalizeSourceType` 各种边界输入
2. 新增 `SearchProviderTests.cs`：
   - Mock HttpClient，测试 `BangumiSearchProvider.SearchAsync` 的 JSON 解析逻辑
   - 测试返回空结果、网络异常、格式异常等边界情况
3. 新增 `AIServiceTests.cs`：
   - 测试 `NeedsExternalSearch` 关键词匹配
   - 测试 `GetDefaultSystemPrompt` 包含必需占位符

**验收**：`dotnet test Tests\AniTechou.Tests.csproj -c Release` 全部通过

---

### 任务 5：添加 MAL 搜索源 ✅

**优先级**：P2（功能增强）  
**预计**：45 分钟  
**文件**：新增 `Services/SearchProviders/MALSearchProvider.cs`，修改 `CompositeSearchProvider.cs`

Jikan API（`api.jikan.moe/v4`）是 MyAnimeList 的非官方 REST API，无需认证，有速率限制（3 req/s）。

**要求**：
1. 实现 `ISearchProvider` 接口，`ProviderName = "MAL"`
2. 搜索端点：`GET /v4/anime?q={query}&limit=5`，`GET /v4/manga?q={query}&limit=5`
3. 详情端点：`GET /v4/anime/{id}/full`
4. 在 `CompositeSearchProvider` 构造函数中注册
5. MAL 结果标记 `Source = "mal"`，`MALId` 填入 MAL ID

**验收**：`dotnet build -c Release` 通过

---

### 任务 6：添加 AniList 搜索源 ✅

**优先级**：P2（功能增强）  
**预计**：45 分钟  
**文件**：新增 `Services/SearchProviders/AniListSearchProvider.cs`，修改 `CompositeSearchProvider.cs`

AniList 使用 GraphQL API（`graphql.anilist.co`），无需认证，有速率限制（90 req/min）。

**要求**：
1. 实现 `ISearchProvider` 接口，`ProviderName = "AniList"`
2. 使用 GraphQL POST 请求搜索：
```graphql
query ($search: String) {
  Page(perPage: 5) { media(search: $search, type: ANIME) { id title { romaji english native } ... } }
}
```
3. 在 `CompositeSearchProvider` 构造函数中注册
4. AniList 结果标记 `Source = "anilist"`，`AniListId` 填入 AniList ID

**验收**：`dotnet build -c Release` 通过

---

### 任务 7：设置页搜索源开关 ✅

**优先级**：P3（用户体验）  
**预计**：30 分钟  
**文件**：`Views/SettingsView.xaml`、`SettingsView.xaml.cs`、`Services/ConfigManager.cs`

**要求**：
1. `AppConfig` 新增三个 bool：`EnableBangumiSearch`(默认 true)、`EnableMALSearch`(默认 false)、`EnableAniListSearch`(默认 false)
2. 设置页 AI 区域新增三个 CheckBox
3. `CompositeSearchProvider` 根据配置决定启用哪些搜索源

**验收**：`dotnet build -c Release` 通过

---

### 任务 8：作品详情页外部链接 ✅

**优先级**：P3（用户体验）  
**预计**：30 分钟  
**文件**：`Views/WorkDetailView.xaml`、`WorkDetailView.xaml.cs`

**要求**：
1. 如果作品有 `BangumiId`，在详情页显示「在 Bangumi 查看」按钮，点击打开 `https://bgm.tv/subject/{id}`
2. 同理对 MAL（`https://myanimelist.net/anime/{id}`）和 AniList（`https://anilist.co/anime/{id}`）
3. 使用 `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })` 在默认浏览器打开

**验收**：`dotnet build -c Release` 通过

---

### 任务 9：封面下载缓存 ✅

**优先级**：P3（性能优化）  
**预计**：30 分钟  
**文件**：`Services/WorkService.cs` 或新建 `Services/WorkCoverService.cs`

当前每次查看作品详情都可能重新下载封面。

**要求**：
1. 下载前检查本地封面文件是否已存在（`CoverPath` 非空且文件存在则跳过）
2. 如果 `BangumiId` 已存储，优先用 Bangumi ID 获取封面（跳过标题搜索步骤）
3. 添加 Debug 日志记录封面来源（缓存命中 / Bangumi API / 标题搜索 / MAL 兜底）

**验收**：`dotnet build -c Release` 通过

---

## 技术参考

### Bangumi API

- 搜索：`GET https://api.bgm.tv/search/subject/{query}?responseGroup=large&max_results=8&type={2}`
  - type: 1=书籍, 2=动画, 3=音乐, 4=游戏, 6=三次元
- 详情：`GET https://api.bgm.tv/v0/subjects/{id}`
- 无需认证，建议每次请求间隔 1s
- v0 API infobox 格式：`{"key": "动画制作", "value": [{"v": "京都动画"}]}`

### Jikan API (MyAnimeList)

- 搜索动画：`GET https://api.jikan.moe/v4/anime?q={query}&limit=5`
- 搜索漫画：`GET https://api.jikan.moe/v4/manga?q={query}&limit=5`
- 详情：`GET https://api.jikan.moe/v4/anime/{id}/full`
- 速率限制：3 req/s

### AniList API

- 端点：`POST https://graphql.anilist.co`
- 速率限制：90 req/min
- 需 GraphQL 查询格式

### 构建与测试命令

```powershell
# 构建
dotnet build -c Release

# 测试
dotnet test Tests\AniTechou.Tests.csproj -c Release

# 发布 (self-contained)
dotnet publish AniTechou.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64-self-contained
```

### Git 工作流

```bash
git add <files>
git commit -m "feat: <简短描述>"
git push origin main
```

---

## 变更日志

- 2026-05-12：初版，完成 AI 搜索三层改进并推送
- 2026-05-12：编译验证通过，修复 8 个文件截断问题
- 2026-05-12：**会话 2** — 完成 5 个任务：
  - ✅ 任务 1：重试与超时机制（新增 RetryHelper + AIService 超时 30s）
  - ✅ 任务 2：修复 SmartChat 上下文串扰（_contextHistory 改为实例变量）
  - ✅ 任务 4：新增 42 个单元测试（17→59），覆盖 AIService/SearchProviders/WorkDataRules
  - ✅ 任务 5：MAL 搜索源（Jikan API v4）
  - ✅ 任务 6：AniList 搜索源（GraphQL API）
  - ✅ 任务 7：设置页搜索源开关（Bangumi/MAL/AniList CheckBox，默认 Bangumi 开启）
  - ✅ 任务 8：作品详情页外部链接（Bangumi/MAL/AniList 直达按钮）
  - ✅ 任务 9：封面下载缓存（本地缓存检查 + 数据库 BangumiId 复用）
  - ✅ 任务 3：拆分 WorkService.cs → 3 文件（partial class: ImportExport 814行 + Cover 330行 + 主文件 1704行）
- 2026-05-13：**会话 4** — 平台系统翻新
  - ✅ 平台精简：DeepSeek / Kimi / OpenAI / 自定义（去掉 智谱AI、阿里云百炼）
  - ✅ API 地址和模型改为可编辑 TextBox，编辑即自动切换到自定义平台
  - ✅ 联网搜索平台自适应：DeepSeek(`enable_search`) / Kimi(`web_search tool`) / OpenAI(`web_search tool`) / 未知平台兜底
  - ✅ 修复 PostToLLMAsync 序列化隐患（runtime type 序列化）
