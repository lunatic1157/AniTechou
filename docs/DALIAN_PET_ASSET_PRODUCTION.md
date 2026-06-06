# 丹特丽安桌宠形象制作交接文档

本文档用于单独推进 AniTechou 的丹特丽安桌宠形象。v0.9.5 已完成桌宠功能链路，但当前 WPF 矢量形象只是占位验证稿，不能代表最终视觉质量。下一轮目标是制作可替换的 PNG 资源包，并尽量不改动 `DesktopPetService` 的业务逻辑。

## 当前代码位置

- 桌宠服务：`Services/DesktopPetService.cs`
- 当前窗口与占位图：`Windows/DesktopPetWindow.xaml`、`Windows/DesktopPetWindow.xaml.cs`
- 资源清单：`Assets/Pets/Dalian/pet.json`
- 人设约束：`docs/DALIAN_PERSONA_GUIDE.md`
- 本次实现说明：`docs/V0.9.5_NOTES_AND_DALIAN_PET.md`

## 形象目标

- 角色必须能一眼看出是丹特丽安 / Dalian / Dantalian，而不是普通黑衣 Q 版少女。
- 保留核心识别点：黑衣少女、哥特感、深色长发、白色发饰或头饰、书、锁、古典书架气质。
- 表情应高傲、冷淡、挑剔，但不是网络表情包式嘲讽。
- 尺寸要适合桌宠：小尺寸仍能辨认轮廓，透明背景，边缘干净。
- 动作要短小安静，避免抢占主应用注意力。

## 建议资源规格

优先方案：PNG spritesheet。

- 路径：`Assets/Pets/Dalian/`
- 单帧建议尺寸：`256x256`，透明背景。
- 运行时显示尺寸：约 `160x170` 到 `190x200`，由窗口缩放控制。
- 命名建议：
  - `idle.png`
  - `thinking.png`
  - `pleased.png`
  - `annoyed.png`
  - `moving.png`
  - `sleeping.png`
- 如果每个状态有多帧动画，可使用：
  - `idle_256x256_8f.png`
  - `thinking_256x256_8f.png`
  - `pleased_256x256_6f.png`
  - `annoyed_256x256_6f.png`
  - `moving_256x256_4f.png`
  - `sleeping_256x256_8f.png`

资源清单建议扩展为：

```json
{
  "id": "dalian",
  "displayName": "丹特丽安",
  "source": "丹特丽安的书架",
  "implementation": "png spritesheet",
  "frameWidth": 256,
  "frameHeight": 256,
  "states": {
    "idle": { "file": "idle_256x256_8f.png", "frames": 8, "fps": 6 },
    "thinking": { "file": "thinking_256x256_8f.png", "frames": 8, "fps": 8 },
    "pleased": { "file": "pleased_256x256_6f.png", "frames": 6, "fps": 6 },
    "annoyed": { "file": "annoyed_256x256_6f.png", "frames": 6, "fps": 6 },
    "moving": { "file": "moving_256x256_4f.png", "frames": 4, "fps": 8 },
    "sleeping": { "file": "sleeping_256x256_8f.png", "frames": 8, "fps": 5 }
  }
}
```

## 可借鉴 Petdex/Codex 宠物方式

可借鉴的是资源组织方式，不是直接迁移运行时：

- 用资源包描述角色、状态、帧尺寸、帧数和帧率。
- 应用事件映射到宠物状态，例如 idle、thinking、pleased、annoyed、moving、sleeping。
- 渲染层只负责播放当前状态动画，业务层继续由 `DesktopPetService` 分发事件。

AniTechou 是 WPF 桌面应用，建议仍然用内置透明窗口承载桌宠。下一轮实现可以把 `DesktopPetWindow.xaml` 中的矢量图替换为 `Image`，用 `CroppedBitmap` 或切帧逻辑播放 spritesheet。

## 新对话提示词

可以直接把下面这段发给新的 Codex 对话：

