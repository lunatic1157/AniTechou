# AniTechou 开发说明

## 1. 文档定位
这份文档面向开发、课程汇报与项目复盘，不面向普通终端用户。
重点说明 AniTechou 的技术结构、关键设计、构建测试方式与发布流程。

## 2. 项目定位
AniTechou 是一个 Windows 桌面端 ACGN 作品记录工具，但项目的核心差异点不只是“记录作品”，而是：
- 用 AI 帮助搜索、推荐、补全和整理作品信息
- 用富文本笔记系统承载个人想法、截图、链接、标签与作品关联

因此，项目整体可以理解为：
- 作品管理层
- 笔记表达层
- AI 辅助层
- 本地持久化层

## 3. 技术栈
- 平台：.NET 8
- UI：WPF
- 数据库：SQLite
- 测试：xUnit
- 安装器：Inno Setup
- 发布形态：Windows x64 self-contained

相关工程：
- 主项目：[AniTechou.csproj](file:///f:/AniTechou/AniTechou/AniTechou.csproj)
- 测试项目：[AniTechou.Tests.csproj](file:///f:/AniTechou/AniTechou/Tests/AniTechou.Tests.csproj)
- 安装器脚本：[AniTechou.iss](file:///f:/AniTechou/AniTechou/Installer/AniTechou.iss)

## 4. 目录结构
- `Views/`：主界面视图与页面逻辑
- `Windows/`：弹窗、编辑窗口、对话框
- `Services/`：配置、主题、账号、数据库、AI、作品服务等核心业务
- `Models/`：作品与用户作品模型
- `Utilities/`：规则与通用逻辑
- `Resources/`：主题资源、样式资源
- `Tests/`：导入与规则相关单元测试
- `Installer/`：安装器脚本与语言文件

## 5. 核心模块说明

### 5.1 配置与主题
- 配置通过 [ConfigManager.cs](file:///f:/AniTechou/AniTechou/Services/ConfigManager.cs) 统一读写
- 主题通过 [ThemeManager.cs](file:///f:/AniTechou/AniTechou/Services/ThemeManager.cs) 管理
- 启动时在 [App.xaml.cs:L20-L24](file:///f:/AniTechou/AniTechou/App.xaml.cs#L20-L24) 初始化主题
- 主题配置会持久化保存，主界面切换后即时生效

### 5.2 账号与数据库
- 账号信息由 [AccountManager.cs](file:///f:/AniTechou/AniTechou/Services/AccountManager.cs) 管理
- 每个账号有独立数据库，由 [DatabaseHelper.cs](file:///f:/AniTechou/AniTechou/Services/DatabaseHelper.cs) 创建与连接
- 当前实现是“按账号分库”的本地单机结构，适合个人使用场景

### 5.3 作品服务
- 作品查询、筛选、详情、导入导出、封面处理等核心逻辑集中在 [WorkService.cs](file:///f:/AniTechou/AniTechou/Services/WorkService.cs)
- 导入时会结合 [WorkDataRules.cs](file:///f:/AniTechou/AniTechou/Utilities/WorkDataRules.cs) 做作品识别与去重
- 导入导出入口在 [SettingsView.xaml.cs:L201-L252](file:///f:/AniTechou/AniTechou/Views/SettingsView.xaml.cs#L201-L252)

### 5.4 笔记系统
- 笔记编辑器核心位于：
  - [NoteEditor.xaml](file:///f:/AniTechou/AniTechou/Views/NoteEditor.xaml)
  - [NoteEditor.xaml.cs](file:///f:/AniTechou/AniTechou/Views/NoteEditor.xaml.cs)
- 当前笔记系统支持：
  - 富文本编辑
  - 自动保存
  - 撤销/重做
  - 插图与链接
  - 标签建议与去重
  - 关联作品
- 这是项目最重要的差异化功能之一

### 5.5 AI 能力
- AI 能力入口在 [AIService.cs](file:///f:/AniTechou/AniTechou/Services/AIService.cs)
- 当前支持：
  - 测试连接
  - 作品搜索/推荐
  - 批量作品搜索
  - 作品信息补全
  - 对话式意图识别
- AI 系统提示词强调结构化 JSON 返回，以便程序直接解析并驱动作品与笔记操作

## 6. 本地数据与资源落盘
- 配置文件：`%LocalAppData%\AniTechou\config.json`
- 账号与数据库：`%LocalAppData%\AniTechou\accounts`
- 封面：`%LocalAppData%\AniTechou\covers`
- 头像：`%LocalAppData%\AniTechou\avatars`
- 笔记插图：`%LocalAppData%\AniTechou\Images\Notes`

设计目的：
- 让程序目录保持纯安装文件
- 重新安装不丢失个人数据
- 便于发布 zip 版与安装版共用同一套数据策略

## 7. 构建与测试

### 7.1 构建
```powershell
dotnet build -c Release
```

### 7.2 测试
```powershell
dotnet test Tests\AniTechou.Tests.csproj -c Release
```

### 7.3 测试范围
当前测试重点覆盖：
- 作品导入逻辑
- 作品识别与归一化规则

对应文件：
- [WorkServiceImportTests.cs](file:///f:/AniTechou/AniTechou/Tests/WorkServiceImportTests.cs)
- [WorkDataRulesTests.cs](file:///f:/AniTechou/AniTechou/Tests/WorkDataRulesTests.cs)

## 8. 发布与安装

### 8.1 发布产物
当前使用 self-contained 发布：
```powershell
dotnet publish AniTechou.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64-self-contained
```

### 8.2 安装器
- 安装器工具：Inno Setup
- 脚本位置：[AniTechou.iss](file:///f:/AniTechou/AniTechou/Installer/AniTechou.iss)
- 当前配置：
  - 安装目录为当前用户 `LocalAppData\Programs\AniTechou`
  - 支持开始菜单快捷方式
  - 支持可选桌面快捷方式
  - 安装器界面为简体中文
  - 使用自包含发布结果，因此用户无需预装 .NET 运行时

### 8.3 应用图标
- 应用图标文件位于 [AniTechou.ico](file:///f:/AniTechou/AniTechou/Assets/Icon/AniTechou.ico)
- csproj 通过 `ApplicationIcon` 注入 exe 图标
- 安装器通过 `SetupIconFile` 设置安装包图标

## 9. 当前适合课程/汇报强调的技术点
- WPF 本地桌面应用完整闭环
- AI 驱动的结构化数据输入与操作分发
- SQLite 本地持久化与按账号分库
- 富文本笔记系统与作品关系建模
- 主题系统与资源字典组织
- 单元测试覆盖核心导入规则
- 安装器打包、图标接入与发布流程

## 10. 后续可扩展方向
- 自动更新
- 更完整的 AI 指令编排与错误恢复
- 图片与封面路径的跨机器迁移优化
- 更系统的测试覆盖
- CI 自动化发布
