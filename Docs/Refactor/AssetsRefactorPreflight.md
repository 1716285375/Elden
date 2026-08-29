# Assets Refactor — Phase 0 Preflight Report

> Generated: 2026-08-29
> Status: **ANALYSIS ONLY — no assets were moved, no code was modified**
> Branch: `main` (dirty worktree baseline recorded in `git_status_baseline_phase0.txt`)

---

## 1. Unity / Tooling State

| Item | Value |
|---|---|
| Unity Editor | Running (multiple `Unity.exe` processes) |
| Unity MCP bridge | Available at `http://localhost:27786` (144 tools registered) |
| Currently opened scene | `SCN_LV01_R03_A01_Base` (clean, not dirty) |
| Compile errors (Editor.log) | 0 (`Compilation failed` not found) |
| Console errors (recent) | `MissingReferenceException: Material destroyed` (UIElements editor-render issue, not a project compile error) |
| `.meta` files with GUID | 12,593 |
| Duplicate GUIDs | **0** |
| Orphan `.meta` (meta without file) | **0** |
| Files without `.meta` | 1 (`Assets/Plugins/NuGet/.nuget-installed.json` — runtime nuget marker, normal) |

> Note: `tests-run` baseline was not executed in Phase 0 because the worktree has
> 190 uncommitted changes and dirty scenes abort the test runner. The EditMode
> test baseline should be captured at the start of Phase 1 on the refactor branch.

---

## 2. Current `Assets/` Root

```
Assets/
├── Art/                  (visual art + audio + fonts + UI sprites)
├── Data/                 (gameplay data + prefabs + materials + meshes + icons + animations + animator overrides + settings)
├── Editor/               (72+ editor scripts, flat)
├── Plugins/              (NuGet, ParrelSync — keep)
├── ProBuilder Data/      (keep)
├── Resources/            (Effects + World Locations)
├── Scenes/               (Bootstrap/Frontend/Levels/Persistent/Tests)
├── Screenshots/          (34 dev screenshots)
├── Script/               (236 runtime scripts)
├── Settings/             (8 rendering settings assets)
├── Tests/                (Editor/ — 48 test scripts + ZZ.SaveSystem.EditorTests.asmdef)
├── TextMesh Pro/         (keep)
├── Thirdparty/           (Ciathyza — rename to ThirdParty)
├── _GameTestShader/      (SwordSlash VFX authoring: production + test mixed)
├── DefaultNetworkPrefabs.asset
├── InputSystem_Actions.inputactions
├── PlayerControls.cs
├── PlayerControls.inputactions
└── *.meta for each of the above
```

---

## 3. Asset Counts

| Area | Count |
|---|---|
| `.anim` clips | 1,980 |
| Animator Controllers / Overrides | 69 |
| `.mat` materials | 641 |
| `.png` textures | 3,223 |
| `.obj` models | 3,142 |
| `.wav` audio | 604 |
| Prefabs under `Data/Prefabs/` | 60 |
| Runtime scripts (`Script/`) | 236 |
| Editor scripts (`Editor/`) | 72 |
| Test scripts (`Tests/Editor/`) | 48 |

---

## 4. asmdef Inventory (exactly 2 — no action)

| Path | Name |
|---|---|
| `Assets/Script/Save System/ZZ.SaveSystem.asmdef` | `ZZ.SaveSystem` |
| `Assets/Tests/Editor/ZZ.SaveSystem.EditorTests.asmdef` | `ZZ.SaveSystem.EditorTests` (refs `ZZ.SaveSystem`, Editor-only, NUnit) |

Both must move **intact** with their `.meta` files. Do **not** rename them.

---

## 5. Input System Generated Wrapper

| Asset | `generateWrapperCode` | `wrapperCodePath` | Class / Namespace |
|---|---|---|---|
| `Assets/PlayerControls.inputactions` | **1** | `Assets/PlayerControls.cs` | `PlayerControls` / `ZZ` |
| `Assets/InputSystem_Actions.inputactions` | 0 | — | — |

- `PlayerControls` class is used by **20+ files** (`new PlayerControls()` in `PlayerInputManager.cs`, Editor setups, tests). Moving the file is compile-safe (global assembly), but the `.inputactions` meta field `wrapperCodePath` must be updated to the new location or regeneration will recreate `Assets/PlayerControls.cs` at the root.
- `InputSystem_Actions.inputactions` has **zero** code references and **zero** GUID references in prefabs/scenes/assets → record as unused candidate, do not delete.

