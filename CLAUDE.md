# AniTechou — Claude 项目上下文

## 项目定位
Windows WPF 桌面端 ACGN 作品管理工具。核心差异：AI 辅助 + 多源同步 + 富文本笔记。

## 技术栈
- .NET 8 + WPF + SQLite (System.Data.SQLite)
- 测试：xUnit (59 tests, `Tests/AniTechou.Tests.csproj`)
- 当前版本：v0.9.3

## 目录结构
```
Views/         WPF 页面 (WorksView, WorkDetailView, AddWorkForm, NoteEditor, SettingsView, ProfileView, AIBatchAddView, NotesView)
Windows/       弹窗 (EditWorkInfoWindow, LoginWindow, RegisterWindow, AppMessageDialog)
Services/      核心逻辑
  AIService.cs          AI 对话/搜索/补全 (GetDefaultSystemPrompt, SmartChat, BatchSearchWorks)
  WorkService.cs        作品 CRUD/标签/笔记/统计 (~1700 行)
  WorkService.Cover.cs  封面下载与缓存
  WorkService.ImportExport.cs  数据导入导出
  SyncService.cs        Bangumi 追番同步
  ConfigManager.cs      配置读写 (config.json)
  DatabaseHelper.cs     SQLite 建库/迁移/连接
  ThemeManager.cs       主题切换 (DynamicResource)
  RetryHelper.cs        HTTP 重试 (指数退避 1s→2s, max 2 次)
  SearchProviders/      BangumiSearchProvider, MALSearchProvider, AniListSearchProvider, CompositeSearchProvider
Models/         WorkInfo, UserWorkInfo
Utilities/      WorkDataRules (去重/归一化)
Tests/          59 测试 (AIServiceTests, SearchProviderTests, WorkDataRulesTests, WorkServiceImportTests)
Installer/      Inno Setup 安装器脚本
```

## 关键文件路径
- 配置文件：`%LocalAppData%\AniTechou\config.json`
- 数据库：`%LocalAppData%\AniTechou\accounts\{username}.db`
- 封面缓存：`%LocalAppData%\AniTechou\covers\`
- 笔记图片：`%LocalAppData%\AniTechou\Images\Notes\`

## 构建与测试
```powershell
dotnet build -c Release
dotnet test Tests/AniTechou.Tests.csproj -c Release
dotnet publish AniTechou.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64-self-contained
```

## 当前特性
- AI 搜索/对话/批量补全 (支持 DeepSeek/Kimi/OpenAI/自定义平台)
- 多源搜索聚合 (Bangumi + MAL + AniList)
- Bangumi 追番同步 (自动导入未匹配作品)
- 评分 1-10 数字制 (×10 整数存储, 显示时 ÷10)
- 12 个 AI 快捷提问标签
- 核查报告 (封面/季度/简介/公司/作者/原作覆盖率)
- 外部链接按钮 (Bangumi/MAL/AniList)
- 主题切换 (DynamicResource)
- 开始/完成日期
- 富文本笔记
- 数据导入/导出 (JSON + ZIP 便携备份)
- DB 迁移版本号 (PRAGMA user_version)

## 已知设计决策
- Rating 列 INTEGER，×10 存储 (7.7→77)，避免 SQLite 截断
- WorkService 用 partial class 拆成 3 文件
- 搜索缓存 1min TTL
- B站 同步已放弃 (API 封锁)
- 联网搜索默认关闭 (延迟高)
