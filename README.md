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

## 数据存储位置
- 软件数据默认保存在 `%LocalAppData%\AniTechou`
- 包含配置、账号信息、数据库、封面、头像与笔记插图

## 项目文档
- 开发说明：[docs/DEVELOPMENT.md](file:///f:/AniTechou/AniTechou/docs/DEVELOPMENT.md)
- AI 协作开发说明：[docs/AI_WORKFLOW.md](file:///f:/AniTechou/AniTechou/docs/AI_WORKFLOW.md)
- 首次发布说明：[docs/RELEASE_NOTES_v0.9.0.md](file:///f:/AniTechou/AniTechou/docs/RELEASE_NOTES_v0.9.0.md)

## 从源码运行
```powershell
dotnet build -c Release
dotnet test Tests\AniTechou.Tests.csproj -c Release
dotnet run
```
