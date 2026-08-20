#!/usr/bin/env python3
"""Prepare a deterministic Unity material reconstruction plan.

The script combines recovered material JSON, the semantic asset manifest,
material-to-texture links, and the original player's shader PathID index. It
does not modify Unity assets; the generated plan is consumed by the editor-side
RecoveredMaterialRebuilder.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


KNOWN_FALLBACK_SHADERS = {
    "Universal Render Pipeline/Lit",
    "Universal Render Pipeline/Unlit",
    "Universal Render Pipeline/Particles/Unlit",
    "Universal Render Pipeline/Terrain/Lit",
    "Skybox/Panoramic",
    "UI/Default",
    "Sprites/Default",
}


def normalized_path(value: str) -> str:
    return str(Path(value).resolve()).casefold()


def to_asset_path(project_root: Path, value: str) -> str:
    path = Path(value).resolve()
    try:
        return path.relative_to(project_root).as_posix()
    except ValueError as exc:
        raise RuntimeError(f"Path is outside the Unity project: {path}") from exc


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def build_shader_index(assets_map: Path, cache_path: Path) -> dict[str, list[str]]:
    if cache_path.exists() and cache_path.stat().st_mtime >= assets_map.stat().st_mtime:
        cached = json.loads(cache_path.read_text(encoding="utf-8"))
        return {str(key): list(value) for key, value in cached.items()}
    index: dict[int, set[str]] = defaultdict(set)
    record: dict[str, Any] = {}
    field_re = re.compile(r'^\s*"(Name|Source|PathID|Type)"\s*:\s*(.+?)(?:,)?\s*$')
    with assets_map.open("r", encoding="utf-8-sig", errors="replace") as handle:
        for line in handle:
            stripped = line.strip()
            match = field_re.match(line)
            if match:
                key, raw = match.groups()
                try:
                    record[key] = json.loads(raw)
                except json.JSONDecodeError:
                    pass
            if stripped in {"},", "}"}:
                if record.get("Type") == "Shader" and record.get("Name") and record.get("PathID") is not None:
                    index[int(record["PathID"])].add(str(record["Name"]))
                record = {}
    serializable = {str(key): sorted(values, key=str.casefold) for key, values in sorted(index.items())}
    cache_path.parent.mkdir(parents=True, exist_ok=True)
    cache_path.write_text(json.dumps(serializable, ensure_ascii=False, indent=2), encoding="utf-8")
    return serializable


def choose_fallback(metadata_asset_path: str, name: str, properties: set[str]) -> tuple[str, str]:
    path = metadata_asset_path.casefold()
    lowered = name.casefold()
    if "/ui/" in path:
        return "UI/Default", "semantic-ui"
    if "sprite" in lowered or {"_RendererColor", "_Flip"} & properties:
        return "Sprites/Default", "sprite-signature"
    if "/vfx/" in path or any(token in lowered for token in ("particle", "trail", "vfx", "effect", "spark", "smoke", "fire", "glow")):
        return "Universal Render Pipeline/Particles/Unlit", "semantic-vfx"
    if {"_Control", "_Splat0", "_Splat1"} & properties:
        return "Universal Render Pipeline/Terrain/Lit", "terrain-signature"
    if any(token in lowered for token in ("skybox", "panoramic")) and {"_MainTex", "_Tex"} & properties:
        return "Skybox/Panoramic", "skybox-signature"
    pbr = {
        "_BaseMap", "_BumpMap", "_Metallic", "_MetallicGlossMap", "_Smoothness",
        "_OcclusionMap", "_SpecGlossMap", "_ParallaxMap",
    }
    if pbr & properties or any(part in path for part in ("/characters/", "/equipment/", "/environment/", "/props/")):
        return "Universal Render Pipeline/Lit", "pbr-or-semantic"
    return "Universal Render Pipeline/Unlit", "generic-unlit"


def shader_candidates(shader_index: dict[str, list[str]], shader_path_id: int, fallback: str) -> tuple[list[str], list[str], str]:
    original = shader_index.get(str(shader_path_id), []) if shader_path_id else []
    usable_original = [name for name in original if not name.startswith("Hidden/")]
    if len(usable_original) == 1:
        candidates = [usable_original[0], fallback]
        method = "unique-original-pathid"
    else:
        candidates = [fallback]
        method = "ambiguous-original-pathid" if usable_original else "fallback-only"
    deduped: list[str] = []
    for candidate in candidates:
        if candidate not in deduped:
            deduped.append(candidate)
    return deduped, original, method


def texenv_data(saved: dict[str, Any], property_name: str) -> dict[str, Any]:
    value = (saved.get("m_TexEnvs") or {}).get(property_name) or {}
    scale = value.get("m_Scale") or {}
    offset = value.get("m_Offset") or {}
    return {
        "scale": {"x": float(scale.get("X", 1.0)), "y": float(scale.get("Y", 1.0))},
        "offset": {"x": float(offset.get("X", 0.0)), "y": float(offset.get("Y", 0.0))},
    }


def primary_texture(textures: list[dict[str, Any]]) -> str:
    priorities = ("basemap", "maintex", "albedo", "diffuse", "basecolor", "color")
    for token in priorities:
        for texture in textures:
            prop = texture["sourceProperty"].replace("_", "").casefold()
            if token in prop:
                return texture["assetPath"]
    for texture in textures:
        prop = texture["sourceProperty"].casefold()
        if not any(token in prop for token in ("normal", "bump", "metal", "rough", "smooth", "occlusion", "mask", "height", "depth")):
            return texture["assetPath"]
    return ""


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--assets-map", type=Path, required=True)
    args = parser.parse_args()
    project_root = args.project_root.resolve()
    report_root = project_root / "Docs" / "ArtRecovery" / "Nephilite"
    rebuild_root = report_root / "MaterialRebuild"
    manifest = read_csv(report_root / "asset_manifest.csv")
    links = read_csv(report_root / "material_texture_links.csv")
    material_rows = [row for row in manifest if row.get("asset_type") == "Material"]
    links_by_source: dict[str, list[dict[str, str]]] = defaultdict(list)
    for link in links:
        links_by_source[normalized_path(link["metadata_json"])].append(link)
    shader_index = build_shader_index(args.assets_map.resolve(), rebuild_root / "shader_pathid_index.json")

    plans: list[dict[str, Any]] = []
    target_keys: set[str] = set()
    errors: list[str] = []
    for row in material_rows:
        metadata_path = Path(row["destination"]).resolve()
        metadata_asset_path = to_asset_path(project_root, str(metadata_path))
        data = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
        saved = data.get("m_SavedProperties") or {}
        floats = {str(k): float(v) for k, v in (saved.get("m_Floats") or {}).items()}
        ints = {str(k): int(v) for k, v in (saved.get("m_Ints") or {}).items()}
        colors = {
            str(k): {
                "r": float((v or {}).get("r", 0.0)),
                "g": float((v or {}).get("g", 0.0)),
                "b": float((v or {}).get("b", 0.0)),
                "a": float((v or {}).get("a", 1.0)),
            }
            for k, v in (saved.get("m_Colors") or {}).items()
        }
        source_links = links_by_source.get(normalized_path(row["recovered_source"]), [])
        textures: list[dict[str, Any]] = []
        for link in source_links:
            property_match = re.search(r"m_TexEnvs\.([^\.]+)\.m_Texture$", link["field_path"])
            if not property_match:
                errors.append(f"Unexpected texture field path: {link['field_path']}")
                continue
            resolved = link["resolved_destination"]
            if not resolved or not Path(resolved).exists():
                errors.append(f"Missing resolved texture: {metadata_path} :: {link['field_path']}")
                continue
            property_name = property_match.group(1)
            textures.append(
                {
                    "sourceProperty": property_name,
                    "referenceName": link["reference_name"],
                    "sourcePathId": int(link["path_id"] or 0),
                    "assetPath": to_asset_path(project_root, resolved),
                    "resolutionMethod": link["resolution_method"],
                    **texenv_data(saved, property_name),
                }
            )
        linked_properties = {item["sourceProperty"] for item in textures}
        unresolved_textures: list[dict[str, Any]] = []
        for property_name, env in (saved.get("m_TexEnvs") or {}).items():
            texture_ref = (env or {}).get("m_Texture") or {}
            if texture_ref.get("IsNull", True) or property_name in linked_properties:
                continue
            unresolved_textures.append(
                {
                    "sourceProperty": property_name,
                    "referenceName": str(texture_ref.get("Name") or ""),
                    "sourceFileId": int(texture_ref.get("m_FileID") or 0),
                    "sourcePathId": int(texture_ref.get("m_PathID") or 0),
                    "reason": "not-present-in-recovered-texture-map",
                }
            )

        properties = set(floats) | set(ints) | set(colors) | {item["sourceProperty"] for item in textures}
        shader_ref = data.get("m_Shader") or {}
        shader_path_id = int(shader_ref.get("m_PathID") or 0)
        fallback, fallback_reason = choose_fallback(metadata_asset_path, metadata_path.stem, properties)
        candidates, original_candidates, resolution = shader_candidates(shader_index, shader_path_id, fallback)
        metadata_parent = Path(metadata_asset_path).parent
        target_parent = metadata_parent.parent if metadata_parent.name == "RecoveredMetadata" else metadata_parent
        target_asset_path = (target_parent / f"{metadata_path.stem}.mat").as_posix()
        target_key = target_asset_path.casefold()
        if target_key in target_keys:
            errors.append(f"Duplicate material target: {target_asset_path}")
        target_keys.add(target_key)
        plans.append(
            {
                "materialName": metadata_path.stem,
                "metadataAssetPath": metadata_asset_path,
                "targetAssetPath": target_asset_path,
                "sourceGroup": row.get("source_group", ""),
                "sourceMaterialPathId": int(row.get("path_id") or 0),
                "sourceShaderFileId": int(shader_ref.get("m_FileID") or 0),
                "sourceShaderPathId": shader_path_id,
                "originalShaderCandidates": original_candidates,
                "shaderCandidates": candidates,
                "shaderResolution": resolution,
                "fallbackShader": fallback,
                "fallbackReason": fallback_reason,
                "floats": floats,
                "ints": ints,
                "colors": colors,
                "textures": sorted(textures, key=lambda item: item["sourceProperty"].casefold()),
                "unresolvedTextures": sorted(unresolved_textures, key=lambda item: item["sourceProperty"].casefold()),
                "primaryTextureAssetPath": primary_texture(textures),
            }
        )

    if errors:
        raise RuntimeError(f"Material rebuild plan has {len(errors)} error(s): {errors[:20]}")
    plans.sort(key=lambda item: item["targetAssetPath"].casefold())
    shader_counts = Counter(item["fallbackShader"] for item in plans)
    summary = {
        "materialCount": len(plans),
        "textureBindingCount": sum(len(item["textures"]) for item in plans),
        "materialsWithTextures": sum(bool(item["textures"]) for item in plans),
        "unresolvedTextureBindingCount": sum(len(item["unresolvedTextures"]) for item in plans),
        "materialsWithUnresolvedTextures": sum(bool(item["unresolvedTextures"]) for item in plans),
        "materialsWithUniqueOriginalShaderCandidate": sum(item["shaderResolution"] == "unique-original-pathid" for item in plans),
        "existingTargetCount": sum((project_root / item["targetAssetPath"]).exists() for item in plans),
        "fallbackShaderCounts": dict(shader_counts.most_common()),
    }
    rebuild_root.mkdir(parents=True, exist_ok=True)
    plan_path = rebuild_root / "material_rebuild_plan.json"
    plan_path.write_text(
        json.dumps({"schemaVersion": 1, "summary": summary, "materials": plans}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    csv_fields = [
        "materialName", "metadataAssetPath", "targetAssetPath", "sourceMaterialPathId",
        "sourceShaderPathId", "shaderResolution", "fallbackShader", "fallbackReason",
        "textureBindingCount", "originalShaderCandidates",
    ]
    with (rebuild_root / "material_rebuild_inventory.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=csv_fields, extrasaction="ignore")
        writer.writeheader()
        for item in plans:
            writer.writerow(
                {
                    **item,
                    "textureBindingCount": len(item["textures"]),
                    "originalShaderCandidates": " | ".join(item["originalShaderCandidates"]),
                }
            )
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
