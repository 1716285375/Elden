# Assets Refactor — Hardcoded Path References Report

> Generated: 2026-08-29 (Phase 0)
> Full CSV: `Docs/Refactor/AssetsRefactorPathReferences.csv` (811 references)

This report documents every hardcoded `"Assets/..."` asset path found in C# code.
All are **path-string** references (used with `AssetDatabase.LoadAssetAtPath`,
`SceneManager.GetSceneByPath`, `File.ReadAllText`, etc.). None modify gameplay logic.

---

## Summary by root

| Root | Occurrences | Where |
|---|---|---|
| `Assets/Data` | 471 | `Editor/*Setup.cs` tools + `Tests/Editor/*` |
| `Assets/Art` | 211 | `Editor/*Setup.cs` tools + `Tests/Editor/*` |
| `Assets/Script` | 88 | mostly `Tests/Editor` `File.ReadAllText`; also `FogWallInteractableSetup.cs` |
| `Assets/Resources` | 26 | `Editor/*Setup.cs` + `Tests/Editor/*` + (runtime uses **relative** `Resources.Load`) |
| `Assets/Scenes` | 12 | `WorldScenePathLayout.cs` (1, authoritative) + `Tests/Editor/*` (11) |
| `Assets/Settings` | 3 | `LargeWorldSceneSetup.cs`, `WorldStreamingSystemSetup.cs`, `WorldStreamingSystemTests.cs` |

---

## Key non-test references (must be updated with their Phase)

### Scene paths (Phase 9)
- `Assets/Script/Save System/WorldScenePathLayout.cs` — `ScenesRoot = "Assets/Scenes"` (single source of truth; 34 dependents)

### Settings paths (Phase 2 / Phase 4)
- `Assets/Editor/LargeWorldSceneSetup.cs:23` — `"Assets/Settings/World Streaming Probe Volume Baking Set.asset"`
- `Assets/Editor/WorldStreamingSystemSetup.cs:20` — same
- `Assets/Tests/Editor/WorldStreamingSystemTests.cs:22` — same

### Data paths in runtime code — **none**
No runtime (non-Editor, non-Test) script hardcodes `Assets/Data`, `Assets/Art`, `Assets/Settings`, or `Assets/Resources` as absolute asset paths. The only runtime absolute path constant is `WorldScenePathLayout.ScenesRoot`.

### Input wrapper
- `Assets/PlayerControls.inputactions.meta` → `wrapperCodePath: Assets/PlayerControls.cs` must be rewritten when the generated file moves (Phase 2).

---

## By-Phase expected code edits (path strings only)

| Phase | Root moved | Files to patch | Nature |
|---|---|---|---|
| 2 | root files | `PlayerControls.inputactions.meta` wrapperCodePath | config |
| 3 | `Script` → `_Game/Scripts` | 12 test `File.ReadAllText($"Assets/Script/...")` + `FogWallInteractableSetup.cs` + any Editor refs | path strings |
| 4 | `Data` → `_Game/Data` + `_Game/Prefabs` | ~471 refs in Editor setups + tests | path strings |
| 5 | `Art` → `_Game/Art` + `_Game/Audio` | ~211 refs in Editor setups + tests | path strings |
| 7 | `Editor` → `_Game/Editor` | none (no self-paths) | — |
| 8 | `Tests` → `_Game/Tests` | none (no self-paths) | — |
| 9 | `Scenes` → `_Game/Scenes` | `WorldScenePathLayout.cs` + 11 test literals + `EditorBuildSettings.asset` (22 entries) | path strings |
| 10 | `Resources` → `_Game/Resources` | 26 absolute refs in Editor setups/tests (runtime relative loads unaffected) | path strings |

> The `Docs/Refactor/AssetsRefactorPathReferences.csv` file is the authoritative
> machine-readable list; regenerate it before each phase to find remaining refs.
