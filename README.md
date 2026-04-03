# AniTechou

AniTechou 是一款「AI + 笔记驱动」的 ACGN 作品管理工具。

它不只是记录“我看过什么”，更强调：
- 用 AI 搜索、推荐、补全和整理作品信息
- 用笔记记录当下的想法、截图、链接和标签

## 项目定位
- 面向个人使用的 Windows 桌面端作品记录软件
- 核心差异点不是单纯的追番清单，而是「AI 辅助 + 富文本笔记」
- 适合记录动画、漫画、轻小说、游戏等 ACGN 内容

## 核心亮点

### AI
- 自然语言搜索与推荐作品
- AI 批量搜索与导入作品信息
- AI 完善作品简介、季度、制作公司/作者、封面等信息
- 支持多平台配置：DeepSeek、智谱AI、阿里云百炼、OpenAI

### 笔记
- 富文本笔记编辑
- 自动保存、撤销/重做
- 插图、链接、标签、关联作品
- 标签建议与去重

### 作品管理
- 添加、编辑、详情查看
- 状态、年份、季度、原作、标签等筛选
- 数据导入与导出
- 主题切换与界面风格统一

## 功能概览
- 作品管理：添加/编辑、进度与状态、封面与详情、筛选与搜索
- AI 辅助：作品搜索/推荐、批量补全与导入、作品信息结构化补齐
- 笔记系统：富文本编辑、插图/链接/标签、作品关联、自动保存
- 数据能力：本地 SQLite 落盘、导入/导出（便于备份迁移）

## 界面预览

### 主界面
<img width="2076" height="1181" alt="image" src="https://github.com/user-attachments/assets/ac17b3e8-1c8d-4926-be54-d26a31880976" />


### 笔记编辑器与AI功能
<img width="2559" height="1599" alt="image" src="https://github.com/user-attachments/assets/047500bc-21d1-4722-b277-346951c8a97e" />



### 设置与 AI 配置
<img width="1545" height="1473" alt="image" src="https://github.com/user-attachments/assets/989186a6-0b2f-4342-965d-0738e95f6927" />

## 下载与安装
- 当前提供 Windows x64 安装版
- 前往 GitHub Releases 下载：<https://github.com/lunatic1157/AniTechou/releases>
- 安装包为 self-contained 版本，无需额外安装 .NET 运行时

## 快速开始
1. 安装并启动 AniTechou
2. 在设置页配置 AI 提供商与 Key（可选）
3. 添加作品或使用 AI 搜索/批量导入完善作品信息
4. 在作品详情或笔记页创建笔记，记录截图、链接与标签，并关联到作品

## 数据存储位置
- 软件数据默认保存在 `%LocalAppData%\AniTechou`
- 包含配置、账号信息、数据库、封面、头像与笔记插图

## 隐私与安全
- 项目不会把 API Key 写入仓库，Key 仅保存在本机数据目录中
- 安装/升级不会默认清空个人数据；如需“彻底重置”，删除 `%LocalAppData%\AniTechou` 即可

## 项目文档
- 开发说明：[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)
- AI 协作开发说明：[docs/AI_WORKFLOW.md](docs/AI_WORKFLOW.md)
- 发布说明： [docs/RELEASE_NOTES_v0.9.0.md](docs/RELEASE_NOTES_v0.9.0.md)、[docs/RELEASE_NOTES_v0.9.1.md](docs/RELEASE_NOTES_v0.9.1.md)

## 从源码运行
```powershell
dotnet build -c Release
dotnet test Tests\AniTechou.Tests.csproj -c Release
dotnet run --project AniTechou.csproj
```
