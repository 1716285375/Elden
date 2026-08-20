#!/usr/bin/env python3
"""Validate recovered Animator/controller/model bindings without scene mutation."""

from __future__ import annotations

import csv
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path


PROJECT = Path(r"C:\C\Game\Unity\Elden")
ASSETS = PROJECT / "Assets"
CONTROLLERS = ASSETS / "Art/Animations/Controllers"
MODELS = ASSETS / "Art/Models/Rigged"
ANIMATIONS = ASSETS / "Art/Animations"
REPORT_DIR = PROJECT / "Docs/ArtRecovery/Nephilite/AnimationBindings"
BINDINGS = REPORT_DIR / "bindings.csv"

GUID_RE = re.compile(r"\bguid:\s*([0-9a-fA-F]{32})\b")
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$", re.MULTILINE)
BUILTIN_GUIDS = {
    "0000000000000000e000000000000000",
    "0000000000000000f000000000000000",
}


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def meta_guid(meta: Path) -> str | None:
    match = GUID_RE.search(read(meta))
    return match.group(1).lower() if match else None


def unity_path(path: Path) -> str:
    return path.relative_to(PROJECT).as_posix()


def split_docs(text: str) -> list[tuple[int, int, str]]:
    matches = list(DOC_RE.finditer(text))
    docs: list[tuple[int, int, str]] = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        docs.append((int(match.group(1)), int(match.group(2)), text[match.start():end]))
    return docs


def field_file_id(text: str, name: str) -> int:
    match = re.search(rf"^\s*{re.escape(name)}:\s*\{{fileID:\s*(-?\d+)", text, re.MULTILINE)
    return int(match.group(1)) if match else 0


def object_name(text: str) -> str:
    match = re.search(r"^\s*m_Name:\s*(.*?)\s*$", text, re.MULTILINE)
    return match.group(1).strip() if match else "Unnamed"


def component_ids(text: str) -> list[int]:
    block = re.search(r"^\s*m_Component:\s*\n(?P<body>(?:\s*- component:.*\n)*)", text, re.MULTILINE)
    return [int(value) for value in re.findall(r"fileID:\s*(-?\d+)", block.group("body"))] if block else []


def prefab_transform_paths(path: Path) -> tuple[set[str], Counter[int], int]:
    docs = split_docs(read(path))
    by_id = {file_id: (class_id, text) for class_id, file_id, text in docs}
    game_objects = {file_id: text for class_id, file_id, text in docs if class_id == 1}
    transforms = {file_id: text for class_id, file_id, text in docs if class_id == 4}
    animators = [(file_id, text) for class_id, file_id, text in docs if class_id == 95]
    counts = Counter(class_id for class_id, _, _ in docs)
    paths: set[str] = set()

    transform_for_go: dict[int, int] = {}
    for go_id, go_text in game_objects.items():
        transform_for_go[go_id] = next(
            (component for component in component_ids(go_text) if component in transforms),
            0,
        )

    for _, animator_text in animators:
        animator_go = field_file_id(animator_text, "m_GameObject")
        animator_transform = transform_for_go.get(animator_go, 0)
        if not animator_transform:
            continue
        for transform_id, transform_text in transforms.items():
            names: list[str] = []
            cursor = transform_id
            seen: set[int] = set()
            while cursor and cursor not in seen and cursor in transforms:
                seen.add(cursor)
                go_id = field_file_id(transforms[cursor], "m_GameObject")
                if cursor == animator_transform:
                    paths.add("/".join(reversed(names)))
                    break
                go_text = game_objects.get(go_id)
                if not go_text:
                    break
                names.append(object_name(go_text))
                cursor = field_file_id(transforms[cursor], "m_Father")
    return paths, counts, len(animators)


def gameobjects_without_transform(path: Path) -> list[str]:
    docs = split_docs(read(path))
    component_class = {file_id: class_id for class_id, file_id, _ in docs}
    missing: list[str] = []
    for class_id, _, text in docs:
        if class_id != 1:
            continue
        classes = {component_class.get(component) for component in component_ids(text)}
        if not classes.intersection({4, 224}):
            missing.append(object_name(text))
    return missing


def animation_paths(path: Path) -> set[str]:
    result: set[str] = set()
    # Deliberately exclude newlines around the value.  ``\s`` would let an
    # empty ``path:`` consume the next line (for example ``classID: 95``).
    for match in re.finditer(r"^[ \t]+path:[ \t]*(.*?)[ \t]*$", read(path), re.MULTILINE):
        value = match.group(1).strip()
        if value and value.isdigit():
            continue
        result.add(value)
    return result


