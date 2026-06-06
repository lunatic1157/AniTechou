# Tag Cleanup Preview Review

Review date: 2026-06-03

## Scope

- Account reviewed: `testuser`
- Database: `%LocalAppData%\AniTechou\accounts\testuser.db`
- Review mode: read-only preview. No cleanup was applied.
- Total works in account: 490

## Preview Summary

- Works affected: 216
- Tags to remove: 299
- Tags to add or convert: 23

## Removal Breakdown

| Reason | Count | Notes |
| --- | ---: | --- |
| Type / status / season / source-type tags | 139 | Examples: `原创`, `漫改`, `轻小说`, `动画`, `游戏改` |
| Company / studio tags duplicated from work fields | 63 | Examples: `MAPPA`, `BONES`, `京都动画`, `PINE JAM` |
| Year or date tags duplicated from work fields | 80 | Examples: `2026`, `2026年4月`, `2025年10月`, `2024年7月` |
| People prefix normalization | 12 | Old prefixed tags converted to standard prefixes |
| Rating tags | 5 | Examples: `评分:8.5` style tags |

Top repeated removals:

| Tag | Count |
| --- | ---: |
| `原创` | 74 |
| `漫改` | 30 |
| `2026` | 12 |
| `轻小说` | 11 |
| `2026年4月` | 9 |
| `MAPPA` | 8 |
| `BONES` | 7 |
| `小说改` | 6 |
| `漫画` | 5 |
| `动画` | 5 |

## Conversion Examples

The preview will add standardized people / creative tags such as:

- `导演:姜广涛`
- `导演:宝木中阳`
- `音乐:SOIL & "PIMP" SESSIONS`
- `音乐:未知瑠`
- `插画:raemz`
- `插画:黒星紅白`
- `脚本:加藤還一`
- `角色设计:永作友克`

During this review, two policy fixes were made before accepting the preview:

- Date tags such as `2026年4月` and `2024-04` are now treated as redundant date tags.
- Personnel values are no longer split by `/` or `／`, avoiding incorrect conversions such as `音乐:U/S` becoming `音乐:U` and `音乐:S`.

## Sample Affected Works

| Work | Remove | Add / Convert | Kept examples |
| --- | --- | --- | --- |
| `BanG Dream! Ave Mujica` | `三次元`, `原创` |  | `乐队`, `少女乐队`, `戏剧` |
| `CLANNAD` | `京都动画` |  | `Key`, `校园`, `治愈`, `石原立也` |
| `Girls Band Cry` | `2026`, `东映动画`, `原创`, `漫画` |  | `3D`, `乐队`, `百合`, `轻百合` |
| `Happy Sugar Life` | `2018年7月` |  | `心理恐怖`, `病娇`, `百合` |
| `NOMAD MEGALO BOX 2` | `2021年4月`, `原创` |  | `拳击`, `热血`, `科幻` |
| `Re：从零开始的异世界生活 第四季 丧失篇` | `2026年4月`, `小说改` |  | `冒险`, `奇幻`, `异世界`, `轮回` |
| `Urara迷路帖` | `芳文社` |  | `日常`, `治愈`, `百合`, `轻百合` |
| `【我推的孩子】第二季` | `动画工房` |  | `偶像`, `复仇`, `悬疑`, `演艺圈` |

## Risk Review

Low-risk removals:

- `原创`, `漫改`, `小说改`, `游戏改` are already represented by `SourceType`.
- `2026`, `2026年4月`, and similar date tags are already represented by year / season fields or are redundant for filtering.
- Studio/company tags that exactly match the work's company field are redundant because company filtering already exists.

Potential watch points:

- Some kept tags still look like structured metadata but are not removed by the current conservative policy, such as `TV`, `漫画改`, or `WHITEFOX` when the company field differs from the tag spelling.
- Some creative tags may be technically organizations rather than people, such as `音乐:ポニーキャニオン`. This is acceptable for the first cleanup because these are standardized and searchable, but a future `People` / `Staff` table would handle them better.
- Works with empty titles appeared in the data. Cleanup can still operate by work ID, but these records should be reviewed separately later.

## Recommendation

The preview was reasonable to apply after exporting a backup. The app requires a portable backup before applying cleanup.

Recommended execution path:

1. Open Settings.
2. Click `标签清理预览`.
3. Confirm the summary matches this review or is close after later edits.
4. Click `备份并应用清理` only after user confirmation.
5. Reopen Works and verify the tag filter area no longer shows most year/date/source/studio tags.

Do not run a silent full-library cleanup outside this preview window.

## Execution Result

Execution time: 2026-06-03 15:57 Asia/Shanghai

- Cleanup was applied through the app UI using `备份并应用清理`.
- Backup created: `%LocalAppData%\AniTechou\backups\tag_cleanup_testuser_20260603_155726.zip`
- Backup size: 136,708,607 bytes
- Post-cleanup read-only recalculation:
  - Works affected: 0
  - Tags to remove: 0
  - Tags to add or convert: 0

During execution, cleanup and backup completed successfully, but the completion message attempted to use an already closed preview window as its owner. This caused a fatal UI dialog after the data operation had finished. The code was fixed so the completion message is shown before closing the preview window.
