#!/usr/bin/env python3
"""Recover rigged Nephilite models and bind the recovered AnimationClips.

The AssetStudio export contains useful standalone .anim files but its FBX export
lost skinning data for this Unity 6000 game.  AssetRipper can deserialize the
original SkinnedMeshRenderer, Avatar, AnimatorController and Mesh objects.  This
tool joins the two exports without importing the recovered gameplay scripts.

Run without --apply for a read-only audit.  Run with --apply only after reading
the generated report in Docs/ArtRecovery/Nephilite/AnimationBindings.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import shutil
import sys
from collections import Counter, defaultdict, deque
from dataclasses import dataclass
from pathlib import Path


PROJECT_ROOT = Path(r"C:\C\Game\Unity\Elden")
SOURCE_ASSETS = Path(
    r"F:\MyProject\Game\RE-Assets\Nephilite-Demo"
    r"\AssetRipper-Level0-1.3.14\ExportedProject\Assets"
)
CURRENT_ANIMATIONS = PROJECT_ROOT / "Assets/Art/Animations"
CURRENT_MATERIALS = PROJECT_ROOT / "Assets/Art/Materials"
OUTPUT_ROOT = PROJECT_ROOT / "Assets/Art"
REPORT_ROOT = PROJECT_ROOT / "Docs/ArtRecovery/Nephilite/AnimationBindings"

CONTROLLER_OUTPUT = OUTPUT_ROOT / "Animations/Controllers"
MODEL_OUTPUT = OUTPUT_ROOT / "Models/Rigged"
MESH_OUTPUT = MODEL_OUTPUT / "Shared/Meshes"
AVATAR_OUTPUT = MODEL_OUTPUT / "Shared/Avatars"

GUID_RE = re.compile(r"\bguid:\s*([0-9a-fA-F]{32})\b")
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$", re.MULTILINE)
FILE_ID_RE = re.compile(r"\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*\d+)?\}")

# RectTransform (224) must remain whenever a stripped UI GameObject survives;
# Unity requires every GameObject to own either Transform or RectTransform.
KEEP_PREFAB_CLASS_IDS = {1, 4, 23, 33, 95, 137, 224}
CONTROLLER_CLASS_IDS = {91, 206, 221, 1101, 1102, 1107}


@dataclass(frozen=True)
class YamlDoc:
    class_id: int
    file_id: int
    text: str


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def meta_guid(meta_path: Path) -> str:
    match = GUID_RE.search(read_text(meta_path))
    if not match:
        raise ValueError(f"No GUID in {meta_path}")
    return match.group(1).lower()


def unity_asset_path(path: Path) -> str:
    return path.relative_to(PROJECT_ROOT).as_posix()


def split_unity_yaml(text: str) -> tuple[str, list[YamlDoc]]:
    matches = list(DOC_RE.finditer(text))
    if not matches:
        return text, []
    header = text[: matches[0].start()]
    docs: list[YamlDoc] = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        docs.append(YamlDoc(int(match.group(1)), int(match.group(2)), text[match.start():end]))
    return header, docs


def field_file_id(text: str, field_name: str) -> int:
    match = re.search(rf"^\s*{re.escape(field_name)}:\s*\{{fileID:\s*(-?\d+)", text, re.MULTILINE)
    return int(match.group(1)) if match else 0


def field_guid(text: str, field_name: str) -> str | None:
    match = re.search(
        rf"^\s*{re.escape(field_name)}:\s*\{{fileID:\s*-?\d+,\s*guid:\s*([0-9a-fA-F]{{32}})",
        text,
        re.MULTILINE,
    )
    return match.group(1).lower() if match else None


def object_name(text: str) -> str:
    match = re.search(r"^\s*m_Name:\s*(.*?)\s*$", text, re.MULTILINE)
    return match.group(1).strip() if match else "Unnamed"


def parse_component_ids(game_object_text: str) -> list[int]:
    block = re.search(r"^\s*m_Component:\s*\n(?P<body>(?:\s*- component:.*\n)*)", game_object_text, re.MULTILINE)
    if not block:
        return []
    return [int(value) for value in re.findall(r"fileID:\s*(-?\d+)", block.group("body"))]


def parse_children(transform_text: str) -> list[int]:
    block = re.search(r"^\s*m_Children:\s*\n(?P<body>(?:\s*- \{fileID:.*\n)*)", transform_text, re.MULTILINE)
    if not block:
        return []
    return [int(value) for value in re.findall(r"fileID:\s*(-?\d+)", block.group("body"))]


def collect_source_assets() -> tuple[dict[str, Path], dict[Path, str]]:
    by_guid: dict[str, Path] = {}
    by_path: dict[Path, str] = {}
    relevant_directories = (
        "AnimationClip", "AnimatorController", "AnimatorOverrideController",
        "Avatar", "GameObject", "Material", "Mesh",
    )
    for directory_name in relevant_directories:
        directory = SOURCE_ASSETS / directory_name
        for meta in directory.glob("*.meta"):
            asset = Path(str(meta)[:-5])
            if not asset.is_file():
                continue
            guid = meta_guid(meta)
            by_guid[guid] = asset
            by_path[asset] = guid
    return by_guid, by_path


def collect_current_assets(root: Path, suffix: str) -> tuple[dict[str, Path], dict[str, Path]]:
    by_stem: dict[str, Path] = {}
    by_guid: dict[str, Path] = {}
    duplicates: defaultdict[str, list[Path]] = defaultdict(list)
    for asset in root.rglob(f"*{suffix}"):
        meta = Path(str(asset) + ".meta")
        if not meta.is_file():
            continue
        key = asset.stem.casefold()
        duplicates[key].append(asset)
        by_guid[meta_guid(meta)] = asset
    ambiguous = {key: values for key, values in duplicates.items() if len(values) > 1}
    if ambiguous:
        details = "\n".join(f"  {key}: {values}" for key, values in sorted(ambiguous.items()))
        raise RuntimeError(f"Ambiguous current {suffix} names:\n{details}")
    by_stem.update({key: values[0] for key, values in duplicates.items()})
    return by_stem, by_guid


def map_collision_name(source_stem: str, current_by_stem: dict[str, Path]) -> Path | None:
    exact = current_by_stem.get(source_stem.casefold())
    if exact:
        return exact
    match = re.match(r"^(.*)_(\d+)$", source_stem)
    if not match:
        return None
    candidate = f"{match.group(1)}_Variant_{int(match.group(2)) + 1:02d}"
    return current_by_stem.get(candidate.casefold())


def replace_guids(text: str, replacements: dict[str, str]) -> str:
    def replace(match: re.Match[str]) -> str:
        old = match.group(1).lower()
        return match.group(0).replace(match.group(1), replacements.get(old, old))
    return GUID_RE.sub(replace, text)


def remove_deleted_component_refs(text: str, deleted_ids: set[int]) -> str:
    lines: list[str] = []
    component_line = re.compile(r"^\s*- component:\s*\{fileID:\s*(-?\d+)\}\s*$")
    bare_ref_line = re.compile(r"^\s*-\s*\{fileID:\s*(-?\d+)\}\s*$")
    local_ref = re.compile(r"\{fileID:\s*(-?\d+)\}")

    def clear_deleted(match: re.Match[str]) -> str:
        return "{fileID: 0}" if int(match.group(1)) in deleted_ids else match.group(0)

    for line in text.splitlines(keepends=True):
        match = component_line.match(line.rstrip("\r\n")) or bare_ref_line.match(line.rstrip("\r\n"))
        if match and int(match.group(1)) in deleted_ids:
            continue
        lines.append(local_ref.sub(clear_deleted, line))
    return "".join(lines)


def clean_prefab_yaml(text: str, guid_replacements: dict[str, str]) -> tuple[str, Counter[int]]:
    header, docs = split_unity_yaml(text)
    kept = [doc for doc in docs if doc.class_id in KEEP_PREFAB_CLASS_IDS]
    kept_ids = {doc.file_id for doc in kept}
    deleted_ids = {doc.file_id for doc in docs if doc.file_id not in kept_ids}
    class_counts = Counter(doc.class_id for doc in kept)
    cleaned: list[str] = []
    for doc in kept:
        value = remove_deleted_component_refs(doc.text, deleted_ids)
        value = replace_guids(value, guid_replacements)
        cleaned.append(value)
    return header + "".join(cleaned), class_counts


def clean_controller_yaml(text: str, guid_replacements: dict[str, str]) -> tuple[str, Counter[int]]:
    header, docs = split_unity_yaml(text)
    kept = [doc for doc in docs if doc.class_id in CONTROLLER_CLASS_IDS]
    kept_ids = {doc.file_id for doc in kept}
    deleted_ids = {doc.file_id for doc in docs if doc.file_id not in kept_ids}
    cleaned = [replace_guids(remove_deleted_component_refs(doc.text, deleted_ids), guid_replacements) for doc in kept]
    return header + "".join(cleaned), Counter(doc.class_id for doc in kept)


def categorize_model(name: str, controller_names: set[str]) -> tuple[str, str]:
    joined = " ".join([name, *sorted(controller_names)]).casefold()
    prop_words = (
        "bow", "crossbow", "chest", "door", "elevator", "mechanism", "pit fall",
        "wall slab", "swinging blade", "wall spikes", "pulling_wire",
    )
    if any(word in joined for word in prop_words):
        return "Props", name
    creature_rules = (
        ("Dog", ("dog",)),
        ("Werewolf", ("werewolf", "werwolf")),
        ("Golem", ("golem",)),
        ("Mimic", ("mimic",)),
        ("Crow", ("crow",)),
        ("Ent", ("ent",)),
        ("Imp", ("imp",)),
        ("Demon", ("jireh", "demon")),
        ("Undead", ("undead", "zombie", "skeleton")),
        ("Spectral", ("phantom", "ghost", "shadow person")),
    )
    for category, words in creature_rules:
        if any(word in joined for word in words):
            return f"Characters/Creatures/{category}", name
    return "Characters/Humanoid", name


def scene_animator_models(scene: Path, source_by_guid: dict[str, Path]) -> list[dict[str, object]]:
    header, docs = split_unity_yaml(read_text(scene))
    del header
    by_id = {doc.file_id: doc for doc in docs}
    game_objects = {doc.file_id: doc for doc in docs if doc.class_id == 1}
    transforms = {doc.file_id: doc for doc in docs if doc.class_id == 4}
    component_owner: dict[int, int] = {}
    for game_object_id, doc in game_objects.items():
        for component_id in parse_component_ids(doc.text):
            component_owner[component_id] = game_object_id
    result: list[dict[str, object]] = []
    for animator in (doc for doc in docs if doc.class_id == 95 and field_guid(doc.text, "m_Controller")):
        game_object_id = field_file_id(animator.text, "m_GameObject") or component_owner.get(animator.file_id, 0)
        game_object = game_objects.get(game_object_id)
        if not game_object:
            continue
        transform_id = next((cid for cid in parse_component_ids(game_object.text) if cid in transforms), 0)
        controller_guid = field_guid(animator.text, "m_Controller")
        avatar_guid = field_guid(animator.text, "m_Avatar")
        result.append(
            {
                "name": object_name(game_object.text),
                "animator_file_id": animator.file_id,
                "game_object_id": game_object_id,
                "transform_id": transform_id,
                "controller_guid": controller_guid,
                "controller_path": str(source_by_guid.get(controller_guid, "")),
                "avatar_guid": avatar_guid or "",
                "avatar_path": str(source_by_guid.get(avatar_guid or "", "")),
            }
        )
    return result


def extract_scene_model_yaml(scene_text: str, animator_file_id: int, guid_replacements: dict[str, str]) -> tuple[str, Counter[int]]:
    header, docs = split_unity_yaml(scene_text)
    by_id = {doc.file_id: doc for doc in docs}
    game_objects = {doc.file_id: doc for doc in docs if doc.class_id == 1}
    transforms = {doc.file_id: doc for doc in docs if doc.class_id == 4}
    animator = by_id[animator_file_id]
    root_go_id = field_file_id(animator.text, "m_GameObject")
    root_go = game_objects[root_go_id]
    root_transform_id = next(cid for cid in parse_component_ids(root_go.text) if cid in transforms)

    transform_ids: set[int] = set()
    queue: deque[int] = deque([root_transform_id])
    while queue:
        transform_id = queue.popleft()
        if transform_id in transform_ids or transform_id not in transforms:
            continue
        transform_ids.add(transform_id)
        queue.extend(parse_children(transforms[transform_id].text))

    go_ids = {field_file_id(transforms[tid].text, "m_GameObject") for tid in transform_ids}
    component_ids: set[int] = set()
    for go_id in go_ids:
        component_ids.update(parse_component_ids(game_objects[go_id].text))
    selected_ids = go_ids | transform_ids | component_ids
    selected_docs = [doc for doc in docs if doc.file_id in selected_ids]
    kept = [doc for doc in selected_docs if doc.class_id in KEEP_PREFAB_CLASS_IDS]
    kept_ids = {doc.file_id for doc in kept}
    deleted_ids = selected_ids - kept_ids
    output_docs: list[str] = []
    for doc in kept:
        value = remove_deleted_component_refs(doc.text, deleted_ids)
        value = re.sub(r"m_PrefabInstance:\s*\{fileID:\s*-?\d+\}", "m_PrefabInstance: {fileID: 0}", value)
        if doc.file_id == root_transform_id:
            value = re.sub(r"m_Father:\s*\{fileID:\s*-?\d+\}", "m_Father: {fileID: 0}", value)
        value = replace_guids(value, guid_replacements)
        output_docs.append(value)
    return header + "".join(output_docs), Counter(doc.class_id for doc in kept)


def external_guids(text: str) -> set[str]:
    return {match.group(1).lower() for match in GUID_RE.finditer(text)}


def ensure_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


def copy_asset_preserving_meta(source: Path, destination: Path, apply: bool) -> None:
    if not apply:
        return
    ensure_parent(destination)
    shutil.copy2(source, destination)
    shutil.copy2(Path(str(source) + ".meta"), Path(str(destination) + ".meta"))


def write_text(path: Path, value: str, apply: bool) -> None:
    if not apply:
        return
    ensure_parent(path)
    path.write_text(value, encoding="utf-8", newline="\n")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="Write recovered assets into the Unity project")
    parser.add_argument(
        "--skip-animation-rebuild",
        action="store_true",
        help="When applying, leave already rebuilt AnimationClip YAML untouched",
    )
    args = parser.parse_args()

    if not SOURCE_ASSETS.is_dir():
        raise FileNotFoundError(SOURCE_ASSETS)
    if not CURRENT_ANIMATIONS.is_dir():
        raise FileNotFoundError(CURRENT_ANIMATIONS)

    print("[1/8] Indexing recovered source GUIDs...", flush=True)
    source_by_guid, source_guid_by_path = collect_source_assets()
    print("[2/8] Indexing current animations and materials...", flush=True)
    current_anims, _ = collect_current_assets(CURRENT_ANIMATIONS, ".anim")
    current_materials, _ = collect_current_assets(CURRENT_MATERIALS, ".mat")

    replacements: dict[str, str] = {}
    source_clip_target: dict[str, Path] = {}
    source_material_target: dict[str, Path] = {}
    unmapped_clips: list[str] = []
    unmapped_materials: list[str] = []

    for source in sorted((SOURCE_ASSETS / "AnimationClip").glob("*.anim")):
        source_guid = source_guid_by_path[source]
        target = map_collision_name(source.stem, current_anims)
        if not target:
            unmapped_clips.append(source.name)
            continue
        target_guid = meta_guid(Path(str(target) + ".meta"))
        replacements[source_guid] = target_guid
        source_clip_target[source_guid] = target

    for source in sorted((SOURCE_ASSETS / "Material").glob("*.mat")):
        source_guid = source_guid_by_path[source]
        target = map_collision_name(source.stem, current_materials)
        if not target:
            unmapped_materials.append(source.name)
            continue
        target_guid = meta_guid(Path(str(target) + ".meta"))
        replacements[source_guid] = target_guid
        source_material_target[source_guid] = target

    print("[3/8] Mapping recovered clip/material GUIDs...", flush=True)
    controller_sources = sorted((SOURCE_ASSETS / "AnimatorController").glob("*.controller"))
    override_sources = sorted((SOURCE_ASSETS / "AnimatorOverrideController").glob("*.overrideController"))
    if not override_sources:
        override_sources = sorted((SOURCE_ASSETS / "AnimatorOverrideController").glob("*"))
        override_sources = [path for path in override_sources if path.is_file() and not path.name.endswith(".meta")]

    controller_by_guid = {
        source_guid_by_path[path]: path for path in [*controller_sources, *override_sources]
    }
    controller_guid_by_name = {path.stem.casefold(): guid for guid, path in controller_by_guid.items()}
    override_base_controller: dict[str, str] = {}
    for source in override_sources:
        override_guid = source_guid_by_path[source]
        base_guid = field_guid(read_text(source), "m_Controller")
        if base_guid and base_guid in controller_by_guid:
            override_base_controller[override_guid] = base_guid
    controller_target: dict[str, Path] = {}
    for guid, source in controller_by_guid.items():
        group = "Overrides" if source.parent.name == "AnimatorOverrideController" else "Runtime"
        target = CONTROLLER_OUTPUT / group / source.name
        controller_target[guid] = target

    print("[4/8] Cleaning controllers and discovering model prefabs...", flush=True)
    prefab_sources = sorted((SOURCE_ASSETS / "GameObject").glob("*.prefab"))
    prefab_rows: list[dict[str, object]] = []
    selected_prefabs: list[tuple[Path, set[str]]] = []
    for prefab in prefab_sources:
        text = read_text(prefab)
        controller_guids = {
            guid for guid in external_guids(text)
            if guid in controller_by_guid
        }
        if not controller_guids:
            continue
        selected_prefabs.append((prefab, controller_guids))

    scene = SOURCE_ASSETS / "Scenes/level0.unity"
    scene_models = scene_animator_models(scene, source_by_guid)

    all_cleaned_assets: list[tuple[str, Path, str, Counter[int]]] = []
    required_guids: set[str] = set()
    controller_clip_guids: dict[str, set[str]] = {}

    for source in [*controller_sources, *override_sources]:
        source_guid = source_guid_by_path[source]
        raw = read_text(source)
        clip_guids = external_guids(raw) & set(source_clip_target)
        controller_clip_guids[source_guid] = clip_guids
        cleaned, counts = clean_controller_yaml(raw, replacements)
        target = controller_target[source_guid]
        all_cleaned_assets.append(("controller", target, cleaned, counts))
        required_guids.update(external_guids(cleaned))

    print("[5/8] Cleaning 3D model prefabs...", flush=True)
    prefab_controller_usage: defaultdict[str, list[Path]] = defaultdict(list)
    model_target_by_stem: dict[str, Path] = {}
    for source, controller_guids in selected_prefabs:
        controller_names = {controller_by_guid[guid].stem for guid in controller_guids}
        category, _ = categorize_model(source.stem, controller_names)
        target = MODEL_OUTPUT / category / source.stem / f"{source.stem}.prefab"
        cleaned, counts = clean_prefab_yaml(read_text(source), replacements)
        all_cleaned_assets.append(("prefab", target, cleaned, counts))
        model_target_by_stem[source.stem.casefold()] = target
        required_guids.update(external_guids(cleaned))
        for controller_guid in controller_guids:
            prefab_controller_usage[controller_guid].append(target)
        prefab_rows.append(
            {
                "model": source.stem,
                "source_kind": "Prefab",
                "source": str(source),
                "destination": unity_asset_path(target),
                "controllers": ";".join(sorted(controller_names)),
                "class_counts": json.dumps(dict(sorted(counts.items()))),
            }
        )

    # Scene-only rigged characters (notably both werewolf variants) are promoted
    # to clean art Prefabs when their Animator/controller pairing isn't already
    # represented by a recovered Prefab with the same name.
    existing_model_names = {source.stem.casefold() for source, _ in selected_prefabs}
    scene_text = read_text(scene)
    scene_name_counts = Counter(str(row["name"]).casefold() for row in scene_models)
    extracted_scene_keys: set[tuple[str, str]] = set()
    for row in scene_models:
        name = str(row["name"])
        controller_guid = str(row["controller_guid"])
        if controller_guid not in controller_by_guid or not int(row["transform_id"]):
            continue
        key = (name.casefold(), controller_guid)
        if name.casefold() in existing_model_names or key in extracted_scene_keys or prefab_controller_usage.get(controller_guid):
            continue
        extracted_scene_keys.add(key)
        controller_name = controller_by_guid[controller_guid].stem
        category, _ = categorize_model(name, {controller_name})
        safe_name = re.sub(r"[<>:\"/\\|?*]", "_", name).strip(" .") or f"SceneModel_{row['animator_file_id']}"
        if scene_name_counts[name.casefold()] > 1:
            safe_name += f"_{row['animator_file_id']}"
        target = MODEL_OUTPUT / category / safe_name / f"{safe_name}.prefab"
        cleaned, counts = extract_scene_model_yaml(scene_text, int(row["animator_file_id"]), replacements)
        all_cleaned_assets.append(("scene-prefab", target, cleaned, counts))
        required_guids.update(external_guids(cleaned))
        prefab_controller_usage[controller_guid].append(target)
        prefab_rows.append(
            {
                "model": name,
                "source_kind": "Scene Animator",
                "source": f"{scene}#Animator:{row['animator_file_id']}",
                "destination": unity_asset_path(target),
                "controllers": controller_name,
                "class_counts": json.dumps(dict(sorted(counts.items()))),
            }
        )

    # Override controllers inherit the model pairing of their base controller.
    # A few controllers were assigned dynamically by game code, so their exact
    # compatible visual model is restored explicitly from the original prefab
    # contents and character family.
    for override_guid, base_guid in override_base_controller.items():
        prefab_controller_usage[override_guid].extend(prefab_controller_usage.get(base_guid, []))
    dynamic_bindings = {
        "werewolf": "player",
        "undead elevator attendant": "undead_villager_07_lantern",
    }
    for controller_name, model_stem in dynamic_bindings.items():
        controller_guid = controller_guid_by_name.get(controller_name)
        model_target = model_target_by_stem.get(model_stem)
        if controller_guid and model_target and model_target not in prefab_controller_usage[controller_guid]:
            prefab_controller_usage[controller_guid].append(model_target)

    # Recovered meshes and avatars retain AssetRipper GUIDs, so all renderer and
    # Animator references stay valid after relocation.
    print("[6/8] Resolving Mesh and Avatar dependencies...", flush=True)
    copied_dependencies: list[tuple[str, Path, Path, str]] = []
    unresolved: list[dict[str, str]] = []
    queue = deque(sorted(required_guids))
    visited_guids: set[str] = set()
    already_resolved_guids = set(replacements.values()) | set(controller_by_guid)
    unity_builtin_guids = {
        "0000000000000000e000000000000000",
        "0000000000000000f000000000000000",
    }
    while queue:
        guid = queue.popleft()
        if guid in visited_guids or guid in replacements or guid in already_resolved_guids or guid in unity_builtin_guids:
            continue
        visited_guids.add(guid)
        source = source_by_guid.get(guid)
        if not source:
            unresolved.append({"guid": guid, "source": "", "reason": "GUID not found in AssetRipper export"})
            continue
        if source.parent.name == "Mesh":
            target = MESH_OUTPUT / source.name
            kind = "Mesh"
        elif source.parent.name == "Avatar":
            target = AVATAR_OUTPUT / source.name
            kind = "Avatar"
        elif source.parent.name in {"AnimatorController", "AnimatorOverrideController"}:
            target = controller_target.get(guid)
            if not target:
                unresolved.append({"guid": guid, "source": str(source), "reason": "Controller has no output mapping"})
                continue
            kind = source.parent.name
        elif source.parent.name == "AnimationClip":
            unresolved.append({"guid": guid, "source": str(source), "reason": "AnimationClip was not mapped"})
            continue
        elif source.parent.name == "Material":
            unresolved.append({"guid": guid, "source": str(source), "reason": "Material was not mapped"})
            continue
        else:
            unresolved.append({"guid": guid, "source": str(source), "reason": f"Unsupported kept dependency type {source.parent.name}"})
            continue
        copied_dependencies.append((kind, source, target, guid))

    # Binding rows record the original controller/model relationship while
    # pointing at the user's already cleaned AnimationClip assets.
    binding_rows: list[dict[str, str]] = []
    referenced_clip_guids: set[str] = set()
    for controller_guid, clip_guids in sorted(controller_clip_guids.items(), key=lambda item: controller_by_guid[item[0]].name.casefold()):
        controller = controller_by_guid[controller_guid]
        models = prefab_controller_usage.get(controller_guid, [])
        for clip_guid in sorted(clip_guids, key=lambda value: unity_asset_path(source_clip_target[value]).casefold()):
            referenced_clip_guids.add(clip_guid)
            target_clip = source_clip_target[clip_guid]
            if models:
                for model in sorted(models):
                    binding_rows.append(
                        {
                            "animation": unity_asset_path(target_clip),
                            "controller": unity_asset_path(controller_target[controller_guid]),
                            "model": unity_asset_path(model),
                            "binding_basis": "Original Animator controller reference",
                        }
                    )
            else:
                binding_rows.append(
                    {
                        "animation": unity_asset_path(target_clip),
                        "controller": unity_asset_path(controller_target[controller_guid]),
                        "model": "",
                        "binding_basis": "Controller recovered; no original model prefab found",
                    }
                )

    # Six clips live in scene-specific sharedassets files outside level0's
    # dependency graph.  They animate cameras or static environment props, not
    # character rigs.  Associate them with the recovered semantic model assets
    # identified from their original sharedassets group.
    supplemental_models: dict[str, list[str]] = {
        "CINECAM_01": [""],
        "Pressure_Plate_Push_Down_01": [
            "Assets/Art/Models/Environment/Architecture/SM_Env_Tiles_Round_01.obj",
        ],
        "Pressure_Plate_Release_01": [
            "Assets/Art/Models/Environment/Architecture/SM_Env_Tiles_Round_01.obj",
        ],
        "Wind_Animation": [
            "Assets/Art/Models/Environment/Architecture/SM_Bld_Camp_Tent_Cover_Side_Cloth_04.obj",
            "Assets/Art/Models/Environment/Architecture/SM_Bld_Camp_Tent_Cover_Top_Cloth_04.obj",
        ],
        "Windmill_Spin_01": [
            "Assets/Art/Models/Props/Mill_LOD0.obj",
            "Assets/Art/Models/Props/Mill_Blade_LOD0.obj",
        ],
        "Whispering_Woods_Shed_Open_01": [
            "Assets/Art/Models/Props/SM_Prop_Shed_01.obj",
            "Assets/Art/Models/Props/SM_Prop_Shed_01_Door_01.obj",
        ],
    }
    supplemental_binding_count = 0
    for clip_name, models in supplemental_models.items():
        target_clip = current_anims.get(clip_name.casefold())
        if not target_clip:
            continue
        for model in models:
            binding_rows.append(
                {
                    "animation": unity_asset_path(target_clip),
                    "controller": "",
                    "model": model,
                    "binding_basis": (
                        "Camera animation; no 3D art model"
                        if not model
                        else "Original sharedassets group and animation curve hierarchy"
                    ),
                }
            )
            supplemental_binding_count += 1

    unreferenced_source_clips = [
        source for guid, source in source_clip_target.items() if guid not in referenced_clip_guids
    ]

    report = {
        "mode": "apply" if args.apply else "audit",
        "source_assets": str(SOURCE_ASSETS),
        "current_animation_count": len(current_anims),
        "source_animation_count": len(source_clip_target) + len(unmapped_clips),
        "mapped_source_animations": len(source_clip_target),
        "animations_rebuilt_on_apply": (
            len(source_clip_target) if args.apply and not args.skip_animation_rebuild else 0
        ),
        "unmapped_source_animations": unmapped_clips,
        "source_material_count": len(source_material_target) + len(unmapped_materials),
        "mapped_source_materials": len(source_material_target),
        "unmapped_source_materials": unmapped_materials,
        "controllers": len(controller_sources),
        "override_controllers": len(override_sources),
        "prefab_models": len(selected_prefabs),
        "scene_animators_found": len(scene_models),
        "scene_models_promoted": sum(1 for row in prefab_rows if row["source_kind"] == "Scene Animator"),
        "binding_rows": len(binding_rows),
        "supplemental_scene_prop_bindings": supplemental_binding_count,
        "controller_referenced_unique_clips": len(referenced_clip_guids),
        "unreferenced_source_clips": len(unreferenced_source_clips),
        "unreferenced_source_clip_names": sorted(path.name for path in unreferenced_source_clips),
        "dependencies_to_copy": Counter(kind for kind, _, _, _ in copied_dependencies),
        "unresolved_dependencies": unresolved,
        "output_assets": len(all_cleaned_assets),
    }

    print("[7/8] Writing binding audit reports...", flush=True)
    REPORT_ROOT.mkdir(parents=True, exist_ok=True)
    (REPORT_ROOT / "audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2, default=dict), encoding="utf-8")
    with (REPORT_ROOT / "models.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["model", "source_kind", "source", "destination", "controllers", "class_counts"])
        writer.writeheader()
        writer.writerows(sorted(prefab_rows, key=lambda row: str(row["destination"]).casefold()))
    with (REPORT_ROOT / "bindings.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["animation", "controller", "model", "binding_basis"])
        writer.writeheader()
        writer.writerows(binding_rows)
    with (REPORT_ROOT / "dependencies.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["kind", "source", "destination", "guid"])
        writer.writeheader()
        for kind, source, target, guid in sorted(copied_dependencies, key=lambda row: str(row[2]).casefold()):
            writer.writerow({"kind": kind, "source": source, "destination": unity_asset_path(target), "guid": guid})

    if unmapped_clips or unresolved:
        print(json.dumps(report, ensure_ascii=False, indent=2, default=dict))
        print("Audit failed: unmapped animation clips or unresolved kept dependencies.", file=sys.stderr)
        return 2

    if args.apply:
        print("[8/8] Rebuilding animations and writing controllers, prefabs, meshes and avatars...", flush=True)
        # AssetStudio preserved the clips but emitted hashed placeholder paths
        # (``path_123...``) when Unity 6000 metadata could not be resolved.  Use
        # AssetRipper's correctly resolved AnimationClip YAML while retaining the
        # semantic destination and its existing .meta GUID.
        if not args.skip_animation_rebuild:
            for source_guid, target in source_clip_target.items():
                source = source_by_guid[source_guid]
                write_text(target, replace_guids(read_text(source), replacements), True)
        for kind, target, cleaned, _ in all_cleaned_assets:
            write_text(target, cleaned, True)
            source = None
            if kind == "controller":
                source = next((path for path in [*controller_sources, *override_sources] if controller_target[source_guid_by_path[path]] == target), None)
            if source:
                shutil.copy2(Path(str(source) + ".meta"), Path(str(target) + ".meta"))
        for kind, source, target, guid in copied_dependencies:
            if kind in {"Mesh", "Avatar"}:
                copy_asset_preserving_meta(source, target, True)
        # Prefabs extracted/cleaned from originals need stable new GUIDs.  Unity
        # creates those .meta files during refresh; existing metas are preserved
        # on repeat runs.

    print(json.dumps(report, ensure_ascii=False, indent=2, default=dict))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
