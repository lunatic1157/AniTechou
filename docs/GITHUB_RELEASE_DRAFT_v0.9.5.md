# AniTechou v0.9.5

本次更新聚焦于「资料整理 + 网络代理 + AI 推荐质量 + Markdown 笔记体验 + 夜子桌宠 MVP」。

v0.9.5 的目标不是单点堆功能，而是把日常使用里最容易打断节奏的地方收顺：标签更干净、网络访问更可控、推荐理由更具体、笔记编辑更像一个长期可用的工作区，同时补上一个可以陪在桌面上的轻量桌宠入口。

## 面向用户的变化

### 笔记编辑体验

- Markdown 笔记编辑器改为左右分栏：左侧编辑，右侧实时预览。
- 分栏宽度可以拖拽调整，并会记住上次比例。
- 长笔记输入时预览刷新更稳，不再每敲一个字符都立刻完整重绘。
- 继续保留 Markdown 存储、自动保存、图片/链接插入、标签和作品关联逻辑。

### 标签整理

- 新增全库标签清理预览窗口，可以先看清楚将删除或整理哪些标签，再决定是否应用。
- 自动标签会过滤年份、日期、评分、状态等冗余内容，减少标签列表被“评分:8.5”“2024”“已完成”这类信息淹没。
- 声优、导演、脚本、音乐、角色设计、原作、插画等人员标签格式更统一。

### 网络与代理

- 设置页新增网络与代理区域。
- 支持使用系统代理、自定义代理或不使用代理。
- 新增网络诊断入口，用于检查 Bangumi API、Bangumi 图片、Jikan、AniList 和 AI API 的连通情况。

### AI 推荐质量

- AI 推荐现在会标注推荐类型，例如相似推荐、口味推荐、拓展推荐、补课推荐。
- 推荐卡片会显示推荐理由，并尽量说明它参考了你的本地收藏画像。
- 推荐前会过滤本地已有作品，减少重复推荐。

### 夜子桌宠 MVP

- 设置页新增“启用夜子桌宠”开关，默认关闭。
- 启用后会显示一个透明、置顶、可拖拽的夜子桌宠小窗，位置会自动保存。
- 桌宠会响应应用事件：
  - AI 思考时进入 `thinking` 状态。
  - 笔记保存成功时进入 `pleased` 状态。
  - AI 或保存失败时进入 `annoyed` 状态。
  - 拖拽时进入 `moving` 状态。
- 桌宠使用 6 个透明 PNG 状态资源，不再使用 WPF 矢量占位图。
- 为避免静态角色图显得奇怪，本版取消了倾斜和上下晃动动画。
- 双击桌宠可打开轻量对话框，支持本地短回复、状态切换、隐藏和关闭桌宠。
- 右键菜单支持打招呼、打开对话框、暂时隐藏和关闭桌宠。

## 技术与工程变化

- `TagPolicy` 统一承载自动标签清理规则，并增加对应测试。
- 搜索提供者不再输出评分类标签，减少脏标签来源。
- 外部 HTTP 访问统一走 `NetworkClientFactory`，便于代理模式和网络诊断复用。
- AI 推荐展示逻辑拆到 `AIRecommendationDisplayHelper`，推荐分类/理由缺失时有兜底显示。
- `RecommendationProfile` 会提取高评分作品、已看/在看作品、常见标签、人员标签和类型偏好，用于推荐 prompt。
- Markdown 预览刷新增加防抖，Markdig 管线复用，减少编辑时的重复开销。
- 桌宠渲染层优先从 `Assets/Pets/Dalian` 加载角色资源包；当前目录名为兼容旧链路保留，manifest 已切换为 `yoruko`。
- 版本号已更新为 `0.9.5`，安装器脚本输出 `AniTechou-Setup-v0.9.5.exe`。

## 验证

本地发布前验证已完成：

```powershell
dotnet build -c Release
dotnet test Tests\AniTechou.Tests.csproj -c Release
```

当前结果：

- 构建：0 warnings, 0 errors
- 测试：104 passed
- 已启动 Release 版做桌宠视觉检查：透明 PNG 显示、双击对话框、本地短回复、状态按钮、隐藏/关闭按钮、拖拽移动和位置保存均已验证。
- 已生成 Windows x64 安装包：
  - `Installer/output/AniTechou-Setup-v0.9.5.exe`
  - SHA256: `3434784FEA24B8CA2F601E335D69CA305024862EF43D427F28A768AFB19CF8EB`

## 后续优化方向

- 把桌宠对话框接入真实 AI 对话，让夜子能在桌面上进行短句互动。
- 将角色人设 prompt 与原 AI 助手能力 prompt 分层，支持夜子、丹特丽安或其他角色包切换。
- 增加角色包 manifest 校验、资源预览和状态触发测试工具。
- 继续拆分 `MainWindow.xaml.cs` 中的 AI 意图分发，让 AI 命令、推荐、补全和桌宠对话各自有更清晰的控制器。
- 增加发布自动化：构建、测试、publish、安装器打包、校验和 Release 草稿生成可以合成一条脚本。

---

## 从源码构建

```powershell
dotnet build -c Release
dotnet test Tests\AniTechou.Tests.csproj -c Release
```

## 下载

前往 [GitHub Releases](https://github.com/lunatic1157/AniTechou/releases) 下载 `AniTechou-Setup-v0.9.5.exe`。
