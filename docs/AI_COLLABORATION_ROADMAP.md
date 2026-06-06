# AniTechou AI Collaboration Roadmap

## Current Implementation

- Tag normalization is centralized in `TagPolicy`.
- AI-added, AI-updated, and Bangumi-completed tags now pass through the same cleanup rules.
- Redundant automatic tags are removed for the work currently touched by AI or Bangumi completion.
- Search providers no longer emit score tags such as `评分:8.5`, `MAL评分:8.1`, or `AniList评分:89%`.
- People and creative tags are standardized as:
  - `cv:声优名`
  - `导演:姓名`
  - `脚本:姓名`
  - `音乐:姓名`
  - `角色设计:姓名`
  - `原作:姓名`
  - `插画:姓名`
- Network access now uses a shared HTTP client factory with system proxy, custom proxy, and no-proxy modes.
- Settings includes network diagnostics for Bangumi API, Bangumi image host, Jikan, AniList, and AI API.

## Progress Status

Last updated: 2026-06-03

- Phase 1 tag normalization is implemented and covered by tests.
- Full-library tag cleanup preview was implemented and applied to the `testuser` account after creating a portable backup.
- Post-cleanup recalculation for `testuser` reports zero remaining cleanup actions.
- Phase 2 network/proxy foundation is implemented: shared HTTP client creation, proxy modes, and settings diagnostics.
- AI recommendation v1 is implemented: prompts request recommendation categories and reasons, local duplicate works are filtered out before rendering, and recommendation cards show the category/reason block.
- Manual UI check confirmed an AI recommendation card displaying `拓展推荐` with a specific recommendation reason.
- AI recommendation quality enhancement is implemented: `RecommendationProfile` extracts high-rated works, done/doing works, common regular tags, people tags, and type preferences for prompt context.
- Recommendation prompts now explicitly require `相似推荐` / `口味推荐` / `拓展推荐` / `补课推荐` and ask each reason to name the local profile signals used.
- Recommendation card display logic is split into `AIRecommendationDisplayHelper`, including a fallback block when the AI omits category or reason.
- Release UI check on `testuser` confirmed recommendation cards display category and reason blocks normally without clicking write actions.

## Collaboration Roles

### Codex

Codex owns implementation, tests, and integration.

- Keep tag policy behavior covered by unit tests before changing it.
- Keep external HTTP access behind `NetworkClientFactory`.
- Run `dotnet build -c Release` and `dotnet test Tests\AniTechou.Tests.csproj -c Release` before handing off.
- Use the small test account for write-heavy QA. Use the real account only for read-only recommendation and network diagnostics unless a backup and preview exist.

### Claude

Claude is best used as a reviewer or design partner when the next step is architectural.

Suggested handoff prompt:

```text
请复核 AniTechou 的 AI 推荐与标签架构。重点看：
1. AIService 的推荐 prompt 是否能稳定输出“相似/口味/拓展/补课”四类推荐理由。
2. MainWindow.xaml.cs 中 AI 意图分发是否应该拆出独立控制器或服务。
3. 当前 TagPolicy 是否足够，还是应该长期升级为 TagCategory / People 表。
请只做架构评审和改造建议，不直接大改代码。
```

### Gemini

Gemini is useful for product and data-source research through Chrome.

Research topics:

- Bangumi, MAL, and AniList coverage differences for Chinese/Japanese ACGN collections.
- Common tagging systems in ACGN collection tools.
- Recommendation reason copywriting that explains user taste without feeling repetitive.

Suggested prompt:

```text
请调研 ACGN 收藏工具的标签体系和推荐理由表达。重点比较 Bangumi、MAL、AniList 的数据覆盖、人员信息、标签质量，以及收藏工具如何区分题材标签、人员标签、公司/年份/评分等结构化字段。最后给 AniTechou 提出产品化建议。
```

## Update Workflow

1. Start with a small written change brief.
2. Add or update unit tests for the behavior that must not regress.
3. Implement the smallest centralized rule first, then wire UI and service callers into it.
4. For AI behavior, update prompts and add deterministic post-processing where possible.
5. For network behavior, route all external HTTP calls through `NetworkClientFactory`.
6. Run targeted tests, then full build and full test suite.
7. For real data operations, export a backup and use preview mode before batch changes.

## Next Optimization Ideas

- Add a full-library tag cleanup preview with export and rollback logs.
- Split `MainWindow.xaml.cs` AI handling into separate command handlers.
- Persist recommendation categories and reasons so users can filter by “similar”, “taste”, “explore”, and “classic”.
- Add provider health status to search results so users understand when Bangumi fell back to MAL/AniList.
- Consider structured tables for people and tag categories if filtering by staff/cast becomes central.

## Desktop Pet and AI Persona Follow-Up

Last updated: 2026-06-06

- Keep the v0.9.5 desktop pet dialogue box local and lightweight first. It should prove the interaction surface before AI calls are wired in.
- For future AI integration, split prompts into a stable assistant capability layer and a selectable character persona layer. The persona layer should be chosen by current pet/role configuration, not hard-coded into `AIService`.
- Model role packages as data: visual assets, state mappings, short lines, persona prompt, safety/behavior constraints, and fallback replies should be loaded from a role manifest.
- Route desktop pet messages through the same AI service only after there is a small controller that can decide whether a message is a pet-only quip, an app command, or a real AI assistant request.
- Keep long AI answers in the main assistant UI or a dedicated chat panel. The desktop pet bubble should show short summaries, status, or character-flavored reactions so it does not block the workspace.
- Add QA affordances for pet assets and behavior: state trigger buttons, current state display, role manifest validation, and contact-sheet review for new visual packs.