---

## 6. Hardcoded Path Reference Summary

### 6.1 `"Assets/Scenes"` — 12 occurrences (all must be updated)

| File | Line | Usage |
|---|---|---|
| `Assets/Script/Save System/WorldScenePathLayout.cs` | 8 | `ScenesRoot = "Assets/Scenes"` — **the single source of truth** for scene paths; 34 files depend on it |
| `Assets/Tests/Editor/*` | 11 | literal scene paths in tests |

`WorldScenePathLayout.cs` is **the only runtime path constant**. All runtime scene
resolution (`WorldSceneManager`, `WorldSaveGameManager`, `PlayerInputManager`,
`PlayerManager`) funnels through it. Updating `ScenesRoot` to `Assets/_Game/Scenes`
is the required "path string only" change — no streaming architecture change.

### 6.2 `"Assets/Data"` — 471 occurrences (Editor setups + tests)

Path-string references only (e.g. `Assets/Data/Prefabs/Player.prefab`,
`Assets/Data/Animator Overrides/Weapons/`). All are in `Editor/` setup tools and
`Tests/Editor/` tests. **No runtime script hardcodes `Assets/Data`.**

### 6.3 `"Assets/Art"` — 211 occurrences (Editor setups + tests)

Same pattern as above — Editor setup tools + tests referencing animation/model/
material/audio paths. **No runtime script hardcodes `Assets/Art`.**

### 6.4 `"Assets/Script"` — 88 occurrences (mostly tests)

12 use `File.ReadAllText($"Assets/Script/{relativePath}")` to read source files for
validation. These must be updated when `Script` moves to `_Game/Scripts`.

### 6.5 `"Assets/Resources"` — 26 occurrences

- `WorldSceneSubSceneManager.k_WorldLocationResourcesPath = "World Locations"` — **relative Resources path**, unaffected by folder move (`Resources` → `_Game/Resources` keeps relative path).
- Editor setups + tests reference `Assets/Resources/Effects/*` asset paths.

### 6.6 `"Assets/Settings"` — 3 occurrences

`World Streaming Probe Volume Baking Set.asset` referenced in
`LargeWorldSceneSetup.cs`, `WorldStreamingSystemSetup.cs`, `WorldStreamingSystemTests.cs`.

### 6.7 `Assets/_GameTestShader` — 0 code references

No `.cs` references the `_GameTestShader` path. But `SwordSlashCoreConfigurator.cs`
hardcodes **future** paths: `Assets/_Game/VFX/Combat/SwordSlash/Materials` and
`Assets/_Game/Rendering/Pipeline`, plus `Assets/Data/Prefabs/Weapons/Melee Weapons/...`.

---

## 7. Resources.Load Audit (runtime relative paths — must remain valid)

| Caller | Relative path | Status under `Resources → _Game/Resources` |
|---|---|---|
| `WorldCharacterEffectsManager` | `"Effects/..."`, `"Effects/Dead Spot"`, `"Effects/Poisoned VFX"`, `"Effects/Frostbite VFX"`, `"Effects/Frozen Material"` | **Safe** — relative path unchanged |
| `WorldSceneSubSceneManager` | `Resources.LoadAll<WorldLocationSceneSet>("World Locations")` | **Safe** — relative path unchanged |
| `BlockingSystemSetup`, `DeadSpotSystemSetup`, `FrostbiteSystemSetup` (Editor) | `Resources.Load("Effects/...")` | Safe |

**Conclusion:** moving `Assets/Resources` → `Assets/_Game/Resources` is safe; all
runtime loads are relative. No `Addressables` usage found anywhere.

---

## 8. Scene Path System Audit

