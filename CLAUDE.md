# AniTechou — Claude 项目上下文

## 待修复问题 (P0)

### Bug 1: Markdown 预览不渲染
切换 Markdown 模式后，上半是编辑框、下半是 `FlowDocumentScrollViewer`，但预览区不显示渲染后的内容。

涉及文件:
- `Views/NoteEditor.xaml` — Grid.Row="3" 区域布局，`RichEditorPanel`、`MarkdownEditPanel`、`MarkdownPreviewPanel`、`MarkdownSplitter`
- `Views/NoteEditor.xaml.cs` — `MarkdownModeToggle_Click`(L623)、`RefreshMarkdownPreview`(L656)、`LoadData`(L156)
- `Utilities/MarkdownConverter.cs` — `MarkdownToFlowDocument(string)` 方法

当前状态:
- 工具栏 `MD` 按钮存在且可点击
- 点击后 RichEditorPanel 隐藏，MarkdownEditPanel + MarkdownPreviewPanel 显示
- `RefreshMarkdownPreview()` 调用 `MarkdownToFlowDocument()` → 赋值给 `MarkdownPreviewViewer.Document`
- 但界面不显示渲染效果

排查方向:
- `MarkdownToFlowDocument` 返回的 FlowDocument 是否正确（在方法末尾加 `Debug.WriteLine` 打印 Block 数量）
- `FlowDocumentScrollViewer.Document` 赋值后是否触发渲染
- Grid RowDefinition `* / Auto / *` 分配是否导致某面板高度为 0

### Bug 2: 自动添加的作品信息不全（制作公司、作者、原作）
通过 Bangumi 同步或 AI 搜索添加作品后，详情页的 Company/Author/OriginalWork 字段经常为空。

涉及文件:
- `Services/SearchProviders/BangumiSearchProvider.cs` — `ParseBangumiV0Subject`、`ExtractInfoboxValue`、`ExtractInfoboxValues`
- `Services/SyncService.cs` — `ApplyBangumiResultsAsync`(L146) 调用 `GetByIdAsync` 获取详情再 AddWork
- `Services/WorkService.cs` — `AddWork`(L1030) 的 fire-and-forget 补全逻辑(L1105-1138)
- `MainWindow.xaml.cs` — `AddWorkFromSearch`(L1366) AI 搜索添加流程

已做但未根治:
- infobox key 匹配已扩展: 出版社/レーベル/文库→Company, 连载杂志→Tag, 作者/插图分离
- `ExtractInfoboxValue/Values` 已支持 value 为直接字符串(非数组)的情况
- `AddWorkFromSearch` 现已传入 `bangumiId`
- `AddWork` fire-and-forget 会在有 Bangumi ID 时补全 7 个空字段
- **已加诊断日志**: VS 输出窗口搜索 `[BangumiSearch] infobox keys for` 和 `Company=`

排查方向:
- 在 VS 输出窗口看 infobox keys 日志，确认具体作品的 key 名称
- Bangumi API 对冷门作品可能本身就没有这些数据
- fire-and-forget 时序问题 — 补充细节可能晚于用户打开详情页

## 项目定位
Windows WPF 桌面端 ACGN 作品管理工具。核心差异：AI 辅助 + 多源同步 + 富文本笔记。

## 技术栈
- .NET 8 + WPF + SQLite (System.Data.SQLite)
- Markdig (Markdown 解析)
- 测试：xUnit (74 tests, `Tests/AniTechou.Tests.csproj`)
- 当前版本：v0.9.4

## 目录结构
```
Views/         WPF 页面 (WorksView, WorkDetailView, AddWorkForm, NoteEditor, SettingsView, ProfileView, AIBatchAddView, NotesView)
Windows/       弹窗 (EditWorkInfoWindow, LoginWindow, RegisterWindow, AppMessageDialog)
Services/      核心逻辑
  AIService.cs          AI 对话/搜索/补全 (GetDefaultSystemPrompt, SmartChat, BatchSearchWorks, BuildCollectionContext)
  WorkService.cs        作品 CRUD/标签/笔记/统计 (~1900 行)
  WorkService.Cover.cs  封面下载与缓存
  WorkService.ImportExport.cs  数据导入导出
  SyncService.cs        Bangumi 追番同步
  ConfigManager.cs      配置读写 (config.json)
  DatabaseHelper.cs     SQLite 建库/迁移/连接 (当前 DB v5)
  ThemeManager.cs       主题切换 (DynamicResource)
  RetryHelper.cs        HTTP 重试
  SearchProviders/      BangumiSearchProvider, MALSearchProvider, AniListSearchProvider, CompositeSearchProvider
Models/         WorkInfo, UserWorkInfo
Utilities/      WorkDataRules, MarkdownConverter
Tests/          74 测试
Installer/      Inno Setup 安装器脚本
```

## 构建与测试
```powershell
dotnet build -c Release
dotnet test Tests/AniTechou.Tests.csproj -c Release
dotnet run -c Release                                    # 直接跑，不需 publish
dotnet publish AniTechou.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64-self-contained  # 打包安装包前才用
```

## v0.9.4 新特性
- **5 状态**: 想看/在看/看过/搁置/抛弃，侧边栏+筛选栏均支持
- **分页加载**: 每页 50 部，`GetWorksCountAsync` 显示总数
- **Markdown 笔记**: NoteEditor 双模式切换，`Markdig` 转换，DB ContentType 列
- **AI 本地管理**: `BuildCollectionContext` 汇总收藏数据，prompt 区分「推荐新作(在线搜索)」vs「本地管理(收藏数据)」
- **Bangumi 同步增强**: 5 状态映射、类型映射修复、用户标签导入、infobox 解析增强
- **手动添加自动补全**: AddWorkForm 有 Bangumi ID 输入框，保存后异步补全

## 已知设计决策
- Rating 列 INTEGER，×10 存储 (7.7→77)
- WorkService 用 partial class 拆成 3 文件
- DB 迁移: PRAGMA user_version=5 (状态扩展 + ContentType + WorkTags Source)
- B站 同步已放弃
- 联网搜索默认关闭
