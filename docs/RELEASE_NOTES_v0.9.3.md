# AniTechou v0.9.3 发布说明

## AI 搜索

### 平台系统翻新
- 平台精简为 **DeepSeek / Kimi(月之暗面) / OpenAI / 自定义**，去掉不常用的旧平台
- **API 地址和模型改为可编辑**，修改即自动切换到自定义平台
- **联网搜索平台自适应**：DeepSeek(`enable_search`)、Kimi(`web_search tool`)、OpenAI(`web_search tool`)、未知平台兜底
- 联网搜索默认关闭，开启时显示延迟警告

### 搜索能力增强
- **MAL 搜索源**（Jikan API v4）：动画/漫画搜索、评分、工作室、流派
- **AniList 搜索源**（GraphQL API）：详细元数据、标签、制作人员
- **搜索源开关**：设置页可分别控制 Bangumi/MAL/AniList
- **关键词提取**：搜"2025年有什么值得看的新番"自动提取为"2025 新番"去查 API
- **搜索缓存**：同关键词 1 分钟内秒出，联网搜索自动跳过缓存
- **多源并行搜索**：3 个数据源并发而非串行，速度提升显著
- **12 个快捷提问标签**：推荐/补全/标签/新番/热梗/声优/动画/漫画/游戏/轻小说/进度/查重

### AI 提示词与数据
- **声优/制作人员提取**：Bangumi infobox 自动提取导演、声优、美术、原作等标签
- **原作者字段增强**：搜索和补全时要求 LLM 返回 originalWork/author
- **提示词精炼**：长度缩减 40%，强调「实时数据优先」「定向更新只改用户提到的字段」

## 追番同步 🆕

- **Bangumi 同步**：设置页输入用户名，一键拉取 Bangumi 收藏 → 自动匹配/导入本地
  - 未匹配作品自动从 Bangumi API 获取详情并添加到本地
  - 同步更新状态（想看/在看/看过）和评分
- **B站 同步**：已移除（B站 API 封锁第三方请求，无法突破）
- **补封面 + BangumiId**：核查→一键补全缺失封面，同时自动存储 BangumiId

## 评分系统 🔄

- **五星制 → 1-10 数字制**，支持一位小数（如 7.8），直接输入数字
- **评分筛选四档**：一般(1-4) / 还行(5-6) / 佳作(7-8) / 神作(9-10)
- 底层存储改为 ×10 整数（避免 SQLite INTEGER 截断）

## 作品管理

### 新增功能
- **外部链接**：有 BangumiId 的作品详情页显示 Bangumi/MAL/AniList 直达按钮
- **开始/完成日期**：添加/编辑作品时可选择起止日期
- **核查 6 字段报告**：封面/季度/简介/公司/作者/原作覆盖率，缺封面一键补全
- **自动标签**：AI 添加作品时，自动从 Bangumi 获取前 6 个热门标签
- **LightNovel 统计**：资料页新增轻小说计数，四类作品分别统计

### 优化
- **WorkService 拆分**：2816 行 → 3 个 partial class（主文件 1704 行 + Cover 330 行 + ImportExport 814 行）
- **封面下载缓存**：已存在本地的不重复下载，优先复用已存储的 BangumiId
- **所有 LLM 调用加 `max_tokens`**：限制回复长度加快响应
- **提示词缩减 40%**：减少输入 tokens

## Bug 修复

- **联网搜索序列化**：`Dictionary<string,object>`→分支匿名类型，修复 `enable_search` 未发出的 bug
- **SmartChat 上下文串扰**：`_contextHistory` 从 static 改为实例变量
- **主题切换文字褪色**：聊天气泡用 `TextElement.Foreground` DynamicResource 自动跟随主题
- **LLM 超时优化**：基础 60s，联网搜索 120s，重试机制（指数退避 1s→2s）
- **MAL/AniList 空值解析**：`SafeGetInt`/`SafeGetDouble` 检查 `JsonValueKind.Null`
- **JsonException 防御**：API 返回 HTML 错误页时优雅降级
- **JSON 解析失败兜底**：先尝试正则提取 `answer` 字段，再降级显示原文
- **GetAllNotes N+1 查询**：一次 SQL 批量获取全部笔记的关联作品
- **Bangumi 评分 ×2 越界**：Bangumi 10 分制不再错误映射
- **旧评分值迁移**：1-10 旧值自动 ×10 适配新评分系统
- **数据库迁移版本号**：`PRAGMA user_version` 快速通道，跳过已完成的迁移

## 工程改进

- **测试 17→59**：新增 `AIServiceTests`、`SearchProviderTests`，扩充 `WorkDataRulesTests`
- **RetryHelper**：带指数退避的 HTTP 重试，覆盖 Bangumi/MAL/AniList/AI Chat
- **Inno Setup 安装器脚本**：`Installer/AniTechou.iss` 已存在
- **Self-contained 发布**：用户无需安装 .NET 运行时

---

## 从源码构建

```powershell
dotnet build -c Release
dotnet test Tests\AniTechou.Tests.csproj -c Release
```

**构建**: 0 errors, 0 warnings | **测试**: 59 passed

## 下载

前往 [GitHub Releases](https://github.com/lunatic1157/AniTechou/releases) 下载 `AniTechou-Setup-v0.9.3.exe`