- `EditorBuildSettings.asset` lists **22 scenes**, all `Assets/Scenes/...` paths — must be rewritten when scenes move.
- `WorldLocationSceneSet` assets (`Resources/World Locations/R01-R05`) reference scenes by **scene ID string** (`SCN_LV01_R01_A01_Base`), resolved through `WorldScenePathLayout.GetScenePath(sceneID)` → **follows the layout constant automatically**.
- `m_requiredLocations` cross-references use GUIDs → safe with `.meta` preserved.
- LV01 region structure (R01–R05 × Base/Props/Effects/Spawners + `Dev/`, `Shared/`, master scene) is intact and matches the plan. **Do not restructure.**
- `SceneManager` / `EditorSceneManager` calls in Editor setups resolve paths from constants (mostly `k_WorldScenePath`, `k_MainMenuScenePath` defined in each setup file as `Assets/Scenes/...` literals) — each must be updated.
- `WorldSceneManager` uses `SceneUtility.GetScenePathByBuildIndex` for the master scene — follows Build Settings, safe once build settings paths are updated.

---

## 9. `_GameTestShader` Ownership Analysis

### 9.1 VFX assets (Materials/Meshes/Shaders/Textures) — production → `_Game`

17 materials (`M_SwordSlash_*`), 17 textures (`T_SwordSlash_*`), 2 shadergraphs,
empty Meshes dir. Only referenced inside `_GameTestShader`. Plan maps them to
`_Game/Art/VFX/Combat/SwordSlash/{Materials,Meshes,Shaders,Textures}`.

### 9.2 VFX prefabs — production → `_Game`

- `VFX_SwordSlash_Hit_Blue.prefab` (guid `c3b47242…`) → `_Game/Prefabs/VFX/Combat/SwordSlash/`
- `VFX_SwordSlash_Swing_Blue.prefab` (guid `f6f340ae…`) → same
- Not referenced outside `_GameTestShader` today; moving with GUIDs intact is safe.

### 9.3 Sword prefabs — **TEST COPIES, different GUIDs** → `_Dev`

| File | GUID | Verdict |
|---|---|---|
| `_GameTestShader/.../Prefabs/Broadsword.prefab` | `34f00405…` | **different** from `Data/.../Broadsword.prefab` (`c3c3759b…`) → test copy |
| `_GameTestShader/.../Prefabs/Straight Sword.prefab` | `b90e5649…` | **different** from `Data/.../Straight Sword.prefab` (`053ee4bc…`) → test copy |
| `Data/Prefabs/Weapons/Melee Weapons/Straight Sword.prefab` | `053ee4bc…` | the real weapon, referenced 16× in `VFX_SwordSlash_WeaponTest.unity` |

→ test copies go to `_Dev/VFXAuthoring/SwordSlash/Fixtures/Weapons/`.

### 9.4 Scripts — split by reference evidence

| Script | Referenced by | Verdict |
|---|---|---|
| `SwordSlashTestDriver.cs` | only `VFX_SwordSlash_WeaponTest.unity` | **test driver** → `_Dev/VFXAuthoring/SwordSlash/Scripts/` |
| `SwordSlashVFXPlayer.cs` | only `VFX_SwordSlash_WeaponTest.unity` | **AMBIGUOUS** — production-grade procedural trail component, no formal prefab ref yet → prefer `_Game/Scripts/VFX/Combat/SwordSlash/` |
| `SwordSlashHitVFXSpawner.cs` | only `VFX_SwordSlash_WeaponTest.unity` | **AMBIGUOUS** — production-grade hit-VFX spawner → prefer `_Game/Scripts/VFX/Combat/SwordSlash/` |
| `SwordSlashCoreConfigurator.cs` | Editor tool; hardcodes `Assets/_Game/VFX/...` and `Assets/_Game/Rendering/Pipeline` | → `_Dev/VFXAuthoring/SwordSlash/Editor/` (authoring tool) |

### 9.5 Authoring scenes + BG materials → `_Dev`

`VFX_SwordSlash_Authoring.unity`, `VFX_SwordSlash_WeaponTest.unity`,
`M_BG_Black.mat`, `M_BG_BrightBlue.mat`, `M_BG_Gray.mat` → `_Dev/VFXAuthoring/SwordSlash/{Scenes,Materials}/`.

### 9.6 Test rendering pipeline → `_Dev` (duplicate candidate)

`_GameTestShader/Rendering/Pipeline/{PC_RPAsset,PC_Renderer}.asset` and
`Rendering/Volume/VP_Global.asset` — compare GUIDs against `Assets/Settings` counterparts
before moving; if duplicates, record in `AssetsRefactorDuplicateCandidates.md`.

