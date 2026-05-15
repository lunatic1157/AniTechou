# AniTechou v0.9.4

本次更新聚焦于「Bug修复 + 性能优化 + Markdown笔记 + AI本地化 + 完整Bangumi同步」。

## Bug 修复

- **Bangumi 同步状态全部变成「想看」**：修复 Bangumi API 返回整数状态码（1/2/3/4/5）被当作字符串匹配的bug，状态映射现已正确
- **非动画作品全部标注为「动画」**：修复 Bangumi 同步时漫画/轻小说/游戏全部被映射为 Anime 的问题，现在使用正确的类型映射
- **Bangumi 导入丢失标签**：修复同步时获取了标签详情但未保存到数据库的问题，现在自动保存 Bangumi 公共标签

## 新增功能

### 完整状态同步
- 新增「搁置」和「抛弃」两种状态，与 Bangumi 的 5 种收藏状态完全对应
- 筛选栏、作品详情、添加作品页面均已支持 5 状态

### 自动标签
- 添加作品时若提供了 Bangumi ID，自动从 Bangumi 获取并保存标签（声优、导演、制作人员等）
- Bangumi 同步时自动保存作品的公共标签和用户个人标签
- WorkTags 表 Source 字段扩展支持 "Bangumi" 和 "AI导入"

### 跨类型 Bangumi 同步
- 同步时不再将所有作品标记为动画，漫画、轻小说、游戏各自保持正确类型

### Markdown 笔记
- 笔记编辑器采用 **MD-native 单一模式**，底层存储统一为 Markdown 纯文本
- 编辑区 = 上半源码编辑 + 下半 FlowDocumentScrollViewer 实时预览
- 工具栏按钮直接插入/包裹 Markdown 语法（B→`**`、I→`*`、U→`<u>`、H1→`# ` 等）
- Markdig 0.40 AST Walker 解析引擎：完整支持 Heading、CodeBlock、List、Table、Quote、Emphasis、Link、Image
- 预览支持 `<u>` 下划线、`<span style="color/bgcolor">` 文字颜色/高亮
- `![](path)` 本地图片直接嵌入 FlowDocument 显示
- `[text](url)` 蓝色下划线，点击浏览器打开
- 旧 XAML 笔记首次打开时自动迁移为 Markdown

### AI 本地化增强
- AI 现在始终了解你的完整收藏列表
- 可以回答「我有哪些京阿尼的作品」「我有几部动画」「推荐几部我没看过的」等本地化问题
- 基于收藏作品的口味进行个性化推荐

## 性能优化

- **分页加载**：作品列表改为分页加载（每页 50 部），大幅减少首次渲染卡顿
- **异步封面**：封面图片改为异步解码，不阻塞 UI 线程
- **封面尺寸限制**：列表封面缩放到 280px 宽度，减少内存占用
- 新增「加载更多」按钮，按需加载后续作品

## 数据库迁移

- 数据库版本升级至 v5
- UserList.Status CHECK 约束扩展为 5 种状态
- Notes 表新增 ContentType 列
- WorkTags.Source CHECK 约束扩展

## 工程改进

- 新增 `SyncServiceTests`：测试 Bangumi 类型映射
- 新增 `MarkdownConverterTests`：测试 XAML↔Markdown 转换
- 扩展 `AIServiceTests`：测试收藏上下文构建和本地化指令
- 新增 `Utilities/MarkdownConverter`：XAML↔Markdown 双向转换工具
- 新增 NuGet 依赖：Markdig 0.40.0

---

## 从源码构建

```powershell
dotnet build -c Release
dotnet test Tests\AniTechou.Tests.csproj -c Release
```

## 下载

前往 [GitHub Releases](https://github.com/lunatic1157/AniTechou/releases) 下载 `AniTechou-Setup-v0.9.4.exe`