def main() -> int:
    guid_to_asset: dict[str, Path] = {}
    duplicate_guids: defaultdict[str, list[str]] = defaultdict(list)
    for meta in ASSETS.rglob("*.meta"):
        guid = meta_guid(meta)
        asset = Path(str(meta)[:-5])
        if not guid or not asset.exists():
            continue
        duplicate_guids[guid].append(unity_path(asset))
        guid_to_asset.setdefault(guid, asset)

    duplicates = {guid: paths for guid, paths in duplicate_guids.items() if len(paths) > 1}
    generated = [
        *CONTROLLERS.rglob("*.controller"),
        *CONTROLLERS.rglob("*.overrideController"),
        *MODELS.rglob("*.prefab"),
        *MODELS.rglob("*.asset"),
        *ANIMATIONS.rglob("*.anim"),
    ]
    missing_refs: list[dict[str, str]] = []
    for asset in generated:
        for guid in sorted({match.group(1).lower() for match in GUID_RE.finditer(read(asset))}):
            if guid in BUILTIN_GUIDS or guid in guid_to_asset:
                continue
            missing_refs.append({"asset": unity_path(asset), "guid": guid})

    invalid_gameobjects: list[dict[str, object]] = []
    for prefab in MODELS.rglob("*.prefab"):
        names = gameobjects_without_transform(prefab)
        if names:
            invalid_gameobjects.append(
                {"prefab": unity_path(prefab), "count": len(names), "game_objects": names[:50]}
            )

    model_cache: dict[str, tuple[set[str], Counter[int], int]] = {}
    animation_cache: dict[str, set[str]] = {}
    path_mismatches: list[dict[str, object]] = []
    binding_rows = list(csv.DictReader(BINDINGS.open(encoding="utf-8-sig", newline="")))
    animations_with_model: set[str] = set()
    direct_model_asset_bindings = 0

    for row in binding_rows:
        model = row["model"]
        animation = row["animation"]
        if not model:
            continue
        animations_with_model.add(animation)
        if Path(model).suffix.casefold() != ".prefab":
            direct_model_asset_bindings += 1
            continue
        if model not in model_cache:
            model_cache[model] = prefab_transform_paths(PROJECT / model)
        if animation not in animation_cache:
            animation_cache[animation] = animation_paths(PROJECT / animation)
        model_paths = model_cache[model][0]
        clip_paths = animation_cache[animation]
        missing_paths = sorted(path for path in clip_paths if path not in model_paths)
        if missing_paths:
            path_mismatches.append(
                {
                    "animation": animation,
                    "model": model,
                    "missing_path_count": len(missing_paths),
                    "missing_paths": missing_paths[:20],
                }
            )

    unique_path_mismatches: dict[tuple[str, str], dict[str, object]] = {}
    for mismatch in path_mismatches:
        unique_path_mismatches[(str(mismatch["animation"]), str(mismatch["model"]))] = mismatch

    model_summaries: list[dict[str, object]] = []
    for model, (paths, counts, animator_count) in sorted(model_cache.items()):
        model_summaries.append(
            {
                "model": model,
                "animators": animator_count,
                "game_objects": counts[1],
                "transforms": counts[4],
                "mesh_renderers": counts[23],
                "mesh_filters": counts[33],
                "skinned_mesh_renderers": counts[137],
                "animation_paths": len(paths),
            }
        )

    all_current_animations = {unity_path(path) for path in ANIMATIONS.rglob("*.anim")}
    bound_animations = {row["animation"] for row in binding_rows}
    report = {
        "project_meta_guids": len(guid_to_asset),
        "duplicate_guid_count": len(duplicates),
        "duplicate_guids": duplicates,
        "generated_assets_checked": len(generated),
        "missing_reference_count": len(missing_refs),
        "missing_references": missing_refs,
        "gameobjects_without_transform_count": sum(int(row["count"]) for row in invalid_gameobjects),
        "gameobjects_without_transform": invalid_gameobjects,
        "controller_count": len(list(CONTROLLERS.rglob("*.controller"))),
        "override_controller_count": len(list(CONTROLLERS.rglob("*.overrideController"))),
        "model_prefab_count": len(list(MODELS.rglob("*.prefab"))),
        "mesh_count": len(list((MODELS / "Shared/Meshes").glob("*.asset"))),
        "avatar_count": len(list((MODELS / "Shared/Avatars").glob("*.asset"))),
        "binding_rows": len(binding_rows),
        "bound_unique_animations": len(bound_animations),
        "animations_with_model": len(animations_with_model),
        "current_animation_count": len(all_current_animations),
        "unbound_animations": sorted(all_current_animations - bound_animations),
        # A controller can be reused by multiple characters and equipment is
        # attached dynamically, so an exact prefab hierarchy mismatch is a
        # diagnostic rather than a broken binding.  GUID integrity and the
        # original Animator/controller/Avatar relationship are authoritative.
        "path_diagnostic_pair_count": len(unique_path_mismatches),
        "direct_model_asset_bindings": direct_model_asset_bindings,
        "path_mismatches": list(unique_path_mismatches.values()),
        "models": model_summaries,
    }
    (REPORT_DIR / "validation.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8", newline="\n"
    )
    print(json.dumps({key: value for key, value in report.items() if key not in {"duplicate_guids", "missing_references", "gameobjects_without_transform", "path_mismatches", "models", "unbound_animations"}}, ensure_ascii=False, indent=2))
    print("Unbound animations:")
    for value in report["unbound_animations"]:
        print(f"  {value}")
    if duplicates or missing_refs or invalid_gameobjects:
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