---

## 10. Ambiguous / High-Risk Items

1. **`Art/Materials/General/` + `Art/Textures/General/` + `Art/Models/Equipment/Weapons/General/`** — 60+ "General" materials/textures with unknown ownership (e.g. `Arrow3bcg.mat`, `BUFF.mat`, `Circle38cg.mat`). Keep in place in Phase 1; audit separately.
2. **`Art/Models/Rigged/`** — contains **prefabs** (`Crow_01.prefab`, `Undead_Dog_01.prefab`, `Golem_01.prefab`, …) alongside models. These are character rig prefabs; ownership of each needs per-creature confirmation before Domain mapping.
3. **`Art/Models/Physics/GeneratedColliders/`** — 50+ `Generated convex submesh *.obj` files (physics-baked meshes). Likely shared → `_Game/Art/Shared/Meshes/` or keep under Physics.
4. **`Art/Models/ProBuilder/`** — `pb_Mesh-*.obj` exports; likely legacy generated, do not touch in this phase.
5. **`Data/Prefabs/Word Managers/`** — confirmed misspelling of "World Managers" (contents: Player Input/UI Manager, World AI/Item Database/Network/Save Game Manager). Safe to rename to `World/Managers` during Prefabs phase, keeping `.meta`.
6. **`Scenes/Bootstrap/` and `Scenes/Persistent/`** — empty (only `.meta`). Kept for structure.
7. **`Scenes/Tests/`** — empty subfolders (AI/Animation/Character/Combat/Environment/VFX, `.meta` only). Keep as structure placeholders.
8. **`SwordSlashVFXPlayer.cs` / `SwordSlashHitVFXSpawner.cs`** — see §9.4.
9. **`DefaultNetworkPrefabs.asset`** — referenced by Netcode config; verify NetworkManager/prefab-list references after move.
10. **`UniversalRenderPipelineGlobalSettings.asset`** — referenced from ProjectSettings (GraphicsSettings), not via asset GUID search; safe to move with `.meta`, then verify ProjectSettings/GraphicsSettings.asset path.

---

## 11. Unused / Legacy Candidates (record only — **DO NOT DELETE**)

See `AssetsRefactorUnusedCandidates.md` for the full list. Highlights:
- `Assets/InputSystem_Actions.inputactions` — zero references
- `Assets/Data/Prefabs/Player_Backup_Old.prefab` — legacy backup
- `Assets/Art/Materials/Prototype_Material_01.mat`, `Assets/Art/Textures/Prototype_Texture_01.png` — prototypes
- 34 screenshots in `Assets/Screenshots/` — no Unity asset references (safe to move to `Docs/Screenshots/`)

---

## 12. Preflight Conclusions

1. **GUID integrity is currently perfect** (0 duplicates, 0 orphans) — any move must preserve `.meta` to keep it that way.
2. **Scene streaming is path-layout-driven**, not hardcoded per-file in runtime — one constant change (`WorldScenePathLayout.ScenesRoot`) + Build Settings rewrite covers it.
3. **Resources loads are relative** — `Assets/Resources` → `Assets/_Game/Resources` is safe.
4. **Input wrapper regeneration** requires updating `PlayerControls.inputactions.meta` → `wrapperCodePath`.
5. **The bulk of hardcoded paths live in `Editor/` setup tools and `Tests/Editor/`** (471 Data + 211 Art + 88 Script) — these are code changes that must accompany the moves phase-by-phase (path strings only).
6. **SwordSlash split is well-defined** by GUID/reference evidence (production VFX vs test fixtures vs authoring tools).

### Recommended execution order (unchanged from plan)
Phase 1 (structure) → Phase 2 (root files) → Phase 3 (Scripts) → Phase 4 (Data/Prefabs)
→ Phase 5 (Art+Audio) → Phase 6 (SwordSlash) → Phase 7 (Editor) → Phase 8 (Tests)
→ Phase 9 (Scenes) → Phase 10 (Resources) → Phase 11 (Screenshots) → Phase 12 (ThirdParty).

### Worktree safety
The worktree has 190 uncommitted changes (feature work in progress). Before Phase 1,
create the `refactor/assets-structure` branch from this exact state so every refactor
commit is isolated and `main` stays untouched.
