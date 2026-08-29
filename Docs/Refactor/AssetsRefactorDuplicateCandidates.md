# Assets Refactor — Duplicate Candidates

> Generated: 2026-08-29 (Phase 0)
> **DO NOT DELETE OR AUTO-MERGE.** Compare and record; human decides.

---

## 1. VFX Authoring rendering pipeline vs project settings

| Authoring copy (`_GameTestShader/Rendering`) | GUID | Project settings (`Assets/Settings`) | GUID | Verdict |
|---|---|---|---|---|
| `Pipeline/PC_RPAsset.asset` | `fc9ca20f…` | `PC_RPAsset.asset` | `4b83569d…` | **Different GUID, different content** → dedicated VFX-authoring pipeline, not a strict duplicate. Move to `_Dev/VFXAuthoring/SwordSlash/Settings/Rendering/`. |
| `Pipeline/PC_Renderer.asset` | `c0fabf18…` | `PC_Renderer.asset` | `f288ae1f…` | Different GUID/content → same handling as above. |
| `Volume/VP_Global.asset` | `1f8cdede…` | `DefaultVolumeProfile.asset` | `ab09877e…` | Different GUID and different internal references → dedicated authoring volume. Same handling as above. |

> These are recorded as *possible* duplicates only because the plan §76 asks to
> compare GUIDs first. Content comparison shows they are authoring-specific, so the
> expected Phase 6 destination is `_Dev/VFXAuthoring/SwordSlash/Settings/Rendering/`.

## 2. Art Material/Texture pairs with variants (not duplicates — informational)

- `Art/Textures/General/B2.png` + `B2_Variant_01.png`, `48_Ring.png` + `48_Ring_Variant_01.png`, etc. — variant families, keep together.
- `Art/Materials/General/BUFF.mat` + `BUFF_Variant_01..03.mat` — variant family.

## 3. Test sword prefab copies (already classified as fixtures, not duplicates)

- `_GameTestShader/.../Broadsword.prefab` (`34f00405…`) vs real `Data/.../Broadsword.prefab` (`c3c3759b…`) — different assets, authoring fixture → `_Dev`.
- `_GameTestShader/.../Straight Sword.prefab` (`b90e5649…`) vs real weapon (`053ee4bc…`) — same.

---

## Notes

No true duplicate GUIDs exist in the project (12,593 metas, 0 dupes). "Duplicate"
here means functionally-similar assets that may be consolidated by a human later.
