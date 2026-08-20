#!/usr/bin/env python3
"""Create and finalize a semantic Unity AssetDatabase move plan.

The plan removes recovery-only source-group directories and PathID filename
suffixes while keeping PathID provenance in CSV/JSON reports. Physical moves
are intentionally executed by Unity's AssetDatabase so existing GUIDs survive.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import shutil
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable


PATH_ID_SUFFIX_RE = re.compile(r"__pathid_-?\d+$", re.IGNORECASE)
PATH_ID_VALUE_RE = re.compile(r"__pathid_(-?\d+)$", re.IGNORECASE)


CREATURES = [
    ("bellkeeper", "BellKeeper"), ("bell_keeper", "BellKeeper"),
    ("grave_tender", "GraveTender"), ("gravetender", "GraveTender"),
    ("werewolf", "Werewolf"), ("werwolf", "Werewolf"), ("lycan", "Werewolf"),
    ("skeleton", "Skeleton"), ("undead", "Undead"), ("zombie", "Undead"),
    ("golem", "Golem"), ("hill_giant", "Giant"), ("giant", "Giant"),
    ("mimic", "Mimic"), ("demon", "Demon"), ("succub", "Demon"),
    ("imp", "Imp"), ("ent", "Ent"), ("talking_tree", "Ent"),
    ("spider", "Spider"), ("rat", "Rat"), ("crow", "Crow"),
    ("dog", "Dog"), ("wolf", "Wolf"), ("bear", "Bear"),
    ("bat", "Bat"), ("wraith", "Wraith"), ("ghost", "Ghost"),
    ("tormented_soul", "TormentedSoul"), ("boss_durk", "Durk"),
    ("durk", "Durk"), ("jireh", "Jireh"), ("silas", "Silas"),
]

WEAPONS = [
    ("greatsword", "Greatsword"), ("twohandsword", "Greatsword"), ("claymore", "Greatsword"),
    ("longsword", "Sword"), ("shortsword", "Sword"), ("scimitar", "Sword"), ("scimtar", "Sword"), ("sword", "Sword"),
    ("greataxe", "Axe"), ("battleaxe", "Axe"), ("pickaxe", "Axe"), ("axe", "Axe"),
    ("warhammer", "Hammer"), ("hammer", "Hammer"),
    ("greatmace", "Mace"), ("mace", "Mace"), ("club", "Club"),
    ("halberd", "Polearm"), ("spear", "Spear"), ("pike", "Spear"),
    ("pitchfork", "Spear"), ("scythe", "Scythe"), ("sickle", "Scythe"), ("sicke", "Scythe"),
    ("dagger", "Dagger"), ("knife", "Dagger"), ("shiv", "Dagger"),
    ("crossbow", "Bow"), ("bow", "Bow"), ("arrow", "Bow"),
    ("stave", "Staff"), ("staff", "Staff"), ("wand", "Staff"),
    ("greatshield", "Shield"), ("shield", "Shield"), ("fist", "Unarmed"), ("torch", "Torch"),
]

ARCHITECTURE = (
    "wall", "floor", "roof", "door", "window", "arch", "pillar", "column",
    "stair", "bridge", "dungeon", "castle", "tower", "house", "gate", "fence",
    "building", "cathedral", "room", "ceiling", "brick", "stonework", "railing",
)
NATURE = (
    "tree", "rock", "grass", "bush", "plant", "leaf", "leaves", "mushroom",
    "flower", "terrain", "cliff", "mountain", "forest", "root", "branch", "vine",
    "water", "waterfall", "river", "ground", "dirt", "mud", "snow",
)
PROPS = (
    "chest", "barrel", "crate", "table", "chair", "candle", "chain", "rope",
    "bell", "book", "bottle", "pot", "anvil", "cart", "wagon", "coffin",
    "tombstone", "altar", "statue", "lever", "elevator", "urn", "flask", "scroll",
    "coin", "key", "lamp", "lantern", "banner", "cloth", "bed", "bench", "bucket",
)
VFX = (
    "vfx", "fx", "particle", "flare", "lightning", "aura", "magic", "spell",
    "smoke", "fire", "flame", "spark", "glow", "trail", "beam", "slash", "splash",
    "blood", "decal", "dissolve", "distortion", "shockwave", "ripple", "noise",
    "gradient", "fresnel", "additive", "impact", "muzzle", "projectile", "bullet",
)


def has(text: str, words: Iterable[str]) -> bool:
    return any(word in text for word in words)


def tokenized(value: str) -> str:
    value = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", value)
    value = re.sub(r"[^A-Za-z0-9]+", "_", value)
    return value.strip("_").casefold()


def mapped_token(text: str, mapping: list[tuple[str, str]]) -> str | None:
    padded = f"_{text.strip('_')}_"
    for token, label in mapping:
        if f"_{token.strip('_')}_" in padded:
            return label
    return None


def clean_name(path: Path) -> tuple[str, int | None]:
    match = PATH_ID_VALUE_RE.search(path.stem)
    path_id = int(match.group(1)) if match else None
    return PATH_ID_SUFFIX_RE.sub("", path.stem).rstrip(" ."), path_id


def meta_guid(path: Path) -> str:
    meta_path = Path(f"{path}.meta")
    if not meta_path.exists():
        return ""
    match = re.search(r"^guid:\s*([0-9a-fA-F]+)\s*$", meta_path.read_text(encoding="utf-8-sig", errors="replace"), re.MULTILINE)
    return match.group(1).lower() if match else ""


def animation_folder(name: str) -> str:
    n = tokenized(name)
    creature = mapped_token(n, CREATURES)
    if creature:
        base = f"Characters/Creatures/{creature}"
    else:
        base = "Characters/Humanoid"
    if has(n, ("walk", "run", "sprint", "idle", "turn", "strafe", "locomotion", "fall", "land", "jump", "roll", "dodge", "crouch", "swim")):
        purpose = "Locomotion"
    elif has(n, ("attack", "combo", "charged", "charge", "release", "guard_counter", "shoot", "cast", "parry", "riposte", "block")):
        weapon = mapped_token(n, WEAPONS) or "General"
        purpose = f"Combat/{weapon}"
    elif has(n, ("hit", "hurt", "damage", "stun", "death", "dead", "knock", "recover", "victim", "getup")):
        purpose = "Reactions"
    elif has(n, ("emote", "clap", "wave", "cheer", "bow_emote", "sit", "sleep", "dance", "taunt")):
        purpose = "Emotes"
    elif has(n, ("climb", "ladder", "door", "pickup", "interact", "open", "close", "drink", "consume", "craft")):
        purpose = "Interactions"
    else:
        purpose = "Actions"
    return f"{base}/{purpose}"


def model_folder(name: str) -> str:
    n = tokenized(name)
    creature = mapped_token(n, CREATURES)
    weapon = mapped_token(n, WEAPONS)
    if n.startswith("generated_convex_submesh"):
        return "Physics/GeneratedColliders"
    if n.startswith("pb_mesh"):
        return "ProBuilder"
    if n in {"cube", "sphere", "cylinder", "capsule", "plane", "quad", "torus", "conus", "icosphere"} or n.startswith(("poly_surface", "p_sphere", "p_cylinder", "p_plane", "part_", "sup_", "cube_", "box_", "cylinder_", "half_cylinder", "half_sphere", "half_torus", "quad_", "quadcv", "shell_", "shellcircle", "cap_lod", "icosahedron", "pyramid", "plataform", "platform")):
        return "Primitives"
    if has(n, ("buff", "ega_crystal", "laser_cone", "rays", "tornado", "twist", "vortex", "knot", "big_roll", "small_roll", "center_plate", "kame_cone", "path4", "plate1", "sup8", "cylindrical_one", "ylindrical_one")):
        return "VFX/Meshes"
    if n.startswith("sm_bld_"):
        return "Environment/Architecture"
    if n.startswith(("sm_prop_", "sm_item_")):
        return "Props"
    if n.startswith("sm_env_"):
        return "Environment/Nature" if has(n, NATURE) else "Environment/Architecture"
    if n.startswith("sm_wep_"):
        return f"Equipment/Weapons/{weapon or 'General'}"
    if n.startswith(("sm_chr_", "md_char_")):
        return "Characters/Humanoid/Armor" if has(n, ("attach", "veil")) else "Characters/Humanoid/Body"
    if n.startswith(("sm_gorechunk", "sm_gore_chunk")):
        return "VFX/Meshes"
    if n.startswith("sm_trap_") or n.startswith("sm_veh_"):
        return "Props"
    if creature:
        return f"Characters/Creatures/{creature}"
    if n in {"spine", "tail_lod0"}:
        return "Characters/Shared/BodyParts"
    if n.startswith("chr_") or has(n, ("character", "body", "head", "arm", "leg", "hand", "hips", "torso", "eyes", "ribcage", "skull")):
        if has(n, ("hair", "beard", "facial")):
            return "Characters/Humanoid/Hair"
        if has(n, ("armor", "helmet", "helm", "hood", "hat", "boot", "glove", "bracer", "cuirass", "cape", "veil", "trousers", "tunic", "accessory")):
            return "Characters/Humanoid/Armor"
        return "Characters/Humanoid/Body"
    if has(n, ("hair", "beard", "facial")):
        return "Characters/Humanoid/Hair"
    if has(n, ("armor", "helmet", "helm", "hood", "hat", "boot", "glove", "bracer", "cuirass", "cape", "veil", "trousers", "tunic")):
        return "Characters/Humanoid/Armor"
    if has(n, ("talisman", "amulet", "ring")):
        return "Equipment/Accessories"
    if weapon == "Shield":
        return "Equipment/Shields"
    if weapon:
        return f"Equipment/Weapons/{weapon}"
    if has(n, ("greathammer", "missionarymace", "meteorite_ugs", "longstaff", "sun_scimtar")):
        if "hammer" in n:
            return "Equipment/Weapons/Hammer"
        if "mace" in n:
            return "Equipment/Weapons/Mace"
        if "staff" in n:
            return "Equipment/Weapons/Staff"
        if "scimtar" in n:
            return "Equipment/Weapons/Sword"
        return "Equipment/Weapons/Greatsword"
    if n.startswith(("weapon_", "l_weapon", "r_weapon")) or "_weapon_" in n:
        return "Equipment/Weapons/General"
    if has(n, VFX):
        return "VFX/Meshes"
    if has(n, ARCHITECTURE):
        return "Environment/Architecture"
    if has(n, NATURE):
        return "Environment/Nature"
    if n == "petal":
        return "Environment/Nature"
    if has(n, ("crystal", "nail", "easel", "punch_ball", "wishing_well", "pulling_wire", "wood_piece", "mill_", "spike")):
        return "Props"
    if has(n, ("dock", "cell")):
        return "Environment/Architecture"
    if has(n, PROPS):
        return "Props"
    if has(n, ("icon", "ui_", "canvas", "button")):
        return "UI"
    return "Misc"


def texture_folder(name: str) -> str:
    n = tokenized(name)
    creature = mapped_token(n, CREATURES)
    weapon = mapped_token(n, WEAPONS)
    if has(n, ("lightmap", "shadowmask", "reflectionprobe", "reflection_probe", "ldr_", "baked")):
        return "Environment/Lighting"
    if has(n, ("ability_", "ability ", "skill_icon", "skill icon", "abilitytree")):
        return "UI/Abilities"
    if has(n, ("buff_icon", "debuff_icon", "status", "modifier_icon", "absorption_icon")):
        return "UI/StatusEffects"
    if has(n, ("keyboard", "playstation", "xbox", "gamepad", "mouse_", "button_", "key_")):
        return "UI/Controls"
    if has(n, ("hair_", "facialhair", "characterSlot".casefold(), "character_slot", "portrait")):
        return "UI/CharacterCustomization"
    if "icon" in n or "slot" in n:
        return "UI/Items"
    if has(n, ("ui_", "hud", "cursor", "crosshair", "frame", "panel", "window", "background")):
        return "UI/General"
    if creature:
        return f"Characters/Creatures/{creature}"
    if has(n, ("chr_", "character", "skin", "body", "face", "hair", "beard")):
        return "Characters/Humanoid"
    if has(n, ("armor", "cuirass", "helmet", "helm", "hood", "boot", "glove", "robe", "cape")):
        return "Equipment/Armor"
    if weapon:
        return f"Equipment/Weapons/{weapon}"
    if has(n, VFX):
        return "VFX"
    if has(n, ARCHITECTURE):
        return "Environment/Architecture"
    if has(n, NATURE):
        return "Environment/Nature"
    if has(n, ("sky", "cloud", "moon", "sun", "star")):
        return "Environment/Sky"
    if has(n, PROPS):
        return "Props"
    return "General"


def audio_folder(name: str) -> str:
    n = tokenized(name)
    creature = mapped_token(n, CREATURES)
    if has(n, ("music", "theme", "phase1", "phase2", "no-loop", "no_loop")) or n in {"nephilite", "respite", "disturbance_intro", "disturbance_loop"}:
        return "Music"
    if has(n, ("ambiance", "ambience", "ambient", "wind", "waterfall", "cave_", "firecamp", "rain")):
        return "Ambience"
    if creature:
        return f"Creatures/{creature}"
    if has(n, ("menu", "confirm", "cancel", "inventory", "level_up", "level up", "common_drop", "rare_drop", "ui_")):
        return "UI"
    if has(n, ("cast", "spell", "magic", "buff", "berserk", "blizzard", "lightning", "fireball", "heal", "smite", "corpse", "summon", "skill")):
        return "SFX/Abilities"
    if has(n, ("voice", "grunt", "yell", "scream", "cough", "laugh", "dialog", "vocal")):
        return "SFX/Characters/Voice"
    if has(n, ("footstep", "foot_step", "walk", "run", "climb", "ladder", "land", "jump")):
        return "SFX/Characters/Movement"
    if has(n, ("attack", "whoosh", "impact", "hit", "block", "parry", "riposte", "weapon", "bow_", "sword", "axe", "hammer")):
        return "SFX/Combat"
    if has(n, ("door", "chain", "coin", "chest", "anvil", "bell", "break", "debris", "rock", "wood", "metal")):
        return "SFX/Environment"
    return "SFX/General"


def material_folder(name: str) -> str:
    n = tokenized(name)
    creature = mapped_token(n, CREATURES)
    weapon = mapped_token(n, WEAPONS)
    if creature:
        return f"Characters/Creatures/{creature}/RecoveredMetadata"
    if has(n, VFX):
        return "VFX/RecoveredMetadata"
    if has(n, ("character", "skin", "body", "face", "hair", "beard", "lips")):
        return "Characters/Humanoid/RecoveredMetadata"
    if has(n, ("armor", "cuirass", "helmet", "helm", "hood", "boot", "glove", "robe", "cape")):
        return "Equipment/Armor/RecoveredMetadata"
    if weapon:
        return f"Equipment/Weapons/{weapon}/RecoveredMetadata"
    if has(n, ARCHITECTURE) or has(n, NATURE) or has(n, ("sky", "terrain", "water")):
        return "Environment/RecoveredMetadata"
    if has(n, PROPS):
        return "Props/RecoveredMetadata"
    if has(n, ("ui", "sprite", "font")):
        return "UI/RecoveredMetadata"
    return "General/RecoveredMetadata"


def semantic_folder(asset_type: str, name: str) -> str:
    if asset_type == "AnimationClip":
        return f"Animations/{animation_folder(name)}"
    if asset_type == "Mesh":
        return f"Models/{model_folder(name)}"
    if asset_type in {"Texture2D", "StreamingAsset"}:
        return f"Textures/{texture_folder(name)}"
    if asset_type == "AudioClip":
        return f"Audio/{audio_folder(name)}"
    if asset_type == "Material":
        return f"Materials/{material_folder(name)}"
    if asset_type == "Font":
        return "Fonts"
    return "Misc"


def write_csv(path: Path, rows: list[dict[str, Any]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def build_plan(project_root: Path, manifest_path: Path) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    with manifest_path.open("r", encoding="utf-8-sig", newline="") as handle:
        source_rows = list(csv.DictReader(handle))
    source_paths = {Path(row["destination"]).resolve() for row in source_rows}
    reserved = {
        path.resolve().as_posix().casefold()
        for path in (project_root / "Assets" / "Art").rglob("*")
        if path.is_file() and path.suffix.casefold() != ".meta" and path.resolve() not in source_paths
    }
    planned: list[dict[str, Any]] = []
    base_counts: Counter[str] = Counter()
    for row in source_rows:
        source = Path(row["destination"]).resolve()
        if not source.exists():
            raise FileNotFoundError(source)
        cleaned, filename_path_id = clean_name(source)
        path_id = row.get("path_id") or filename_path_id or ""
        folder = semantic_folder(row["asset_type"], cleaned)
        base_asset_path = (Path("Assets") / "Art" / folder / f"{cleaned}{source.suffix}").as_posix()
        base_counts[base_asset_path.casefold()] += 1
        planned.append(
            {
                **row,
                "original_destination": str(source),
                "source_asset_path": source.relative_to(project_root).as_posix(),
                "source_guid": meta_guid(source),
                "clean_name": cleaned,
                "path_id": path_id,
                "semantic_folder": folder,
                "base_destination_asset_path": base_asset_path,
            }
        )

    assigned: set[str] = set(reserved)
    collision_rows = 0
    for row in sorted(planned, key=lambda item: (item["base_destination_asset_path"].casefold(), int(item["path_id"] or 0), item["source_asset_path"].casefold())):
        base_path = Path(row["base_destination_asset_path"])
        candidate = base_path
        variant = 0
        while candidate.as_posix().casefold() in assigned:
            variant += 1
            candidate = base_path.with_name(f"{base_path.stem}_Variant_{variant:02d}{base_path.suffix}")
        if variant:
            collision_rows += 1
        assigned.add(candidate.as_posix().casefold())
        row["destination_asset_path"] = candidate.as_posix()
        row["destination"] = str((project_root / candidate).resolve())
        row["variant_index"] = variant
        row["base_collision_count"] = base_counts[row["base_destination_asset_path"].casefold()]

    planned.sort(key=lambda item: item["destination_asset_path"].casefold())
    category_counts = Counter(row["semantic_folder"] for row in planned)
    old_markers = ("/Nephilite/", "sharedassets", "level0_export", "resources.assets_export")
    summary = {
        "total_moves": len(planned),
        "total_bytes": sum(int(row["bytes"]) for row in planned),
        "variant_named_files": collision_rows,
        "colliding_base_paths": sum(1 for count in base_counts.values() if count > 1),
        "max_base_collision": max(base_counts.values(), default=0),
        "pathid_remaining_in_destinations": sum("__pathid_" in row["destination_asset_path"].casefold() for row in planned),
        "nephilite_folder_remaining": sum("/nephilite/" in row["destination_asset_path"].casefold() for row in planned),
        "source_group_folder_remaining": sum(any(marker.casefold() in row["destination_asset_path"].casefold() for marker in old_markers[1:]) for row in planned),
        "max_destination_length": max((len(row["destination_asset_path"]) for row in planned), default=0),
        "source_meta_guid_missing": sum(not row["source_guid"] for row in planned),
        "category_counts": dict(category_counts.most_common()),
    }
    return planned, summary


def finalize(project_root: Path, report_root: Path, plan: list[dict[str, Any]]) -> dict[str, Any]:
    missing = [row for row in plan if not (project_root / row["destination_asset_path"]).exists()]
    old_remaining = [row for row in plan if (project_root / row["source_asset_path"]).exists()]
    size_mismatches = [row for row in plan if (project_root / row["destination_asset_path"]).exists() and (project_root / row["destination_asset_path"]).stat().st_size != int(row["bytes"])]
    guid_mismatches = [
        row for row in plan
        if (project_root / row["destination_asset_path"]).exists()
        and (not row.get("source_guid") or meta_guid(project_root / row["destination_asset_path"]) != row.get("source_guid"))
    ]
    if missing or old_remaining or size_mismatches or guid_mismatches:
        raise RuntimeError(
            f"Cannot finalize: missing={len(missing)}, old_remaining={len(old_remaining)}, "
            f"size_mismatches={len(size_mismatches)}, guid_mismatches={len(guid_mismatches)}"
        )
    fields = list(plan[0].keys()) if plan else []
    write_csv(report_root / "semantic_asset_manifest.csv", plan, fields)
    canonical_manifest = report_root / "asset_manifest.csv"
    recovery_manifest = report_root / "asset_manifest_recovery_layout.csv"
    if canonical_manifest.exists() and not recovery_manifest.exists():
        shutil.copyfile(canonical_manifest, recovery_manifest)
    write_csv(canonical_manifest, plan, fields)
    destination_by_source = {row["original_destination"].casefold(): row["destination"] for row in plan}
    for filename in ("asset_reference_links.csv", "material_texture_links.csv"):
        path = report_root / filename
        if not path.exists():
            continue
        with path.open("r", encoding="utf-8-sig", newline="") as handle:
            links = list(csv.DictReader(handle))
            link_fields = list(links[0].keys()) if links else []
        for link in links:
            old = link.get("resolved_destination", "").casefold()
            if old in destination_by_source:
                link["resolved_destination"] = destination_by_source[old]
        write_csv(path, links, link_fields)
    return {
        "finalized": True,
        "manifest_rows": len(plan),
        "missing": 0,
        "old_remaining": 0,
        "size_mismatches": 0,
        "guid_mismatches": 0,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--finalize", action="store_true")
    args = parser.parse_args()
    project_root = args.project_root.resolve()
    report_root = project_root / "Docs" / "ArtRecovery" / "Nephilite"
    manifest_path = (args.manifest or report_root / "asset_manifest.csv").resolve()
    if args.finalize:
        plan_path = report_root / "semantic_move_plan.csv"
        if not plan_path.exists():
            raise FileNotFoundError(plan_path)
        with plan_path.open("r", encoding="utf-8-sig", newline="") as handle:
            plan = list(csv.DictReader(handle))
        summary_path = report_root / "semantic_move_summary.json"
        summary = json.loads(summary_path.read_text(encoding="utf-8")) if summary_path.exists() else {"total_moves": len(plan)}
        summary["finalization"] = finalize(project_root, report_root, plan)
        summary_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps(summary, ensure_ascii=False, indent=2))
        return 0
    plan, summary = build_plan(project_root, manifest_path)
    fields = list(plan[0].keys()) if plan else []
    write_csv(report_root / "semantic_move_plan.csv", plan, fields)
    move_items = [
        {"sourcePath": row["source_asset_path"], "destinationPath": row["destination_asset_path"]}
        for row in plan
    ]
    (report_root / "semantic_move_plan.json").write_text(
        json.dumps({"items": move_items, "summary": summary}, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    (report_root / "semantic_move_summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
