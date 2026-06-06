# AniTechou Update Checkpoint - 2026-06-04

## Purpose

This document records the current development checkpoint after the v0.9.4 push point. It is intended as a handoff note for future feature work, release preparation, and AI-assisted continuation.

## Baseline

- Last pushed reference observed in git: `9d3ca9f docs: v0.9.4 开发复盘 + 后续优化方向`
- Current project version fields still show `0.9.4` in:
  - `AniTechou.csproj`
  - `Installer/AniTechou.iss`
- This checkpoint is code-complete for the current stage, but not yet packaged as a formal release.

## Completed In This Stage

### Tag Normalization And Cleanup

- Added centralized automatic tag normalization through `Utilities/TagPolicy.cs`.
- AI-added, AI-updated, Bangumi-completed, and external-search tags now pass through the same cleanup policy.
- Redundant automatic tags are removed or skipped:
  - year and date tags
  - rating tags
  - type/status/season/source-type tags
  - company/studio tags duplicated by work fields
- People and creative tags are standardized with prefixes:
  - `cv:`
  - `导演:`
  - `脚本:`
  - `音乐:`
  - `角色设计:`
  - `原作:`
  - `插画:`
- Added full-library tag cleanup preview and apply flow through `Windows/TagCleanupPreviewWindow.xaml(.cs)`.
- Applied cleanup to `testuser` after backup; post-cleanup preview reported zero remaining cleanup actions.

### Network And Proxy Foundation

- Added `Services/NetworkClientFactory.cs`.
- Added proxy modes:
  - system proxy
  - custom proxy
  - no proxy
- Routed AI and external search HTTP clients through the shared network factory.
- Added settings-page network diagnostics for configured data/API targets.

### AI Recommendation Quality

- Added recommendation fields to AI work results:
  - `recommendationCategory`
  - `recommendationReason`
- Local existing-work filtering is preserved before rendering recommendation cards.
- Added `Services/RecommendationProfile.cs` to extract local taste signals:
  - high-rated works
  - completed works
  - currently watching/reading works
  - common regular tags
  - people tags
  - type preferences
- Recommendation prompts now explicitly ask for:
  - `相似推荐`
  - `口味推荐`
  - `拓展推荐`
  - `补课推荐`
- Recommendation reasons must explain which local profile signals were used.

### AI Panel Light Split

- Added `Services/AIRecommendationDisplayHelper.cs`.
- Moved recommendation category normalization and empty category/reason fallback display logic out of `MainWindow.xaml.cs`.
- `MainWindow.xaml.cs` still contains substantial AI chat and result handling logic; deeper command-handler extraction remains future work.

### Tests Added Or Expanded

- Added `Tests/TagPolicyTests.cs`.
- Added `Tests/WorkServiceTagPolicyIntegrationTests.cs`.
- Added `Tests/NetworkClientFactoryTests.cs`.
- Added `Tests/RecommendationProfileTests.cs`.
- Added `Tests/AIRecommendationDisplayHelperTests.cs`.
- Expanded `Tests/AIServiceTests.cs`.
- Expanded `Tests/SearchProviderTests.cs`.

## Verification

Last verified commands:

```powershell
dotnet build -c Release
dotnet test Tests\AniTechou.Tests.csproj -c Release
```

Results:

- Build passed with 0 warnings and 0 errors.
- Test suite passed: 104/104.
- Computer Use was available in the verification thread.
- Release build was launched with `testuser`.
- AI recommendation UI check confirmed visible recommendation cards with category and reason blocks.
- No recommendation card "add to list" action was clicked during UI verification.

## Documentation Added

- `docs/AI_COLLABORATION_ROADMAP.md`
- `docs/TAG_CLEANUP_PREVIEW_REVIEW.md`
- `docs/UPDATE_CHECKPOINT_2026-06-04.md`
- `docs/RELEASE_NOTES_v0.9.5.md`
- `docs/V0.9.5_NOTES_AND_DALIAN_PET.md`
- `docs/DALIAN_PERSONA_GUIDE.md`
- `docs/DALIAN_PET_ASSET_PRODUCTION.md`

## v0.9.5 Second-Half Additions

- Note editor layout changed from top/bottom Markdown edit-preview split to left/right split.
- Note split ratio is persisted through `AppConfig.NoteEditorSplitRatio`.
- Markdown preview refresh now uses a short debounce timer.
- `MarkdownConverter` reuses a static Markdig pipeline.
- Added Dalian desktop pet MVP:
  - `Services/DesktopPetService.cs`
  - `Windows/DesktopPetWindow.xaml(.cs)`
  - `Assets/Pets/Dalian/pet.json`
- Settings now include a Dalian desktop pet enable switch.
- Desktop pet state reacts to AI thinking/result/error and note save success/failure.
- Project and installer version fields were bumped to `0.9.5`.

## Not Yet Done

These are not current-stage blockers, but they should not be mistaken for completed work:

- Version fields have been bumped to `0.9.5`.
- Release notes for `0.9.5` have been written.
- Dalian pet asset production has a dedicated handoff document for the next visual-resource pass.
- A clean release package has not been prepared.
- `publishwin-x64-self-contained/` remains an existing untracked publish directory and should not be committed without explicit release intent.
- AI panel logic has only been lightly split; deeper extraction into command handlers/services remains pending.
- Recommendation category/reason data is not persisted yet.
- Provider health or fallback status is not shown in recommendation/search result cards yet.
- Tag data is still a string-tag system with cleanup rules; it has not been upgraded to structured `People`, `Staff`, or `TagCategory` tables.
- Notes editor layout was not changed in this stage. Previous Markdown editor work already provides top/bottom edit-preview split, not a new left/right notes workspace.

## Release Readiness Assessment

This checkpoint is suitable for a source-code commit and push after reviewing the staged file list.

For a formal release, do these first:

1. Decide the next version number.
2. Update `AniTechou.csproj` version fields.
3. Update `Installer/AniTechou.iss` `MyAppVersion`.
4. Add release notes for the new version.
5. Exclude `publishwin-x64-self-contained/` unless intentionally producing a clean release artifact.
6. Re-run Release build and full tests.
7. Do one final `testuser` smoke test.

## Suggested Next Feature Directions

Recommended order:

1. Deep split of AI chat/result handling from `MainWindow.xaml.cs`.
2. Persist recommendation categories and reasons for history/filtering.
3. Show provider health/fallback status in AI search and recommendation cards.
4. Add a dedicated note-editor layout pass if future note features are planned.
5. Consider structured people/staff/tag-category tables only when staff/cast filtering becomes a real product requirement.
