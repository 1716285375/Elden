#!/usr/bin/env python3
"""Generate Unity 6 Material YAML files from the recovered material plan.

The script deliberately uses Unity-authored template materials for shader and
serialization metadata. Recovered properties are merged over the template
defaults, and only texture links resolved by the recovery manifest are bound.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import os
import re
import sys
from collections import OrderedDict
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
PLAN_PATH = PROJECT_ROOT / "Docs/ArtRecovery/Nephilite/MaterialRebuild/material_rebuild_plan.json"
REPORT_DIR = PROJECT_ROOT / "Docs/ArtRecovery/Nephilite/MaterialRebuild"
MATERIAL_ROOT = (PROJECT_ROOT / "Assets/Art/Materials").resolve()
TEMPLATE_ROOT = PROJECT_ROOT / "Assets/Editor/ArtRecovery/MaterialTemplates"

TEMPLATE_BY_SHADER = {
    "Universal Render Pipeline/Lit": TEMPLATE_ROOT / "Template_URP_Lit.mat",
    "Universal Render Pipeline/Unlit": TEMPLATE_ROOT / "Template_URP_Unlit.mat",
    "Universal Render Pipeline/Particles/Unlit": TEMPLATE_ROOT / "Template_URP_Particles_Unlit.mat",
    "UI/Default": TEMPLATE_ROOT / "Template_UI_Default.mat",
    "Sprites/Default": TEMPLATE_ROOT / "Template_Sprites_Default.mat",
}

GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)
SAVED_PROPERTIES_RE = re.compile(
    r"^  m_SavedProperties:\n.*?(?=^  m_BuildTextureStacks:)", re.MULTILINE | re.DOTALL
)


def require_within(path: Path, root: Path, label: str) -> None:
    try:
        path.resolve().relative_to(root.resolve())
    except ValueError as exc:
        raise ValueError(f"{label} escapes allowed root: {path}") from exc


def number(value: Any) -> str:
    numeric = float(value)
    if not math.isfinite(numeric):
        raise ValueError(f"Non-finite numeric material value: {value!r}")
    if numeric == 0:
        return "0"
    return format(numeric, ".17g")


def yaml_string(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


def parse_pair(text: str) -> dict[str, float]:
    values = {}
    for key, raw in re.findall(r"([xy]):\s*([^,}]+)", text):
        values[key] = float(raw.strip())
    return {"x": values.get("x", 0.0), "y": values.get("y", 0.0)}


def parse_color(text: str) -> dict[str, float]:
    values = {}
    for key, raw in re.findall(r"([rgba]):\s*([^,}]+)", text):
        values[key] = float(raw.strip())
    return {
        "r": values.get("r", 0.0),
        "g": values.get("g", 0.0),
        "b": values.get("b", 0.0),
        "a": values.get("a", 0.0),
    }


def extract_section(saved: str, name: str, next_name: str | None) -> str:
    end = rf"(?=^    {re.escape(next_name)}:)" if next_name else r"\Z"
    match = re.search(
        rf"^    {re.escape(name)}:\s*(.*?)" + end,
        saved,
        re.MULTILINE | re.DOTALL,
    )
    if not match:
        raise ValueError(f"Template is missing {name}")
    return match.group(1).strip("\n")


def parse_template_defaults(text: str) -> dict[str, OrderedDict[str, Any]]:
    match = SAVED_PROPERTIES_RE.search(text)
    if not match:
        raise ValueError("Template has no m_SavedProperties block")
    saved = match.group(0)

    textures: OrderedDict[str, Any] = OrderedDict()
    tex_section = extract_section(saved, "m_TexEnvs", "m_Ints")
    tex_pattern = re.compile(
        r"^    - ([A-Za-z0-9_.]+):\n"
        r"        m_Texture: (\{[^\n]+\})\n"
        r"        m_Scale: (\{[^\n]+\})\n"
        r"        m_Offset: (\{[^\n]+\})",
        re.MULTILINE,
    )
    for prop, texture_ref, scale, offset in tex_pattern.findall(tex_section):
        textures[prop] = {
            "textureRef": texture_ref,
            "scale": parse_pair(scale),
            "offset": parse_pair(offset),
        }

    def scalar_map(section_name: str, next_name: str) -> OrderedDict[str, float]:
        result: OrderedDict[str, float] = OrderedDict()
        section = extract_section(saved, section_name, next_name)
        if section.strip() == "[]":
            return result
        for prop, raw in re.findall(r"^    - ([A-Za-z0-9_.]+):\s*([^\n]+)$", section, re.MULTILINE):
            result[prop] = float(raw.strip())
        return result

    ints = scalar_map("m_Ints", "m_Floats")
    floats = scalar_map("m_Floats", "m_Colors")

    colors: OrderedDict[str, Any] = OrderedDict()
    color_section = extract_section(saved, "m_Colors", None)
    if color_section.strip() != "[]":
        for prop, raw in re.findall(
            r"^    - ([A-Za-z0-9_.]+):\s*(\{[^\n]+\})$", color_section, re.MULTILINE
        ):
            colors[prop] = parse_color(raw)

    return {"textures": textures, "ints": ints, "floats": floats, "colors": colors}


def texture_guid(asset_path: str, cache: dict[str, str]) -> str:
    if asset_path in cache:
        return cache[asset_path]
    asset = (PROJECT_ROOT / Path(asset_path)).resolve()
    require_within(asset, PROJECT_ROOT / "Assets", "Texture asset")
    meta = Path(str(asset) + ".meta")
    if not asset.is_file() or not meta.is_file():
        raise FileNotFoundError(f"Texture or .meta missing: {asset_path}")
    match = GUID_RE.search(meta.read_text(encoding="utf-8-sig"))
    if not match:
        raise ValueError(f"No Unity GUID in {meta}")
    cache[asset_path] = match.group(1).lower()
    return cache[asset_path]


def first_texture(textures: OrderedDict[str, Any], exact: tuple[str, ...], contains: tuple[str, ...]) -> str | None:
    bound = {key: value for key, value in textures.items() if value["textureRef"] != "{fileID: 0}"}
    lower_to_key = {key.lower(): key for key in bound}
    for candidate in exact:
        if candidate.lower() in lower_to_key:
            return lower_to_key[candidate.lower()]
    rejected = ("normal", "bump", "mask", "metal", "rough", "smooth", "occl", "emiss", "height", "parallax", "cube")
    for key in bound:
        lowered = key.lower()
        if any(token in lowered for token in contains) and not any(token in lowered for token in rejected):
            return key
    return None


def copy_texture_alias(textures: OrderedDict[str, Any], source: str | None, aliases: tuple[str, ...]) -> list[str]:
    if not source:
        return []
    added = []
    for alias in aliases:
        current = textures.get(alias)
        if current is None or current["textureRef"] == "{fileID: 0}":
            textures[alias] = dict(textures[source])
            added.append(alias)
    return added


def is_bound(textures: OrderedDict[str, Any], prop: str) -> bool:
    return prop in textures and textures[prop]["textureRef"] != "{fileID: 0}"


def build_saved_properties(
    textures: OrderedDict[str, Any],
    ints: OrderedDict[str, float],
    floats: OrderedDict[str, float],
    colors: OrderedDict[str, Any],
) -> str:
    lines = ["  m_SavedProperties:", "    serializedVersion: 3"]
    if textures:
        lines.append("    m_TexEnvs:")
        for prop in sorted(textures, key=str.casefold):
            item = textures[prop]
            scale = item["scale"]
            offset = item["offset"]
            lines.extend(
                [
                    f"    - {prop}:",
                    f"        m_Texture: {item['textureRef']}",
                    f"        m_Scale: {{x: {number(scale['x'])}, y: {number(scale['y'])}}}",
                    f"        m_Offset: {{x: {number(offset['x'])}, y: {number(offset['y'])}}}",
                ]
            )
    else:
        lines.append("    m_TexEnvs: []")

    if ints:
        lines.append("    m_Ints:")
        for prop in sorted(ints, key=str.casefold):
            lines.append(f"    - {prop}: {int(round(float(ints[prop])))}")
    else:
        lines.append("    m_Ints: []")

    if floats:
        lines.append("    m_Floats:")
        for prop in sorted(floats, key=str.casefold):
            lines.append(f"    - {prop}: {number(floats[prop])}")
    else:
        lines.append("    m_Floats: []")

    if colors:
        lines.append("    m_Colors:")
        for prop in sorted(colors, key=str.casefold):
            color = colors[prop]
            lines.append(
                f"    - {prop}: {{r: {number(color.get('r', 0))}, "
                f"g: {number(color.get('g', 0))}, b: {number(color.get('b', 0))}, "
                f"a: {number(color.get('a', 0))}}}"
            )
    else:
        lines.append("    m_Colors: []")
    return "\n".join(lines) + "\n"


def replace_keywords(text: str, keywords: list[str]) -> str:
    if keywords:
        replacement = "  m_ValidKeywords:\n" + "".join(f"  - {item}\n" for item in sorted(set(keywords)))
    else:
        replacement = "  m_ValidKeywords: []\n"
    return re.sub(
        r"^  m_ValidKeywords:.*?(?=^  m_InvalidKeywords:)",
        replacement,
        text,
        count=1,
        flags=re.MULTILINE | re.DOTALL,
    )


def replace_material_name(text: str, material_name: str) -> str:
    marker = "--- !u!21 &2100000\nMaterial:\n"
    marker_index = text.find(marker)
    if marker_index < 0:
        raise ValueError("Template has no primary Material object")
    material_start = marker_index + len(marker)
    prefix = text[:material_start]
    material_body = text[material_start:]
    material_body, count = re.subn(
        r"^  m_Name:.*$",
        f"  m_Name: {yaml_string(material_name)}",
        material_body,
        count=1,
        flags=re.MULTILINE,
    )
    if count != 1:
        raise ValueError(f"Could not replace Material.m_Name for {material_name}")
    return prefix + material_body


def render_material(
    item: dict[str, Any], template_text: str, defaults: dict[str, OrderedDict[str, Any]], guid_cache: dict[str, str]
) -> tuple[str, dict[str, Any], list[dict[str, Any]]]:
    shader = item["fallbackShader"]
    textures: OrderedDict[str, Any] = OrderedDict(
        (key, {"textureRef": value["textureRef"], "scale": dict(value["scale"]), "offset": dict(value["offset"])})
        for key, value in defaults["textures"].items()
    )
    ints: OrderedDict[str, float] = OrderedDict(defaults["ints"])
    floats: OrderedDict[str, float] = OrderedDict(defaults["floats"])
    colors: OrderedDict[str, Any] = OrderedDict((key, dict(value)) for key, value in defaults["colors"].items())

    floats.update(item.get("floats", {}))
    ints.update(item.get("ints", {}))
    colors.update((key, dict(value)) for key, value in item.get("colors", {}).items())

    normalized_non_finite = 0
    for prop, value in list(floats.items()):
        if not math.isfinite(float(value)):
            floats[prop] = defaults["floats"].get(prop, 0.0)
            normalized_non_finite += 1
    for prop, value in list(ints.items()):
        if not math.isfinite(float(value)):
            ints[prop] = defaults["ints"].get(prop, 0.0)
            normalized_non_finite += 1
    for prop, color in colors.items():
        template_color = defaults["colors"].get(prop, {})
        for channel in ("r", "g", "b", "a"):
            if not math.isfinite(float(color.get(channel, 0.0))):
                color[channel] = template_color.get(channel, 0.0)
                normalized_non_finite += 1

    if "_Smoothness" not in item.get("floats", {}):
        if "_Glossiness" in item.get("floats", {}):
            floats["_Smoothness"] = item["floats"]["_Glossiness"]
        elif "_GlossMapScale" in item.get("floats", {}):
            floats["_Smoothness"] = item["floats"]["_GlossMapScale"]

    if "_BaseColor" not in item.get("colors", {}) and "_Color" in item.get("colors", {}):
        colors["_BaseColor"] = dict(item["colors"]["_Color"])
    if "_Color" not in item.get("colors", {}) and "_BaseColor" in item.get("colors", {}):
        colors["_Color"] = dict(item["colors"]["_BaseColor"])

    for binding in item.get("textures", []):
        guid = texture_guid(binding["assetPath"], guid_cache)
        textures[binding["sourceProperty"]] = {
            "textureRef": f"{{fileID: 2800000, guid: {guid}, type: 3}}",
            "scale": dict(binding.get("scale", {"x": 1.0, "y": 1.0})),
            "offset": dict(binding.get("offset", {"x": 0.0, "y": 0.0})),
        }

    unresolved_rows = []
    for unresolved in item.get("unresolvedTextures", []):
        prop = unresolved["sourceProperty"]
        textures.setdefault(
            prop,
            {
                "textureRef": "{fileID: 0}",
                "scale": {"x": 1.0, "y": 1.0},
                "offset": {"x": 0.0, "y": 0.0},
            },
        )
        unresolved_rows.append(
            {
                "materialName": item["materialName"],
                "targetAssetPath": item["targetAssetPath"],
                "property": prop,
                "sourceFileId": unresolved.get("sourceFileId", ""),
                "sourcePathId": unresolved.get("sourcePathId", ""),
                "referenceName": unresolved.get("referenceName", ""),
                "reason": unresolved.get("reason", ""),
            }
        )

    aliases: list[str] = []
    albedo = first_texture(
        textures,
        ("_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_AlbedoMap", "_Diffuse", "_DiffuseMap", "_Texture"),
        ("albedo", "diffuse", "basecolor", "maintex"),
    )
    if shader in ("UI/Default", "Sprites/Default"):
        aliases += copy_texture_alias(textures, albedo, ("_MainTex",))
    else:
        aliases += copy_texture_alias(textures, albedo, ("_BaseMap", "_MainTex"))

    normal = first_texture(textures, ("_BumpMap", "_NormalMap", "_Normal"), ("normal", "bump"))
    emission = first_texture(textures, ("_EmissionMap", "_EmissiveMap"), ("emission", "emissive"))
    if shader not in ("UI/Default", "Sprites/Default"):
        aliases += copy_texture_alias(textures, normal, ("_BumpMap",))
        aliases += copy_texture_alias(textures, emission, ("_EmissionMap",))
    if shader == "Universal Render Pipeline/Lit":
        metallic = first_texture(textures, ("_MetallicGlossMap", "_MetallicMap"), ("metallic",))
        occlusion = first_texture(textures, ("_OcclusionMap", "_AOMap"), ("occlusion",))
        aliases += copy_texture_alias(textures, metallic, ("_MetallicGlossMap",))
        aliases += copy_texture_alias(textures, occlusion, ("_OcclusionMap",))

    alpha_clip = float(floats.get("_AlphaClip", 0)) >= 0.5
    transparent = float(floats.get("_Surface", 0)) >= 0.5
    custom_queue = 3000 if transparent else 2450 if alpha_clip else -1
    render_type = "Transparent" if transparent else "TransparentCutout" if alpha_clip else "Opaque"

    keywords = []
    if is_bound(textures, "_BumpMap"):
        keywords.append("_NORMALMAP")
    emission_color = colors.get("_EmissionColor", {})
    emission_active = is_bound(textures, "_EmissionMap") or any(
        float(emission_color.get(channel, 0)) > 0.000001 for channel in ("r", "g", "b")
    )
    if emission_active:
        keywords.append("_EMISSION")
    if shader == "Universal Render Pipeline/Lit" and is_bound(textures, "_MetallicGlossMap"):
        keywords.append("_METALLICSPECGLOSSMAP")
    if shader == "Universal Render Pipeline/Lit" and float(floats.get("_WorkflowMode", 1)) < 0.5:
        keywords.append("_SPECULAR_SETUP")
    if alpha_clip:
        keywords.append("_ALPHATEST_ON")
    if transparent:
        keywords.append("_SURFACE_TYPE_TRANSPARENT")
        blend_mode = int(round(float(floats.get("_Blend", 0))))
        if blend_mode == 1:
            keywords.append("_ALPHAPREMULTIPLY_ON")
        elif blend_mode == 3:
            keywords.append("_ALPHAMODULATE_ON")

    saved = build_saved_properties(textures, ints, floats, colors)
    output = replace_material_name(template_text, item["materialName"])
    output = replace_keywords(output, keywords)
    output = re.sub(r"^  m_LightmapFlags:.*$", f"  m_LightmapFlags: {2 if emission_active else 4}", output, count=1, flags=re.MULTILINE)
    output = re.sub(r"^  m_CustomRenderQueue:.*$", f"  m_CustomRenderQueue: {custom_queue}", output, count=1, flags=re.MULTILINE)
    output = re.sub(
        r"^  stringTagMap:.*?(?=^  disabledShaderPasses:)",
        f"  stringTagMap:\n    RenderType: {render_type}\n",
        output,
        count=1,
        flags=re.MULTILINE | re.DOTALL,
    )
    output, replaced = SAVED_PROPERTIES_RE.subn(saved, output, count=1)
    if replaced != 1:
        raise ValueError(f"Could not replace saved properties for {item['materialName']}")
    if not output.endswith("\n"):
        output += "\n"

    result = {
        "materialName": item["materialName"],
        "targetAssetPath": item["targetAssetPath"],
        "shader": shader,
        "shaderResolution": item.get("shaderResolution", "fallback-only"),
        "fallbackReason": item.get("fallbackReason", ""),
        "recoveredTextureCount": len(item.get("textures", [])),
        "unresolvedTextureCount": len(item.get("unresolvedTextures", [])),
        "aliasesAdded": aliases,
        "keywordCount": len(set(keywords)),
        "customRenderQueue": custom_queue,
        "normalizedNonFiniteValueCount": normalized_non_finite,
        "status": "planned",
    }
    return output, result, unresolved_rows


def write_reports(results: list[dict[str, Any]], unresolved: list[dict[str, Any]], summary: dict[str, Any]) -> None:
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    json_path = REPORT_DIR / "material_rebuild_result.json"
    csv_path = REPORT_DIR / "material_rebuild_results.csv"
    unresolved_path = REPORT_DIR / "unresolved_texture_bindings.csv"

    json_path.write_text(
        json.dumps({"schemaVersion": 1, "summary": summary, "materials": results}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    with csv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        fields = [
            "materialName", "targetAssetPath", "shader", "shaderResolution", "fallbackReason",
            "recoveredTextureCount", "unresolvedTextureCount", "aliasesAdded", "keywordCount",
            "customRenderQueue", "normalizedNonFiniteValueCount", "status",
        ]
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        for row in results:
            serial = dict(row)
            serial["aliasesAdded"] = ";".join(row["aliasesAdded"])
            writer.writerow(serial)

    with unresolved_path.open("w", encoding="utf-8-sig", newline="") as stream:
        fields = ["materialName", "targetAssetPath", "property", "sourceFileId", "sourcePathId", "referenceName", "reason"]
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(unresolved)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="Write the planned .mat files and result reports")
    parser.add_argument(
        "--replace-existing-planned",
        action="store_true",
        help="Replace only the exact targets in the rebuild plan (used to repair a prior generated batch)",
    )
    args = parser.parse_args()

    if not PLAN_PATH.is_file():
        raise FileNotFoundError(PLAN_PATH)
    plan = json.loads(PLAN_PATH.read_text(encoding="utf-8"))
    materials = plan.get("materials", [])
    if len(materials) != 686:
        raise ValueError(f"Expected 686 planned materials, found {len(materials)}")

    template_texts: dict[str, str] = {}
    template_defaults: dict[str, dict[str, OrderedDict[str, Any]]] = {}
    for shader, path in TEMPLATE_BY_SHADER.items():
        if not path.is_file():
            raise FileNotFoundError(f"Unity-authored material template missing: {path}")
        text = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
        if "--- !u!21 &2100000" not in text or "  m_Shader:" not in text:
            raise ValueError(f"Invalid Unity Material template: {path}")
        template_texts[shader] = text
        template_defaults[shader] = parse_template_defaults(text)

    seen: set[str] = set()
    existing: list[str] = []
    prepared: list[tuple[Path, str, dict[str, Any]]] = []
    results: list[dict[str, Any]] = []
    unresolved_rows: list[dict[str, Any]] = []
    guid_cache: dict[str, str] = {}

    for item in materials:
        shader = item["fallbackShader"]
        if shader not in template_texts:
            raise ValueError(f"No template configured for shader: {shader}")
        target = (PROJECT_ROOT / Path(item["targetAssetPath"])).resolve()
        require_within(target, MATERIAL_ROOT, "Material target")
        normalized = str(target).casefold()
        if normalized in seen:
            raise ValueError(f"Duplicate material target: {target}")
        seen.add(normalized)
        if not target.parent.is_dir():
            raise FileNotFoundError(f"Material target parent does not exist: {target.parent}")
        if target.exists():
            existing.append(item["targetAssetPath"])
        output, result, unresolved = render_material(
            item, template_texts[shader], template_defaults[shader], guid_cache
        )
        prepared.append((target, output, result))
        results.append(result)
        unresolved_rows.extend(unresolved)

    if existing and not args.replace_existing_planned:
        preview = "\n".join(existing[:20])
        raise FileExistsError(f"Refusing to overwrite {len(existing)} existing material targets:\n{preview}")

    if existing and args.replace_existing_planned:
        for item in materials:
            target = (PROJECT_ROOT / Path(item["targetAssetPath"])).resolve()
            if not target.exists():
                continue
            current = target.read_text(encoding="utf-8-sig")
            template = template_texts[item["fallbackShader"]]
            expected_shader = re.search(r"^  m_Shader:.*$", template, re.MULTILINE)
            if not expected_shader or expected_shader.group(0) not in current:
                raise ValueError(f"Existing planned target does not match its generated shader template: {target}")

    resolved_count = sum(row["recoveredTextureCount"] for row in results)
    unresolved_count = sum(row["unresolvedTextureCount"] for row in results)
    summary = {
        "mode": "write" if args.write else "dry-run",
        "materialCount": len(results),
        "resolvedTextureBindingCount": resolved_count,
        "unresolvedTextureBindingCount": unresolved_count,
        "uniqueTextureAssetCount": len(guid_cache),
        "normalizedNonFiniteValueCount": sum(row["normalizedNonFiniteValueCount"] for row in results),
        "existingTargetCount": len(existing),
        "shaderCounts": dict(sorted((shader, sum(row["shader"] == shader for row in results)) for shader in TEMPLATE_BY_SHADER)),
        "allTargetsWithinMaterialRoot": True,
    }

    if args.write:
        for target, output, result in prepared:
            temporary = target.with_name(target.name + ".tmp-material-rebuild")
            temporary.write_text(output, encoding="utf-8", newline="\n")
            os.replace(temporary, target)
            result["status"] = "written"
        write_reports(results, unresolved_rows, summary)

    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise
