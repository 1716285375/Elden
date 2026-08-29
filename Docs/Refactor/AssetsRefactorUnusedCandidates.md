# Assets Refactor — Unused / Legacy Candidates

> Generated: 2026-08-29 (Phase 0)
> **DO NOT DELETE ANYTHING IN THIS PHASE.** These are candidates for human review.
> The plan forbids deleting, deduplicating, or auto-replacing assets.

---

## A. Input Actions

| Asset | Evidence |
|---|---|
| `Assets/InputSystem_Actions.inputactions` | Zero `.cs` references; zero GUID references in any prefab/scene/asset; `generateWrapperCode: 0`. Likely abandoned default Input System template. |

## B. Legacy / backup prefabs

| Asset | Evidence |
|---|---|
| `Assets/Data/Prefabs/Player_Backup_Old.prefab` | Name implies backup. No test/editor references found in path scan. |

## C. Prototype assets

| Asset | Evidence |
|---|---|
| `Assets/Art/Materials/Prototype_Material_01.mat` | Prototype naming. |
| `Assets/Art/Textures/Prototype_Texture_01.png` | Prototype naming. |
| `Assets/Thirdparty/Ciathyza/Gridbox Prototype Materials/` | Vendor demo package (complete third-party package — do not break apart). |

## D. Developer screenshots (move to `Docs/Screenshots/`, not delete)

34 files in `Assets/Screenshots/` (`VFX_Phase*.png`, `sceneview.png`, `screenshot.png`, `save-game-ui-validation.png`).
No references from any material/UI/test. These are the only files that leave the Unity Asset Database.

## E. Test copies (move to `_Dev`, not delete)

| Asset | GUID | Notes |
|---|---|---|
| `_GameTestShader/.../Prefabs/Broadsword.prefab` | `34f00405…` | Different GUID from the real `Data/.../Broadsword.prefab` (`c3c3759b…`) → authoring fixture |
| `_GameTestShader/.../Prefabs/Straight Sword.prefab` | `b90e5649…` | Different GUID from the real weapon (`053ee4bc…`) → authoring fixture |
| `_GameTestShader/Rendering/Pipeline/PC_RPAsset.asset`, `PC_Renderer.asset`, `Rendering/Volume/VP_Global.asset` | — | Suspected duplicates of `Assets/Settings` counterparts; compare GUIDs before Phase 6 |

## F. Empty structural folders (keep, not delete)

- `Assets/Scenes/Bootstrap/` — empty (meta only)
- `Assets/Scenes/Persistent/` — empty (meta only)
- `Assets/Scenes/Tests/{AI,Animation,Character,Combat,Environment,VFX}/` — empty (meta only)

## G. Generated / bake artifacts (do not delete)

- `Assets/Art/Models/Physics/GeneratedColliders/` — `Generated convex submesh *.obj`
- `Assets/Art/Models/ProBuilder/` — `pb_Mesh-*.obj`
- `Assets/Scenes/Levels/LV01_AbandonedMonastery/Shared/{Lighting,Navigation,Occlusion}/` — baked data (keep with LV01)

## H. General dump folders (audit separately — see GeneralFolderAudit.md)

- `Assets/Art/Materials/General/` (~60+ mats)
- `Assets/Art/Textures/General/` (~50+ textures)
- `Assets/Art/Models/Equipment/Weapons/General/`
- `Assets/Art/Textures/UI/General/`
- `Assets/Art/Audio/SFX/General/`
- Creature `Combat/General/` animation folders (these follow the documented creature layout — structural, keep)
