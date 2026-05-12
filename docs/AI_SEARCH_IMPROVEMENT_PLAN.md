# AniTechou AI 搜索改进计划

## 当前状态

三层次改进代码已提交到 main 分支（commit `a26777c`），但**尚未在 Windows 上编译验证**（VM 无 .NET SDK）。

## 已完成的改动

### 第1层 — 真实 API 搜索集成 ✅

**新增文件：**
- `Services/SearchProviders/ISearchProvider.cs` — 搜索提供者接口
- `Services/SearchProviders/SearchProviderModels.cs` — 统一搜索结果模型（`ExternalSearchResult`）
- `Services/SearchProviders/BangumiSearchProvider.cs` — Bangumi API 调用
  - 搜索: `api.bgm.tv/search/subject/{query}`
  - 详情: `api.bgm.tv/v0/subjects/{id}`
  - 提供类型、年份、封面、标签、制作信息等结构化数据
- `Services/SearchProviders/CompositeSearchProvider.cs` — 聚合多源搜索结果
  - `SearchAsync()` — 跨源搜索并去重
  - `GetByBangumiIdAsync()` — 按 ID 获取详情
  - `FormatForLLMPrompt()` — 格式化为 LLM prompt 上下文

**修改文件：**
- `Services/AIService.cs` — 核心改动
  - `BatchSearchWorks()` — 调用 LLM 前先搜索 Bangumi API，注入真实数据
  - `GetEnhancedWorkInfo()` — 同上，先查 API 再让 LLM 补全
  - `SmartChat()` — 对话中检测搜索意图，自动预搜索
  - 新增 `NeedsExternalSearch()` — 意图检测辅助方法
  - 系统 prompt 中新增 `{SEARCH_CONTEXT}` 占位符

### 第2层 — 增强数据模型 ✅

**修改文件：**
- `Models/WorkInfo.cs` — 新增字段：`BangumiId`, `MALId`, `AniListId`, `VoiceActorInfo`
- `Services/WorkService.cs`
  - `AddWork()` 新增可选参数：`bangumiId`, `malId`, `anilistId`, `voiceActorInfo`
  - 新增 `UpdateWorkBangumiId()` 方法
  - `GetWorkById()` SELECT 包含新字段
- `Services/DatabaseHelper.cs`
  - 建表语句包含新列
  - 新增 `MigrateWorksExternalIds()` 自动迁移旧数据库
- `Views/AIBatchAddView.xaml.cs` — `AIBatchWorkItem` 新增 `BangumiId`，添加时自动保存
- `MainWindow.xaml.cs` — `HandleWorkUpdate` 中 bangumiId 现在真实保存到数据库

### 第3层 — DeepSeek 联网搜索 ✅

**修改文件：**
- `Services/ConfigManager.cs` — `AppConfig` 新增 `EnableWebSearch` 属性
- `Services/AIService.cs` — `SmartChat()` 检测 DeepSeek 平台时添加 `enable_search: true`
- `Views/SettingsView.xaml` — 新增"启用 DeepSeek 联网搜索"复选框
- `Views/SettingsView.xaml.cs` — 读取/保存 `EnableWebSearch` 设置

## 下一步：编译验证

在项目目录下运行：

```powershell
dotnet build -c Release
```

如果编译通过，运行测试：

```powershell
dotnet test Tests\AniTechou.Tests.csproj -c Release
```

常见可能的编译问题：
1. `BangumiSearchProvider.cs` 中 infobox 解析的 JSON 逻辑 — 需实际测试 API 返回格式
2. `AIBatchAddView.xaml.cs` 中 `AddWork()` 调用 — 参数顺序是否匹配
3. `SmartChat()` 中 `enable_search` 参数 — 只有 DeepSeek 支持，其他平台会忽略

## 后续可扩展方向

### 短期（下一个迭代）
- [ ] 编译验证 + 修复编译错误
- [ ] 手动测试：搜索一部作品验证 API 数据是否注入
- [ ] 手动测试：DeepSeek 联网搜索是否生效
- [ ] 添加更多搜索源：AniListSearchProvider, MALSearchProvider
- [ ] 添加 `ISearchProvider` 的单元测试

### 中期
- [ ] 拆分 `WorkService.cs`（125KB 过重）→ `WorkQueryService`, `WorkImportExportService`, `CoverService`
- [ ] AIService 添加重试/超时机制
- [ ] `SmartChat` 的 `_contextHistory` 从 static 改为实例变量（当前多账号会串对话）
- [ ] 设置页添加搜索源选择（Bangumi/AniList/MAL 开关）

### 长期
- [ ] 作品详情页显示外部链接（Bangumi/MAL/AniList 直达链接）
- [ ] 自动定期同步作品信息（监测 Bangumi 更新）
- [ ] 封面下载优化：缓存机制、并发下载
- [ ] CI/CD：GitHub Actions 自动构建 + 发布

## 技术笔记

- Bangumi API 无需认证，但有速率限制（建议每次请求间隔 1s）
- Bangumi v0 API infobox 格式：`{"key": "动画制作", "value": [{"v": "京都动画"}]}`
- Jikan API 也有速率限制（每秒 3 请求）
- DeepSeek `enable_search` 参数仅 DeepSeek 平台生效，其他平台会静默忽略
- 新增的外部 ID 字段对旧数据库完全向后兼容（自动迁移，默认空字符串）
