# AniTechou v0.9.5 Release Checklist

## Version

- [x] `AniTechou.csproj` version: `0.9.5`
- [x] `AniTechou.csproj` assembly/file version: `0.9.5.0`
- [x] `Installer/AniTechou.iss` `MyAppVersion`: `0.9.5`
- [x] Git tag: `v0.9.5`
- [x] GitHub Release title: `AniTechou v0.9.5`

## Local Verification

- [x] `dotnet build -c Release`
  - Result: 0 warnings, 0 errors
- [x] `dotnet test Tests\AniTechou.Tests.csproj -c Release --no-build`
  - Result: 104 passed
- [x] Release app visual smoke test
  - Desktop pet transparent PNG display
  - Double-click dialogue
  - Local short reply
  - State buttons
  - Hide/close buttons
  - Drag movement and position persistence
  - Context menu visibility

## Package

- [x] `dotnet publish AniTechou.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64-self-contained`
- [x] Confirmed pet assets copied into publish output:
  - `Assets/Pets/Dalian/pet.json`
  - `idle_256x256_1f.png`
  - `thinking_256x256_1f.png`
  - `pleased_256x256_1f.png`
  - `annoyed_256x256_1f.png`
  - `moving_256x256_1f.png`
  - `sleeping_256x256_1f.png`
- [x] Installer package generated:
  - `Installer/output/AniTechou-Setup-v0.9.5.exe`
  - SHA256: `3434784FEA24B8CA2F601E335D69CA305024862EF43D427F28A768AFB19CF8EB`

## Documentation To Review Before GitHub Release

- [x] `docs/RELEASE_NOTES_v0.9.5.md`
- [x] `docs/GITHUB_RELEASE_DRAFT_v0.9.5.md`
- [x] `docs/V0.9.6_ROADMAP.md`
- [x] `docs/DEVELOPMENT.md`
- [x] `docs/AI_COLLABORATION_ROADMAP.md`
- [x] `docs/V0.9.5_NOTES_AND_DALIAN_PET.md`

## GitHub Release Actions

- [x] Stage intended files only.
- [x] Commit v0.9.5 changes.
- [x] Push to `origin/main`.
- [x] Create tag `v0.9.5`.
- [x] Create GitHub Release using `docs/GITHUB_RELEASE_DRAFT_v0.9.5.md`.
- [x] Upload release asset:
  - `Installer/output/AniTechou-Setup-v0.9.5.exe`
- [x] Verify published Release:
  - https://github.com/lunatic1157/AniTechou/releases/tag/v0.9.5
