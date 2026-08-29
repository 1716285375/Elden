# General Folder Audit

> Generated: 2026-08-29 (Phase 0)
> Per plan §18: existing `General` folders are **kept** in Phase 1; contents audited
> here for later, separate cleanup. No new `General/Misc/Others/Temp` folders may be
> created going forward.

---

## 1. Animation `Combat/General` folders (structural — keep as-is)

These follow the documented creature animation layout (`Actions/Combat/Emotes/…`)
and are *not* junk:

- `Art/Animations/Characters/Creatures/Demon/Combat/General`
- `Art/Animations/Characters/Creatures/Dog/Combat/General`
- `Art/Animations/Characters/Creatures/Golem/Combat/General`
- `Art/Animations/Characters/Creatures/Mimic/Combat/General`
- `Art/Animations/Characters/Creatures/Undead/Combat/General`
- `Art/Animations/Characters/Creatures/Werewolf/Combat/General`
- `Art/Animations/Characters/Humanoid/Combat/General` (shared humanoid combat pool — many Editor setups reference it, e.g. `SpellSystemSetup`, `RiposteSystemSetup`)

## 2. Material/Texture `General` folders (audit candidates — keep, do not sort in this phase)

| Folder | Contents |
|---|---|
| `Art/Materials/General/` | 60+ mats (`Alek_Material_01`, `Arrow3bcg`, `BUFF`, `Breakable_Material7`, `Circle*.mat`, …). Mixed ownership (character, weapon, environment). |
| `Art/Textures/General/` | 50+ textures (`48_Ring`, `AreaTex`, `Arrow*`, `B1/B2`, `Circle*`, …). Mixed ownership. |
| `Art/Models/Equipment/Weapons/General/` | general weapon models (shared across weapon types) |
| `Art/Textures/UI/General/` | general UI textures |
| `Art/Audio/SFX/General/` | general SFX (`SFX_Luna_Line_Farewell_Wanderer.wav` referenced by `WeaponUpgradeSystemSetup`) |

**Recommendation:** leave all `General` folders in place for Phase 1–12; resolve
ownership in a dedicated future task. Several items are referenced by Editor setup
tools (e.g. `WeaponUpgradeSystemSetup` → `Art/Audio/SFX/General/…`, `Art/Audio/UI/…`),
so path updates still apply during Phase 5, but the folder *name* stays `General`.
