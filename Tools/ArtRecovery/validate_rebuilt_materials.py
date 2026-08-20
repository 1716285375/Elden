#!/usr/bin/env python3
"""Validate rebuilt Material YAML against the recovery plan and texture GUIDs."""

from __future__ import annotations

import json
import re
import sys
from collections import Counter
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
PLAN = ROOT / "Docs/ArtRecovery/Nephilite/MaterialRebuild/material_rebuild_plan.json"
REPORT = ROOT / "Docs/ArtRecovery/Nephilite/MaterialRebuild/material_rebuild_validation.json"
MATERIAL_ROOT = ROOT / "Assets/Art/Materials"
TEMPLATE_ROOT = ROOT / "Assets/Editor/ArtRecovery/MaterialTemplates"
TEMPLATES = {
    "Universal Render Pipeline/Lit": TEMPLATE_ROOT / "Template_URP_Lit.mat",
    "Universal Render Pipeline/Unlit": TEMPLATE_ROOT / "Template_URP_Unlit.mat",
    "Universal Render Pipeline/Particles/Unlit": TEMPLATE_ROOT / "Template_URP_Particles_Unlit.mat",
    "UI/Default": TEMPLATE_ROOT / "Template_UI_Default.mat",
    "Sprites/Default": TEMPLATE_ROOT / "Template_Sprites_Default.mat",
}
MATERIAL_MARKER = "--- !u!21 &2100000\nMaterial:\n"
GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)


def material_body(text: str) -> str:
    normalized = text.replace("\r\n", "\n")
    if MATERIAL_MARKER not in normalized:
        raise ValueError("missing primary Material object")
    return normalized.split(MATERIAL_MARKER, 1)[1]


def field(body: str, name: str) -> str:
    match = re.search(rf"^  {re.escape(name)}:\s*(.*)$", body, re.MULTILINE)
    if not match:
        raise ValueError(f"missing Material.{name}")
    return match.group(1).strip()


def decoded_name(raw: str) -> str:
    if raw.startswith('"'):
        return json.loads(raw)
    return raw


def meta_guid(asset_path: str) -> str:
    meta = ROOT / f"{asset_path}.meta"
    match = GUID_RE.search(meta.read_text(encoding="utf-8-sig"))
    if not match:
        raise ValueError(f"missing GUID: {meta}")
    return match.group(1).lower()


def texture_reference(body: str, prop: str) -> str | None:
    match = re.search(
        rf"^    - {re.escape(prop)}:\n        m_Texture:\s*(\{{[^\n]+\}})",
        body,
        re.MULTILINE,
    )
    return match.group(1) if match else None


def main() -> int:
    plan = json.loads(PLAN.read_text(encoding="utf-8"))
    materials = plan["materials"]
    expected_shaders = {
        shader: field(material_body(path.read_text(encoding="utf-8-sig")), "m_Shader")
        for shader, path in TEMPLATES.items()
    }

    failures: list[dict[str, Any]] = []
    shader_counts: Counter[str] = Counter()
    checked_texture_bindings = 0
    checked_unresolved_bindings = 0
    imported_meta_count = 0
    serialized_name_count = 0

    def fail(item: dict[str, Any], check: str, detail: str) -> None:
        failures.append(
            {
                "targetAssetPath": item.get("targetAssetPath", ""),
                "materialName": item.get("materialName", ""),
                "check": check,
                "detail": detail,
            }
        )

    for item in materials:
        target = ROOT / item["targetAssetPath"]
        if not target.is_file():
            fail(item, "target-exists", "material file missing")
            continue
        if Path(str(target) + ".meta").is_file():
            imported_meta_count += 1
        else:
            fail(item, "unity-import", ".mat.meta missing after AssetDatabase refresh")

        try:
            body = material_body(target.read_text(encoding="utf-8-sig"))
            actual_name = decoded_name(field(body, "m_Name"))
            if actual_name == item["materialName"]:
                serialized_name_count += 1
            else:
                fail(item, "material-name", f"expected {item['materialName']!r}, found {actual_name!r}")

            shader = item["fallbackShader"]
            shader_counts[shader] += 1
            actual_shader = field(body, "m_Shader")
            if actual_shader != expected_shaders[shader]:
                fail(item, "shader-reference", f"expected {expected_shaders[shader]}, found {actual_shader}")

            for binding in item.get("textures", []):
                checked_texture_bindings += 1
                reference = texture_reference(body, binding["sourceProperty"])
                expected_guid = meta_guid(binding["assetPath"])
                expected = f"{{fileID: 2800000, guid: {expected_guid}, type: 3}}"
                if reference != expected:
                    fail(
                        item,
                        "resolved-texture",
                        f"{binding['sourceProperty']}: expected {expected}, found {reference}",
                    )

            for binding in item.get("unresolvedTextures", []):
                checked_unresolved_bindings += 1
                reference = texture_reference(body, binding["sourceProperty"])
                if reference != "{fileID: 0}":
                    fail(
                        item,
                        "unresolved-texture",
                        f"{binding['sourceProperty']}: expected empty reference, found {reference}",
                    )
        except Exception as exc:
            fail(item, "parse", str(exc))

    material_files = list(MATERIAL_ROOT.rglob("*.mat"))
    legacy_path_tokens = ("nephilite", "sharedassets", "__pathid_")
    legacy_paths = [
        path.relative_to(ROOT).as_posix()
        for path in material_files
        if any(token in path.as_posix().lower() for token in legacy_path_tokens)
    ]
    summary = {
        "success": not failures,
        "plannedMaterialCount": len(materials),
        "materialFilesUnderArtCount": len(material_files),
        "importedMetaCount": imported_meta_count,
        "matchingSerializedNameCount": serialized_name_count,
        "matchingShaderReferenceCount": len(materials)
        - sum(row["check"] == "shader-reference" for row in failures),
        "checkedResolvedTextureBindingCount": checked_texture_bindings,
        "checkedUnresolvedTextureBindingCount": checked_unresolved_bindings,
        "legacyPathCount": len(legacy_paths),
        "shaderCounts": dict(sorted(shader_counts.items())),
        "failureCount": len(failures),
    }
    REPORT.write_text(
        json.dumps(
            {"schemaVersion": 1, "summary": summary, "legacyPaths": legacy_paths, "failures": failures},
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if not failures else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise
