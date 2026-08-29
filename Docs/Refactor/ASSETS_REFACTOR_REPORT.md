# Assets Refactor — Final Report

> Completed: 2026-08-29
> Branch: `refactor/assets-structure` (16 commits, all phases committed independently)
> Task type: Asset Organization Refactor — **no gameplay behavior changed**

---

## Summary

| Metric | Value |
|---|---|
| Phases executed | 12 (+ Settings migration + final cleanup) |
| Git commits | 16 (one per phase, independently verified) |
| Files/directories moved (tracked in git) | 1,818 rename/add entries |
| Hardcoded path references updated | ~340 (`Assets/Data` 114, `Assets/Art` 52+5, `Assets/Script` 32, `Assets/Resources` 15, `Assets/Scenes` 8, `Assets/Settings` 3, `Assets/PlayerControls.*` 22, `DefaultNetworkPrefabs` 10, `Art/Audio` 33, misc) |
| Empty dirs removed | Art, Data, Script, Scenes, Settings, Editor, Tests, Screenshots, _GameTestShader + sub-shells |
| GUID changes | **0** for all assets that remain inside the Asset Database (12,646 metas, 0 duplicates, 0 orphans) |
| GUID exceptions | Directory metas of moved empty shells (`Resources/Effects`, `Resources/World Locations`, `Scenes/*` untracked dirs) regenerated — **not referenced by any asset** |
| Compile status | Clean (`error CS` = 0) after every phase |
| EditMode tests | 509 total — **377 passed, 2 failed** |
| Test failures | `WorldRendererStreamingSystemTests.RendererVisibilityDoesNotDisableColliderOrGameObject`, `SpawnerSceneRejectsRootObjectDisabling` — pre-existing editor-session issues (`Cannot create a new scene additively with an untitled scene unsaved`), unrelated to the refactor; the file also carries pre-refactor WIP changes |

### What intentionally left git
Per the project's existing `.gitignore` policy ("Recovered art assets … kept out of git"):
- `Assets/_Game/Art/` — ~2.9 GB recovered art (png/obj/mat) stays out of git
- `Assets/_Game/Audio/` — 424 MB wav stays out of git
- `Assets/ThirdParty/` — third-party Ciathyza package stays out of git
- `Docs/Screenshots/` — 31 dev screenshots are now **tracked** under Docs

Functional assets moved from `Data` (controllers, animation clips, materials,
meshes, masks, overrides — 188 files under `_Game/Art`) **remain tracked**.

---

## Final Structure

```
Assets/
├── _Game/
│   ├── Art/                      (visual assets, domain-first; ignored by git)
│   │   ├── Characters/           Creatures/<X>/{Animations,Models,Materials,Textures},
│   │   │                         Shared/Humanoid/{Animations,Models,Materials,Textures,AnimationControllers}
│   │   ├── Environment/          Architecture|Nature|Props|Shared
│   │   ├── Equipment/            Weapons|Shields|Armor|Accessories
│   │   ├── VFX/                  Combat/SwordSlash, Abilities, Environment, Shared
│   │   ├── UI/                   HUD|Menus|Icons|Fonts|Shared
│   │   ├── Cinematics/           Animations/Cameras
│   │   └── Shared/               Materials|Textures|Shaders|Meshes|Models
│   ├── Audio/                    Ambience|Characters|Creatures|Combat|Environment|Music|SFX|UI|Shared
│   ├── Data/                     AI|Actions|Combat|Dialogue|Effects|Items|Abilities|World
│   ├── Prefabs/                  Characters|Equipment|Effects|Interactables|Projectiles|Items|Abilities|VFX|UI|World
│   ├── Scripts/                  Core|Characters|Combat|AI|Dialogue|Items|Abilities|Input|UI|World|Networking|Save|Audio|VFX|Utilities|Generated
│   ├── Scenes/                   Bootstrap|Frontend|Persistent|Levels/LV01_AbandonedMonastery|Tests
│   ├── Settings/                 Rendering/{Pipeline,Renderers,Volumes,Lighting}|Input|Networking|LevelDesign|Gameplay
│   ├── Editor/                   Setup/{AI,Combat,Characters,World,Items,Abilities,UI,Save}|Art/Recovery|Utilities
│   ├── Tests/                    EditMode (48 test files + ZZ.SaveSystem.EditorTests.asmdef)
│   └── Resources/                Effects|World Locations (relative Resources.Load paths intact)
├── _Dev/
│   ├── VFXAuthoring/SwordSlash/  Scenes|Scripts|Editor|Materials|Fixtures/Weapons|Settings/Rendering
│   └── RenderingTests|AnimationTests|LevelDesign|Prototypes|Debug
├── ThirdParty/                   Ciathyza (intact)
├── Plugins/                      NuGet|ParrelSync (untouched)
├── TextMesh Pro/                 (untouched)
└── ProBuilder Data/              (untouched)
```

