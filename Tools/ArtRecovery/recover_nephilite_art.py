#!/usr/bin/env python3
"""Inventory and copy recovered Nephilite art with PathID provenance.

The AssetStudio export keeps each file unique with ``__pathid_<id>``.  This
tool joins those files to assets_map.json by (serialized source file, PathID),
then writes a reproducible manifest and optionally copies authoring-useful art
into the Unity project's existing Assets/Art categories.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import shutil
from collections import Counter, defaultdict
from functools import lru_cache
from pathlib import Path
from typing import Any, Iterable, Iterator


PATH_ID_RE = re.compile(r"__pathid_(-?\d+)\.[^.]+$", re.IGNORECASE)
CODE_TYPE_RE = re.compile(
    r"\b(Texture2D|Texture|RenderTexture|Sprite|SpriteAtlas|Material|Mesh|"
    r"SkinnedMeshRenderer|MeshRenderer|GameObject|AnimationClip|"
    r"RuntimeAnimatorController|AnimatorOverrideController|Avatar|AudioClip|"
    r"Font|TMP_FontAsset|ParticleSystem|VisualEffect|Shader|VideoClip|Cubemap)\b"
)
RESOURCE_API_RE = re.compile(
    r"Resources\.(?:Load|LoadAsync)|Addressables\.|AssetBundle\.|"
    r"StreamingAssets|SpriteAtlas|VideoClip"
)

TYPE_DESTINATIONS = {
    "Texture2D": ("Textures", "converted"),
    "Cubemap": ("Textures", "converted"),
    "Sprite": ("Sprites", "converted"),
    "Mesh": ("Models", "converted"),
    "AnimationClip": ("Animations", "converted"),
    "AudioClip": ("Audio", "converted"),
    "Font": ("Fonts", "converted"),
    "Material": ("Materials/RecoveredJson", "converted"),
    "AnimatorController": ("Animations/RecoveredMetadata", "converted"),
    "AnimatorOverrideController": ("Animations/RecoveredMetadata", "converted"),
    "Avatar": ("Animations/RecoveredMetadata", "converted"),
    "SpriteAtlas": ("Sprites/RecoveredMetadata", "converted"),
    "VideoClip": ("Video", "converted"),
}

TYPE_EXTENSIONS = {
    "Texture2D": {".png"},
    "Cubemap": {".png"},
    "Sprite": {".png"},
    "Mesh": {".obj"},
    "AnimationClip": {".anim"},
    "AudioClip": {".wav"},
    "Font": {".ttf"},
    "Material": {".json"},
    "AnimatorController": {".json"},
    "AnimatorOverrideController": {".json"},
    "Avatar": {".json"},
    "SpriteAtlas": {".json"},
    "VideoClip": {".mp4", ".webm"},
}

FALLBACK_TYPES = {
    ".png": "Texture2D",
    ".obj": "Mesh",
    ".anim": "AnimationClip",
    ".wav": "AudioClip",
    ".ttf": "Font",
}

STREAMING_DESTINATIONS = {
    ".png": "Textures",
    ".jpg": "Textures",
    ".jpeg": "Textures",
    ".tga": "Textures",
    ".bmp": "Textures",
    ".dds": "Textures",
    ".obj": "Models",
    ".fbx": "Models",
    ".wav": "Audio",
    ".ogg": "Audio",
    ".mp3": "Audio",
    ".ttf": "Fonts",
    ".otf": "Fonts",
    ".mp4": "Video",
    ".webm": "Video",
}

MANIFEST_FIELDS = [
    "asset_name",
    "asset_type",
    "container",
    "serialized_source",
    "source_group",
    "path_id",
    "recovered_source",
    "destination",
    "extension",
    "bytes",
    "match_method",
    "copy_status",
]


def iter_json_array(path: Path, chunk_size: int = 1024 * 1024) -> Iterator[dict[str, Any]]:
    """Stream a top-level JSON array without loading the 500+ MiB map at once."""
    decoder = json.JSONDecoder()
    with path.open("r", encoding="utf-8-sig") as handle:
        buffer = ""
        position = 0
        started = False
        eof = False
        while True:
            if position >= len(buffer) and not eof:
                buffer = handle.read(chunk_size)
                position = 0
                eof = not buffer
            while position < len(buffer) and buffer[position].isspace():
                position += 1
            if not started:
                if position >= len(buffer):
                    if eof:
                        raise ValueError(f"Empty JSON document: {path}")
                    continue
                if buffer[position] != "[":
                    raise ValueError(f"Expected a top-level JSON array: {path}")
                position += 1
                started = True
                continue
            while position < len(buffer) and (buffer[position].isspace() or buffer[position] == ","):
                position += 1
            if position < len(buffer) and buffer[position] == "]":
                return
            if position >= len(buffer):
                if eof:
                    raise ValueError(f"Unexpected end of JSON array: {path}")
                buffer = ""
                position = 0
                continue
            try:
                value, end = decoder.raw_decode(buffer, position)
            except json.JSONDecodeError:
                if eof:
                    raise
                buffer = buffer[position:] + handle.read(chunk_size)
                position = 0
                eof = handle.tell() == path.stat().st_size
                continue
            if not isinstance(value, dict):
                raise ValueError(f"Expected object in JSON array, got {type(value).__name__}")
            yield value
            position = end
            if position > chunk_size:
                buffer = buffer[position:]
                position = 0


def source_leaf(source: str) -> str:
    return source.replace("\\", "/").rstrip("/").rsplit("/", 1)[-1]


def forensic_index(root: Path) -> tuple[dict[tuple[str, int], list[Path]], Counter[str], int]:
    index: dict[tuple[str, int], list[Path]] = defaultdict(list)
    extensions: Counter[str] = Counter()
    total_bytes = 0
    if not root.exists():
        return index, extensions, total_bytes
    for directory, _, filenames in os.walk(root):
        directory_path = Path(directory)
        try:
            group = directory_path.relative_to(root).parts[0].lower()
        except (ValueError, IndexError):
            continue
        for filename in filenames:
            match = PATH_ID_RE.search(filename)
            if not match:
                continue
            path = directory_path / filename
            index[(group, int(match.group(1)))].append(path)
            extensions[path.suffix.lower()] += 1
            total_bytes += path.stat().st_size
    return index, extensions, total_bytes


def scan_code(code_root: Path) -> tuple[list[dict[str, Any]], list[dict[str, Any]], Counter[str]]:
    fields: list[dict[str, Any]] = []
    api_refs: list[dict[str, Any]] = []
    type_counts: Counter[str] = Counter()
    for path in sorted(code_root.rglob("*.cs")):
        try:
            lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        except OSError:
            continue
        relative = path.relative_to(code_root).as_posix()
        for line_number, line in enumerate(lines, 1):
            types = sorted(set(CODE_TYPE_RE.findall(line)))
            if types:
                for art_type in types:
                    type_counts[art_type] += 1
                fields.append(
                    {
                        "file": relative,
                        "line": line_number,
                        "art_types": ";".join(types),
                        "code": line.strip(),
                    }
                )
            if RESOURCE_API_RE.search(line):
                api_refs.append({"file": relative, "line": line_number, "code": line.strip()})
    return fields, api_refs, type_counts


def safe_group(value: str) -> str:
    value = re.sub(r"[<>:\"/\\|?*]", "_", value).strip(" .")
    return value or "unknown_source"


def recovered_name(path: Path) -> str:
    return re.sub(r"__pathid_-?\d+$", "", path.stem, flags=re.IGNORECASE)


def normalized_name(value: str) -> str:
    return re.sub(r"[^\w]+", "", value.casefold(), flags=re.UNICODE)


def choose_candidates(candidates: list[Path], asset_type: str, asset_name: str) -> list[Path]:
    expected_extensions = TYPE_EXTENSIONS[asset_type]
    typed = [path for path in candidates if path.suffix.lower() in expected_extensions]
    if ".json" in expected_extensions:
        typed = [
            path for path in typed
            if classify_json_path(str(path)) == asset_type
        ]
    if len(typed) <= 1:
        return typed
    expected_name = normalized_name(asset_name)
    named = [path for path in typed if normalized_name(recovered_name(path)) == expected_name]
    if named:
        return named[:1]
    return []


def read_json_object(path: Path) -> dict[str, Any] | None:
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig", errors="replace"))
    except (OSError, json.JSONDecodeError):
        return None
    return value if isinstance(value, dict) else None


def classify_json_art(value: dict[str, Any]) -> str | None:
    if "m_Shader" in value and "m_SavedProperties" in value:
        return "Material"
    if "m_PackedSprites" in value or "m_PackedSpriteNamesToIndex" in value:
        return "SpriteAtlas"
    if "m_AnimatorParameters" in value or "m_AnimatorLayers" in value:
        return "AnimatorController"
    if "m_Clips" in value and "m_Controller" in value:
        return "AnimatorOverrideController"
    if "m_AvatarSize" in value and ("m_AvatarSkeleton" in value or "m_HumanDescription" in value):
        return "Avatar"
    return None


@lru_cache(maxsize=None)
def classify_json_path(path: str) -> str | None:
    value = read_json_object(Path(path))
    return classify_json_art(value) if value is not None else None


def iter_pptr_references(value: Any, field_path: str = "") -> Iterator[tuple[str, int, int, str]]:
    if isinstance(value, dict):
        if "m_FileID" in value and "m_PathID" in value:
            try:
                file_id = int(value["m_FileID"])
                path_id = int(value["m_PathID"])
            except (TypeError, ValueError):
                pass
            else:
                reference_name = str(value.get("Name", value.get("m_Name", "")))
                yield field_path, file_id, path_id, reference_name
        for key, child in value.items():
            child_path = f"{field_path}.{key}" if field_path else str(key)
            yield from iter_pptr_references(child, child_path)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from iter_pptr_references(child, f"{field_path}[{index}]")


def expected_types_for_field(field_path: str) -> set[str] | None:
    lowered = field_path.casefold()
    if "material" in lowered:
        return {"Material"}
    if "texture" in lowered or "texenv" in lowered:
        return {"Texture2D", "Sprite"}
    if "sprite" in lowered:
        return {"Sprite", "Texture2D"}
    if "mesh" in lowered:
        return {"Mesh"}
    if "audio" in lowered:
        return {"AudioClip"}
    if "animation" in lowered or lowered.endswith(".m_clip"):
        return {"AnimationClip", "AudioClip"}
    if "avatar" in lowered:
        return {"Avatar"}
    if "controller" in lowered:
        return {"AnimatorController", "AnimatorOverrideController"}
    if "font" in lowered:
        return {"Font"}
    return None


def destination_for(project_root: Path, asset_type: str, source_group: str, source_path: Path) -> Path:
    category, _ = TYPE_DESTINATIONS[asset_type]
    return project_root / "Assets" / "Art" / Path(category) / "Nephilite" / safe_group(source_group) / source_path.name


def hash_file(path: Path, block_size: int = 1024 * 1024) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while block := handle.read(block_size):
            digest.update(block)
    return digest.hexdigest()


def copy_one(source: Path, destination: Path) -> str:
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists():
        if source.stat().st_size == destination.stat().st_size and hash_file(source) == hash_file(destination):
            return "already-identical"
        raise FileExistsError(f"Refusing to overwrite different file: {destination}")
    shutil.copy2(source, destination)
    return "copied"


def write_csv(path: Path, rows: Iterable[dict[str, Any]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--assets-root", type=Path, required=True)
    parser.add_argument("--code-root", type=Path, required=True)
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--copy", action="store_true", help="Execute the generated copy plan")
    args = parser.parse_args()

    assets_root = args.assets_root.resolve()
    code_root = args.code_root.resolve()
    project_root = args.project_root.resolve()
    map_path = assets_root / "assets_map.json"
    converted_root = assets_root / "Forensic" / "Converted"
    report_root = project_root / "Docs" / "ArtRecovery" / "Nephilite"

    for required in (map_path, converted_root, code_root, project_root / "Assets"):
        if not required.exists():
            raise FileNotFoundError(required)

    print("Indexing converted forensic files...", flush=True)
    converted, converted_extensions, converted_bytes = forensic_index(converted_root)
    print(f"Indexed {sum(len(v) for v in converted.values()):,} converted files", flush=True)

    print("Scanning decompiled code for art dependency types...", flush=True)
    code_fields, api_refs, code_type_counts = scan_code(code_root)

    manifest: list[dict[str, Any]] = []
    unmatched_selected: list[dict[str, Any]] = []
    unmatched_sample_keys: set[tuple[str, int, str, str]] = set()
    unmatched_selected_record_count = 0
    unmatched_selected_type_counts: Counter[str] = Counter()
    map_type_counts: Counter[str] = Counter()
    selected_type_counts: Counter[str] = Counter()
    nonempty_container_counts: Counter[str] = Counter()
    total_map_records = 0

    print("Streaming assets_map.json and joining by source + PathID...", flush=True)
    for record in iter_json_array(map_path):
        total_map_records += 1
        asset_type = str(record.get("Type", ""))
        name = str(record.get("Name", ""))
        container = str(record.get("Container", ""))
        serialized_source = str(record.get("Source", ""))
        path_id = int(record.get("PathID", 0))
        map_type_counts[asset_type] += 1
        if container:
            nonempty_container_counts[asset_type] += 1
        if asset_type not in TYPE_DESTINATIONS:
            continue
        source_group = source_leaf(serialized_source) + "_export"
        candidates = choose_candidates(
            converted.get((source_group.lower(), path_id), []), asset_type, name
        )
        if not candidates:
            unmatched_selected_record_count += 1
            unmatched_selected_type_counts[asset_type] += 1
            sample_key = (source_group.lower(), path_id, asset_type, name)
            if len(unmatched_selected) < 5000 and sample_key not in unmatched_sample_keys:
                unmatched_sample_keys.add(sample_key)
                unmatched_selected.append(
                    {
                        "asset_name": name,
                        "asset_type": asset_type,
                        "container": container,
                        "serialized_source": serialized_source,
                        "source_group": source_group,
                        "path_id": path_id,
                    }
                )
            continue
        for recovered_source in candidates:
            manifest_asset_type = "Texture2D" if asset_type in {"Sprite", "Cubemap"} else asset_type
            destination = destination_for(project_root, manifest_asset_type, source_group, recovered_source)
            row = {
                "asset_name": name,
                "asset_type": manifest_asset_type,
                "container": container,
                "serialized_source": serialized_source,
                "source_group": source_group,
                "path_id": path_id,
                "recovered_source": str(recovered_source),
                "destination": str(destination),
                "extension": recovered_source.suffix.lower(),
                "bytes": recovered_source.stat().st_size,
                "match_method": "serialized-source+pathid",
                "copy_status": "planned",
            }
            manifest.append(row)
            selected_type_counts[manifest_asset_type] += 1

    matched_recovered_sources = {row["recovered_source"].lower() for row in manifest}
    fallback_files_added = 0
    for (source_group_lower, path_id), candidates in converted.items():
        for recovered_source in candidates:
            if recovered_source.suffix.lower() not in FALLBACK_TYPES:
                continue
            if str(recovered_source).lower() in matched_recovered_sources:
                continue
            asset_type = FALLBACK_TYPES[recovered_source.suffix.lower()]
            source_group = recovered_source.relative_to(converted_root).parts[0]
            destination = destination_for(project_root, asset_type, source_group, recovered_source)
            manifest.append(
                {
                    "asset_name": recovered_name(recovered_source),
                    "asset_type": asset_type,
                    "container": "",
                    "serialized_source": "",
                    "source_group": source_group,
                    "path_id": path_id,
                    "recovered_source": str(recovered_source),
                    "destination": str(destination),
                    "extension": recovered_source.suffix.lower(),
                    "bytes": recovered_source.stat().st_size,
                    "match_method": "converted-format-fallback",
                    "copy_status": "planned",
                }
            )
            matched_recovered_sources.add(str(recovered_source).lower())
            fallback_files_added += 1

    structurally_classified_json_counts: Counter[str] = Counter()
    structurally_added_json_files = 0
    json_files = sorted(converted_root.rglob("*.json"))
    for recovered_source in json_files:
        value = read_json_object(recovered_source)
        if value is None:
            continue
        asset_type = classify_json_art(value)
        if asset_type is None:
            continue
        structurally_classified_json_counts[asset_type] += 1
        if str(recovered_source).lower() in matched_recovered_sources:
            continue
        source_group = recovered_source.relative_to(converted_root).parts[0]
        path_match = PATH_ID_RE.search(recovered_source.name)
        path_id = int(path_match.group(1)) if path_match else 0
        destination = destination_for(project_root, asset_type, source_group, recovered_source)
        manifest.append(
            {
                "asset_name": str(value.get("m_Name", value.get("Name", recovered_name(recovered_source)))),
                "asset_type": asset_type,
                "container": "",
                "serialized_source": "",
                "source_group": source_group,
                "path_id": path_id,
                "recovered_source": str(recovered_source),
                "destination": str(destination),
                "extension": ".json",
                "bytes": recovered_source.stat().st_size,
                "match_method": "json-structure-classification",
                "copy_status": "planned",
            }
        )
        matched_recovered_sources.add(str(recovered_source).lower())
        structurally_added_json_files += 1

    streaming_rows: list[dict[str, Any]] = []
    streaming_root = assets_root / "Original-StreamingAssets"
    if streaming_root.exists():
        for source in sorted(path for path in streaming_root.rglob("*") if path.is_file()):
            category = STREAMING_DESTINATIONS.get(source.suffix.lower())
            if not category:
                continue
            relative = source.relative_to(streaming_root)
            destination = project_root / "Assets" / "Art" / category / "Nephilite" / "Original-StreamingAssets" / relative
            row = {
                "asset_name": source.stem,
                "asset_type": "StreamingAsset",
                "container": relative.as_posix(),
                "serialized_source": str(streaming_root),
                "source_group": "Original-StreamingAssets",
                "path_id": "",
                "recovered_source": str(source),
                "destination": str(destination),
                "extension": source.suffix.lower(),
                "bytes": source.stat().st_size,
                "match_method": "streaming-art-extension",
                "copy_status": "planned",
            }
            manifest.append(row)
            streaming_rows.append(row)

    deduplicated_sources: dict[str, dict[str, Any]] = {}
    duplicate_recovered_sources_collapsed = 0
    for row in manifest:
        key = row["recovered_source"].lower()
        existing = deduplicated_sources.get(key)
        if existing is None:
            deduplicated_sources[key] = row
            continue
        duplicate_recovered_sources_collapsed += 1
        existing_rank = (0 if existing["match_method"] == "serialized-source+pathid" else 1,)
        row_rank = (0 if row["match_method"] == "serialized-source+pathid" else 1,)
        if row_rank < existing_rank:
            deduplicated_sources[key] = row
    manifest = list(deduplicated_sources.values())

    deduplicated: dict[str, dict[str, Any]] = {}
    duplicate_map_matches_collapsed = 0
    for row in manifest:
        key = row["destination"].lower()
        existing = deduplicated.get(key)
        if existing is None:
            deduplicated[key] = row
            continue
        if existing["recovered_source"].lower() != row["recovered_source"].lower():
            raise RuntimeError(
                "Different recovered files map to the same destination: "
                f"{existing['recovered_source']} and {row['recovered_source']}"
            )
        duplicate_map_matches_collapsed += 1
    manifest = list(deduplicated.values())
    manifest.sort(key=lambda row: (row["asset_type"], row["source_group"], int(row["path_id"] or 0), row["recovered_source"]))
    selected_manifest_type_counts = Counter(row["asset_type"] for row in manifest)
    selected_manifest_extension_counts = Counter(row["extension"] for row in manifest)

    lookup_by_group_path: dict[tuple[str, int], list[dict[str, Any]]] = defaultdict(list)
    lookup_by_name: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in manifest:
        if row["path_id"] != "":
            lookup_by_group_path[(row["source_group"].lower(), int(row["path_id"]))].append(row)
        lookup_by_name[normalized_name(row["asset_name"])].append(row)

    reference_links: list[dict[str, Any]] = []
    material_texture_links: list[dict[str, Any]] = []
    for json_path in json_files:
        value = read_json_object(json_path)
        if value is None:
            continue
        source_group = json_path.relative_to(converted_root).parts[0]
        json_kind = classify_json_art(value) or "ComponentMetadata"
        root_name = str(value.get("m_Name", value.get("Name", recovered_name(json_path))))
        for field_path, file_id, path_id, reference_name in iter_pptr_references(value):
            if path_id == 0:
                continue
            candidates = list(lookup_by_group_path.get((source_group.lower(), path_id), []))
            expected_types = expected_types_for_field(field_path)
            if expected_types is None and not reference_name:
                continue
            if expected_types:
                candidates = [row for row in candidates if row["asset_type"] in expected_types]
            resolution_method = "source-group+pathid"
            if reference_name:
                named = [
                    row for row in candidates
                    if normalized_name(row["asset_name"]) == normalized_name(reference_name)
                ]
                if named:
                    candidates = named
                    resolution_method += "+name"
                else:
                    candidates = []
            if len(candidates) != 1 and reference_name:
                global_named = list(lookup_by_name.get(normalized_name(reference_name), []))
                if expected_types:
                    global_named = [row for row in global_named if row["asset_type"] in expected_types]
                unique_sources = {row["recovered_source"].lower() for row in global_named}
                if len(global_named) == 1 or len(unique_sources) == 1:
                    candidates = global_named[:1]
                    resolution_method = "globally-unique-name"
            if len(candidates) != 1:
                continue
            resolved = candidates[0]
            link = {
                "metadata_json": str(json_path),
                "metadata_kind": json_kind,
                "metadata_name": root_name,
                "source_group": source_group,
                "field_path": field_path,
                "file_id": file_id,
                "path_id": path_id,
                "reference_name": reference_name,
                "resolved_asset_type": resolved["asset_type"],
                "resolved_asset_name": resolved["asset_name"],
                "resolved_source": resolved["recovered_source"],
                "resolved_destination": resolved["destination"],
                "resolution_method": resolution_method,
            }
            reference_links.append(link)
            if json_kind == "Material" and resolved["asset_type"] in {"Texture2D", "Sprite"}:
                material_texture_links.append(link)

    if args.copy:
        print(f"Copying {len(manifest):,} selected files...", flush=True)
        for index, row in enumerate(manifest, 1):
            row["copy_status"] = copy_one(Path(row["recovered_source"]), Path(row["destination"]))
            if index % 250 == 0 or index == len(manifest):
                print(f"  {index:,}/{len(manifest):,}", flush=True)

    write_csv(report_root / "asset_manifest.csv", manifest, MANIFEST_FIELDS)
    write_csv(
        report_root / "unmatched_selected_assets.csv",
        unmatched_selected,
        ["asset_name", "asset_type", "container", "serialized_source", "source_group", "path_id"],
    )
    write_csv(report_root / "code_art_references.csv", code_fields, ["file", "line", "art_types", "code"])
    write_csv(report_root / "code_resource_api_references.csv", api_refs, ["file", "line", "code"])
    link_fields = [
        "metadata_json", "metadata_kind", "metadata_name", "source_group", "field_path",
        "file_id", "path_id", "reference_name", "resolved_asset_type", "resolved_asset_name",
        "resolved_source", "resolved_destination", "resolution_method",
    ]
    write_csv(report_root / "asset_reference_links.csv", reference_links, link_fields)
    write_csv(report_root / "material_texture_links.csv", material_texture_links, link_fields)

    summary = {
        "source": {
            "assets_root": str(assets_root),
            "code_root": str(code_root),
            "assets_map_records": total_map_records,
            "converted_files_indexed": sum(len(v) for v in converted.values()),
            "converted_bytes_indexed": converted_bytes,
            "converted_extension_counts": dict(converted_extensions.most_common()),
        },
        "code_evidence": {
            "reference_lines": len(code_fields),
            "resource_api_reference_lines": len(api_refs),
            "art_type_reference_counts": dict(code_type_counts.most_common()),
        },
        "selection": {
            "planned_files": len(manifest),
            "planned_bytes": sum(int(row["bytes"]) for row in manifest),
            "asset_type_match_counts_before_deduplication": dict(selected_type_counts.most_common()),
            "asset_type_counts": dict(selected_manifest_type_counts.most_common()),
            "extension_counts": dict(selected_manifest_extension_counts.most_common()),
            "streaming_art_files": len(streaming_rows),
            "fallback_files_added": fallback_files_added,
            "structurally_classified_json_counts": dict(structurally_classified_json_counts.most_common()),
            "structurally_added_json_files": structurally_added_json_files,
            "resolved_asset_reference_links": len(reference_links),
            "resolved_material_texture_links": len(material_texture_links),
            "unmatched_selected_index_records": unmatched_selected_record_count,
            "unmatched_selected_index_type_counts": dict(unmatched_selected_type_counts.most_common()),
            "unmatched_selected_samples_written": len(unmatched_selected),
            "duplicate_map_matches_collapsed": duplicate_map_matches_collapsed,
            "duplicate_recovered_sources_collapsed": duplicate_recovered_sources_collapsed,
            "copy_executed": args.copy,
            "copy_status_counts": dict(Counter(row["copy_status"] for row in manifest)),
        },
        "assets_map": {
            "type_counts": dict(map_type_counts.most_common()),
            "types_with_nonempty_container_counts": dict(nonempty_container_counts.most_common()),
        },
    }
    report_root.mkdir(parents=True, exist_ok=True)
    (report_root / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary["selection"], ensure_ascii=False, indent=2), flush=True)
    print(f"Reports: {report_root}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
