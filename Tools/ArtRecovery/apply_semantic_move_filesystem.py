#!/usr/bin/env python3
"""Apply a prepared semantic move plan while preserving Unity .meta GUIDs.

This is a fallback for cases where an editor-side batch extension cannot handle
the volume safely. Source and destination are required to be on the same volume;
each asset and its .meta file are moved as a pair without overwriting targets.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import re
from pathlib import Path
from typing import Any


OLD_ROOTS = (
    "Assets/Art/Animations/Nephilite",
    "Assets/Art/Audio/Nephilite",
    "Assets/Art/Fonts/Nephilite",
    "Assets/Art/Materials/RecoveredJson/Nephilite",
    "Assets/Art/Models/Nephilite",
    "Assets/Art/Sprites/Nephilite",
    "Assets/Art/Textures/Nephilite",
)


def read_guid(asset_path: Path) -> str:
    meta_path = Path(f"{asset_path}.meta")
    if not meta_path.exists():
        return ""
    match = re.search(
        r"^guid:\s*([0-9a-fA-F]+)\s*$",
        meta_path.read_text(encoding="utf-8-sig", errors="replace"),
        re.MULTILINE,
    )
    return match.group(1).lower() if match else ""


def load_plan(plan_path: Path) -> list[dict[str, str]]:
    with plan_path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def classify(project_root: Path, rows: list[dict[str, str]]) -> tuple[list[dict[str, str]], int, list[str]]:
    pending: list[dict[str, str]] = []
    complete = 0
    invalid: list[str] = []
    for row in rows:
        source = project_root / row["source_asset_path"]
        destination = project_root / row["destination_asset_path"]
        source_meta = Path(f"{source}.meta")
        destination_meta = Path(f"{destination}.meta")
        state = (source.exists(), source_meta.exists(), destination.exists(), destination_meta.exists())
        if state == (True, True, False, False):
            pending.append(row)
        elif state == (False, False, True, True):
            complete += 1
        else:
            invalid.append(f"{state}: {row['source_asset_path']} -> {row['destination_asset_path']}")
    return pending, complete, invalid


def move_pair(project_root: Path, row: dict[str, str]) -> None:
    source = project_root / row["source_asset_path"]
    destination = project_root / row["destination_asset_path"]
    source_meta = Path(f"{source}.meta")
    destination_meta = Path(f"{destination}.meta")
    destination.parent.mkdir(parents=True, exist_ok=True)
    if source.drive.casefold() != destination.drive.casefold():
        raise RuntimeError(f"Cross-volume move is forbidden: {source} -> {destination}")
    if destination.exists() or destination_meta.exists():
        raise FileExistsError(destination)
    os.replace(source, destination)
    try:
        os.replace(source_meta, destination_meta)
    except Exception:
        os.replace(destination, source)
        raise


def validate(project_root: Path, rows: list[dict[str, str]]) -> dict[str, Any]:
    missing = 0
    source_remaining = 0
    size_mismatches = 0
    guid_mismatches = 0
    for row in rows:
        source = project_root / row["source_asset_path"]
        destination = project_root / row["destination_asset_path"]
        if source.exists() or Path(f"{source}.meta").exists():
            source_remaining += 1
        if not destination.exists() or not Path(f"{destination}.meta").exists():
            missing += 1
            continue
        if destination.stat().st_size != int(row["bytes"]):
            size_mismatches += 1
        if read_guid(destination) != row["source_guid"]:
            guid_mismatches += 1
    return {
        "rows": len(rows),
        "missing": missing,
        "source_remaining": source_remaining,
        "size_mismatches": size_mismatches,
        "guid_mismatches": guid_mismatches,
    }


def remove_recovery_tree(project_root: Path, asset_path: str) -> int:
    root = (project_root / asset_path).resolve()
    art_root = (project_root / "Assets" / "Art").resolve()
    if art_root not in root.parents:
        raise RuntimeError(f"Unsafe cleanup root: {root}")
    if not root.exists():
        return 0
    directories = [root, *(path for path in root.rglob("*") if path.is_dir())]
    folder_meta = {Path(f"{directory}.meta").resolve() for directory in directories}
    unexpected = [path for path in root.rglob("*") if path.is_file() and path.resolve() not in folder_meta]
    if unexpected:
        raise RuntimeError(f"Recovery folder is not empty: {root}; unexpected={unexpected[:5]}")
    removed = 0
    for meta_path in folder_meta:
        if meta_path.exists():
            meta_path.unlink()
            removed += 1
    for directory in sorted(directories, key=lambda item: len(item.parts), reverse=True):
        directory.rmdir()
        removed += 1
    return removed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--plan", type=Path)
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--cleanup-old-folders", action="store_true")
    args = parser.parse_args()
    project_root = args.project_root.resolve()
    plan_path = (args.plan or project_root / "Docs" / "ArtRecovery" / "Nephilite" / "semantic_move_plan.csv").resolve()
    rows = load_plan(plan_path)
    pending, complete, invalid = classify(project_root, rows)
    if invalid:
        raise RuntimeError(f"Invalid move states ({len(invalid)}): {invalid[:10]}")
    result: dict[str, Any] = {
        "mode": "execute" if args.execute else "dry-run",
        "total": len(rows),
        "already_complete": complete,
        "pending": len(pending),
    }
    if args.execute:
        for index, row in enumerate(pending, 1):
            move_pair(project_root, row)
            if index % 500 == 0 or index == len(pending):
                print(f"Moved {complete + index}/{len(rows)}", flush=True)
        validation = validate(project_root, rows)
        result["validation"] = validation
        if any(validation[key] for key in ("missing", "source_remaining", "size_mismatches", "guid_mismatches")):
            raise RuntimeError(f"Post-move validation failed: {validation}")
        if args.cleanup_old_folders:
            removed = sum(remove_recovery_tree(project_root, path) for path in OLD_ROOTS)
            removed += remove_recovery_tree(project_root, "Assets/Art/Materials/RecoveredJson")
            removed += remove_recovery_tree(project_root, "Assets/Art/Sprites")
            result["old_folder_entries_removed"] = removed
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