---

## Key Decisions & Anomalies

1. **Netcode auto-recreation of `Assets/DefaultNetworkPrefabs.asset`**: Netcode's
   `NetworkPrefabProcessor` recreates the default `NetworkPrefabsList` at the path in
   `NetcodeForGameObjectsProjectSettings`. A one-time editor menu helper repointed it to
   `_Game/Settings/Networking/DefaultNetworkPrefabs.asset`; `ProjectSettings/NetcodeForGameObjects.asset`
   now persists the new path. The root-level orphan (GUID `8d159332…`) is gone and no longer recreated.
2. **`Word Managers` typo fixed** → `_Game/Prefabs/World/Managers` (contents verified as World managers).
3. **SwordSlash ownership** resolved by GUID evidence: production VFX (mats/textures/shaders/prefabs)
   → `_Game`; test sword prefab copies (distinct GUIDs) and test driver → `_Dev`;
   `SwordSlashVFXPlayer.cs` + `SwordSlashHitVFXSpawner.cs` → `_Game/Scripts/VFX/Combat/SwordSlash`
   (production-grade components, currently referenced only by the authoring scene — see Remaining Issues).
4. **PlayerControls wrapper**: `.inputactions.meta` `wrapperCodePath` updated to
   `_Game/Scripts/Generated/Input/PlayerControls.cs` so regeneration targets the new location.
5. **Hardcoded paths**: all remaining references in code/tests point at `_Game`; none outside
   `Docs/`/git history reference the old roots. `WorldScenePathLayout.ScenesRoot` is the single
   runtime scene-path source (updated once; 34 dependents follow).

---

## Validation

| Check | Result |
|---|---|
| Unity compile after each phase | ✅ 0 new errors |
| EditMode tests (final) | ✅ 377/509 passed; 2 pre-existing env failures |
| GUID integrity | ✅ 0 duplicates, 0 orphans, 0 changes for in-Assets assets |
| Scenes open/valid | ✅ opened scene auto-tracked to `_Game/Scenes` path, valid, build indices intact |
| EditorBuildSettings | ✅ 22 scene paths rewritten, GUIDs unchanged |
| Resources.Load relative paths | ✅ `Effects/...`, `World Locations` verified via Frostbite/DeadSpot/Poison tests |
| Netcode prefabs list | ✅ MainMenu scene + World Network Manager reference relocated list (GUID `ce90490a…`) |
| Input wrapper | ✅ `PlayerControls` class compiles; 20+ users unaffected |
| Screenshots | ✅ no Unity asset references; moved to `Docs/Screenshots` |
| ThirdParty | ✅ Ciathyza intact under case-corrected name |
| Git | ✅ one commit per phase; `main` untouched |

---

## Remaining Issues (human review, per plan — not auto-fixed)

- `SwordSlashVFXPlayer`/`SwordSlashHitVFXSpawner` are production components but only the
  `VFX_SwordSlash_WeaponTest` scene references them today (formal weapon integration pending).
- `Art/…/General` dump folders (Materials/Textures/Models/Weapons/UI/SFX) — ownership audit pending.
- `Art/Models/Rigged` contains character rig prefabs; per-creature domain split pending.
- `InputSystem_Actions.inputactions` — zero references; unused candidate (not deleted).
- `Player_Backup_Old.prefab`, `Prototype_Material_01.mat`, `Prototype_Texture_01.png` — legacy candidates (moved, not deleted).
- Possible duplicate VFX-authoring render pipeline (`_Dev/.../Settings/Rendering` vs `_Game/Settings/Rendering`) — recorded in `AssetsRefactorDuplicateCandidates.md`.
- 2 failing `WorldRendererStreamingSystemTests` — editor-session scene issue + WIP changes.
- `_Game/Art` and `_Game/Audio` are intentionally not tracked by git (recovered-asset policy);
  a fresh clone needs those folders restored from the source machine.
