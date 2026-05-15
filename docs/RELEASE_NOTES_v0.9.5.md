# AniTechou v0.9.5

## 笔记编辑器重构：MD-native 架构

### 核心变更

**从 XAML↔Markdown 双模式切换改为 MD-native 单一模式**，底层存储统一为 Markdown 纯文本。

- **编辑区**：上半 Markdown 源码编辑 + 下半实时预览（FlowDocumentScrollViewer），始终可见，无需切换
- **工具栏按钮**：B→`**`、I→`*`、U→`<u>`、H1→`# `、•→`- `、1.→`1. `、🔗→`[text](url)`、🖼→`![](path)` 等，直接插入/包裹 Markdown 语法
- **键盘快捷键**保持不变：Ctrl+B/I/U/1/2/3/K、Ctrl+Shift+L/O/I
- **自动迁移**：首次打开旧 XAML 格式笔记时，自动调用 XamlToMarkdown 转换为 Markdown 并持久化
- 删除约 860 行 RichTextBox/图片缩放拖拽代码

### Markdown 预览增强

- **HTML 标签渲染**：`<u>` → 下划线、`<span style="color:#XXX">` → 文字颜色、`<span style="background-color:#XXX">` → 高亮
- **图片渲染**：`![](path)` 本地图片直接加载显示（InlineUIContainer），最大 700×500，Uniform 缩放
- **链接交互**：`[text](url)` 蓝色下划线，点击调用默认浏览器打开
- **Markdig AST Walker**：Markdown→FlowDocument 完整支持 Heading / Paragraph / Code(Fenced)Block / List / ThematicBreak / Quote / Table / Emphasis(粗斜体) / Link / Image / HTML inline

### Bug 修复

- 修复 Markdig 0.40 Table/TableRow/TableCell 命名空间错误（`Markdig.Syntax` → `Markdig.Extensions.Tables`）
- 修复 XAML→Markdown 往返转换时粗体/斜体标记重复叠加（Run 在 Bold/Italic 内部继承 FontWeight 导致 `****text****`）
- 修复 ConvertEmphasis 集合修改异常（WPF Inline 单父级限制，`.ToList()` 快照枚举）
- 修复 WrapSelection 可能抛出 ArgumentOutOfRangeException（保存原始 SelectionStart 再计算）
- 修复图片导入导出的正则同时匹配新旧两种格式（`ani-image:` + `![](path)`）

### 技术细节

- 依赖：.NET 8、WPF、SQLite、Markdig 0.40
- 测试：74 个 xUnit 测试全部通过
- DB 版本：PRAGMA user_version=5
- ContentType 字段新增默认值 "Markdown"，旧 "Xaml" 笔记懒迁移