```text
我们在 F:\AniTechou\AniTechouRepo 做 AniTechou v0.9.5 的丹特丽安桌宠形象完善。当前桌宠功能链路已经实现：Services/DesktopPetService.cs、Windows/DesktopPetWindow.xaml、Windows/DesktopPetWindow.xaml.cs、Assets/Pets/Dalian/pet.json。现在的 WPF 矢量形象只是占位，用户认为完全看不出是丹特丽安，需要单独制作最终桌宠资源。

请先阅读：
- docs/DALIAN_PET_ASSET_PRODUCTION.md
- docs/DALIAN_PERSONA_GUIDE.md
- docs/V0.9.5_NOTES_AND_DALIAN_PET.md
- Windows/DesktopPetWindow.xaml
- Windows/DesktopPetWindow.xaml.cs
- Assets/Pets/Dalian/pet.json

目标：
1. 调研丹特丽安 / Dalian / Dantalian 的外观与人设，必要时联网查资料，用户不要求规避版权风险。
2. 如果我提供参考图，优先根据参考图生成桌宠资源；如果我没有提供图，请自行查找可靠参考并说明来源。
3. 可以调用 AI 生图、图像编辑、Canva、Figma、或其他可用工具探索形象，但最终要落到本地资源文件。
4. 参考 Petdex/Codex 宠物的资源包思路，制作透明背景 PNG spritesheet 或至少一组状态 PNG。
5. 优先输出 256x256 单帧或 spritesheet，状态包括 idle、thinking、pleased、annoyed、moving、sleeping。
6. 修改 DesktopPetWindow 的渲染层，让它优先加载 Assets/Pets/Dalian 下的新 PNG 资源；尽量不要改 DesktopPetService 的事件分发逻辑。
7. 更新 Assets/Pets/Dalian/pet.json，记录帧尺寸、帧数、fps、文件名。
8. 做实际视觉测试：启动 Release 版 AniTechou，打开设置启用桌宠，确认透明背景、缩放、默认位置、状态切换和窗口拖拽没有问题。必要时截图检查。

形象要求：
- 必须能一眼看出是丹特丽安，而不是普通黑衣 Q 版少女。
- 保留黑衣、哥特、深色长发、白色发饰或头饰、书、锁、古典书架气质。
- 表情高傲、冷淡、挑剔，但不要做成网络恶毒 AI。
- 小尺寸也要能辨认轮廓，动作安静，不能遮挡主应用关键操作。

完成后请给出：变更文件、资源规格、视觉测试结果、仍需人工挑选或替换的素材问题。
```

## AI 生图提示词草案

中文方向：

```text
丹特丽安的桌面宠物形象，Q版但不幼稚，黑衣哥特少女，深色长发，白色发饰，抱着古典书本，胸前或书上有金色锁与钥匙意象，表情高傲冷淡、聪明挑剔，古典图书馆气质，清晰轮廓，透明背景，适合桌面宠物小尺寸显示，256x256，像素干净，边缘清楚，不要复杂背景，不要现代网络萌系表情包风格
```

英文方向：

```text
chibi desktop pet character inspired by Dalian / Dantalian from Bibliotheca Mystica de Dantalian, gothic black dress, long dark hair, white hair accessory, holding an antique book, golden lock and key motif, haughty calm expression, elegant mysterious library guardian, clean silhouette, transparent background, readable at small size, 256x256, polished anime game sprite, no background, no meme expression
```

状态差分：

- `idle`：站立或坐在书旁，轻微呼吸，冷淡看向用户。
- `thinking`：翻书、书页微亮、锁或钥匙有轻微光效。
- `pleased`：略微得意，书合上或轻点书脊。
- `annoyed`：皱眉、抱书偏头、轻微不悦。
- `moving`：被拖拽时抱紧书，衣摆或发梢轻微偏移。
- `sleeping`：靠在书上小睡，仍保持哥特与书架意象。

## 验收标准

- `dotnet build -c Release` 通过。
- `dotnet test Tests\AniTechou.Tests.csproj -c Release` 通过。
- 启动 `bin\Release\net8.0-windows\AniTechou.exe` 后能在设置中启用桌宠。
- 桌宠透明背景正常，无白底、黑边或锯齿明显问题。
- 默认位置不遮挡 AI 输入框、发送按钮、笔记编辑区核心操作。
- 资源文件在 clean checkout 后可被复制到输出目录或通过 WPF Resource/Content 正确加载。
- `pet.json` 是合法 UTF-8 JSON。
