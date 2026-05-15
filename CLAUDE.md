# AniTechou — Claude 项目上下文

## 待修复问题 (P0)

### Bug 1: Markdown 模式不稳定（内容消失 + 剪贴板崩溃）✅ 已修复

**修复历程**:
- 预览区不渲染 → 已修复（`FlowDocumentScrollViewer` 添加 Foreground/Background）
- 暗色主题文字不可见 → 已修复（`RefreshMarkdownPreview` 显式设置 Foreground）
- 切换 MD 按钮时的自动保存竞态 → 已修复（`MarkdownModeToggle_Click` 包裹 suppressTextEvents）
- XamlToMarkdown 图片丢失 → 已修复（`FindImageInGrid` 遍历 Children 替代 FindName）
- 剪贴板方案崩溃 → 重写为 **Markdig AST walker**（`Markdown.Parse()` → 递归遍历 → 构建 WPF FlowDocument 元素）
- 编译错误 → `Table`/`TableRow`/`TableCell` 命名空间修正：`Markdig.Syntax` → `Markdig.Extensions.Tables`（commit `193534f`）
- 往返转换标记叠加 → `ConvertInlinesToMarkdown` 新增 `inBold/inItalic` 上下文参数，防止 Run 继承 Bold 的 FontWeight 后重复追加 `**`（commit `bc18363`）
- `ConvertEmphasis` 复用 `innerSpan` + `.ToList()` 快照，修复集合修改异常（commit `101b03e`）
- **架构重构为 MD-native**（commit `33d2be5`）：删除 XAML↔MD 双模式切换，底层统一存储 Markdown，编辑=上半源码+下半实时预览，工具栏=插入 MD 语法，旧 XAML 笔记首次打开时自动迁移

**AST Walker 能力**:
- Block: Heading(1-6级), Paragraph, FencedCodeBlock, CodeBlock, List(有序/无序), ThematicBreak, Quote(左边框), Table
- Inline: LiteralInline, EmphasisInline(`*`/`**`/`***`), LinkInline(链接+图片), CodeInline, LineBreakInline, HtmlInline, HtmlEntityInline

涉及文件:
- `Utilities/MarkdownConverter.cs` — `MarkdownToFlowDocument`（AST walker）、`ConvertBlock`、`ConvertInline`、`XamlToMarkdown`

### Bug 2: 自动添加的作品信息不全 ✅ 已基本解决

**已修复**:
- `BangumiSearchProvider.ParseBangumiV0Subject`: infobox key 匹配顺序修复
  - "原作者" 优先匹配 Author（原来被 "原作" 抢先命中，错误写入 OriginalWork）
  - 新增 key 匹配: 制作/作画/开发/開発/品牌
- `SyncService.ApplyBangumiResultsAsync`: **已有作品元数据补全**
  - 匹配到已有作品时，检查 Company/Author/OriginalWork/Season/SourceType/Episodes/Synopsis/Cover 空字段
  - 有 BangumiId 则从 Bangumi 拉取补全
  - BangumiId 保存移出 `if(changed)` 判断，始终关联
- 诊断日志: 输出窗口搜索 `[SyncService] 元数据补全`

**剩余问题**:
- 冷门作品 Bangumi infobox 本身无数据 → 非代码问题
- 部分 infobox key 名称与预期不同 → 查看输出窗口日志逐个补充
- fire-and-forget 时序（补充晚于用户打开详情页）→ 下次打开详情页即显示

## 会话改动总结

### v0.9.4 开发 (2026-05-15)

- **Markdig AST Walker**: Markdown→FlowDocument 预览引擎，Markdig 0.40 直接解析构建 WPF FlowDocument
- **MD-native 编辑器重构**: 删除 XAML↔MD 双模式切换，底层统一存储 Markdown，编辑区=源码+实时预览
- **工具栏 MD 语法插入**: B→`**`, I→`*`, U→`<u>`, H1-3→`#`, 列表→`-`/`1.`, 链接→`[text](url)`, 图片→`![](path)`
- **HTML 标签渲染**: `<u>`→下划线, `<span style="color/bgcolor">`→文字颜色/高亮
- **图片渲染**: `![](path)` 本地图片直接嵌入 FlowDocument
- **链接交互**: `[text](url)` 蓝色下划线，点击浏览器打开
- **旧笔记自动迁移**: 首次打开 XAML 笔记时调用 XamlToMarkdown 转换并持久化
- **ImportExport**: 图片路径正则同时匹配 `ani-image:` 和 `![](path)` 两种格式
- **Bug 修复**: Table 命名空间、往返转换标记叠加、集合修改异常、WrapSelection 越界

## 项目定位
Windows WPF 桌面端 ACGN 作品管理工具。核心差异：AI 辅助 + 多源同步 + Markdown 笔记。

## 技术栈
- .NET 8 + WPF + SQLite (System.Data.SQLite)
- Markdig 0.40 (Markdown 解析) — AST walker 方案，底层存储纯 Markdown 文本
- 测试：xUnit (74 tests, `Tests/AniTechou.Tests.csproj`)
- 当前版本：v0.9.4

## 目录结构
```
Views/         WPF 页面 (WorksView, WorkDetailView, AddWorkForm, NoteEditor, SettingsView, ProfileView, AIBatchAddView, NotesView)
Windows/       弹窗 (EditWorkInfoWindow, LoginWindow, RegisterWindow, AppMessageDialog)
Services/      核心逻辑
  AIService.cs          AI 对话/搜索/补全
  WorkService.cs        作品 CRUD/标签/笔记/统计 (~1900 行)
  WorkService.Cover.cs  封面下载与缓存
  WorkService.ImportExport.cs  数据导入导出
  SyncService.cs        Bangumi 追番同步 (含元数据补全)
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
dotnet run -c Release
dotnet publish AniTechou.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64-self-contained
```

## v0.9.4 新特性
- **5 状态**: 想看/在看/看过/搁置/抛弃
- **分页加载**: 每页 50 部
- **MD-native 编辑器**: 底层存储=Markdown，编辑=源码+实时预览，工具栏插入 MD 语法，Markdig AST walker 预览引擎
- **Markdown 预览增强**: HTML 标签渲染(`<u>`/`<span>`)、图片直接显示、链接交互
- **旧笔记自动迁移**: 首次打开 XAML 笔记自动转 Markdown
- **AI 本地管理**: BuildCollectionContext 汇总收藏数据
- **Bangumi 同步增强**: 5 状态映射、类型映射、用户标签导入、infobox 解析、已有作品元数据补全

## 已知设计决策
- Rating 列 INTEGER，×10 存储 (7.7→77)
- WorkService 用 partial class 拆成 3 文件
- DB 迁移: PRAGMA user_version=5
- B站 同步已放弃
- 联网搜索默认关闭
