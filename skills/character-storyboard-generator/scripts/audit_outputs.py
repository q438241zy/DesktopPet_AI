#!/usr/bin/env python3
"""Audit generated PNG files against a V1.0 JSONL manifest."""

from __future__ import annotations

import argparse
import json
import struct
from pathlib import Path


def png_meta(path: Path):
    with path.open("rb") as handle:
        data = handle.read()
    header = data[:29]
    if len(header) < 29 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise ValueError("not a valid PNG header")
    width, height = struct.unpack(">II", header[16:24])
    color_type = header[25]
    return width, height, color_type in {4, 6} or b"tRNS" in data


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("project", type=Path)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--only-existing", action="store_true", help="Validate generated files without treating ungenerated planned tasks as errors")
    args = parser.parse_args()
    root = args.project.resolve().parent
    manifest = args.manifest.resolve() if args.manifest else root / "manifests" / "tasks.jsonl"
    problems = []
    checked = 0
    existing_checked = 0
    missing_skipped = 0
    with manifest.open("r", encoding="utf-8") as handle:
        for line in handle:
            if not line.strip():
                continue
            row = json.loads(line)
            checked += 1
            path = root / row["relative_output"]
            if not path.exists():
                if args.only_existing:
                    missing_skipped += 1
                    continue
                problems.append({"task_id": row["task_id"], "issue": "missing", "path": str(path)})
                continue
            try:
                existing_checked += 1
                width, height, alpha = png_meta(path)
                if row.get("canvas_mode", "fixed") == "fixed" and (width, height) != (row["width"], row["height"]):
                    problems.append({"task_id": row["task_id"], "issue": "wrong-size", "actual": [width, height]})
                if row.get("canvas_mode") == "adaptive" and min(width, height) < 256:
                    problems.append({"task_id": row["task_id"], "issue": "adaptive-canvas-too-small", "actual": [width, height]})
                if row.get("transparent") and not alpha:
                    problems.append({"task_id": row["task_id"], "issue": "missing-alpha-channel"})
            except Exception as exc:
                problems.append({"task_id": row["task_id"], "issue": "invalid-png", "detail": str(exc)})
    report = {"spec_version": "v1.0", "checked_manifest_rows": checked, "existing_files_checked": existing_checked, "missing_planned_skipped": missing_skipped, "problem_count": len(problems), "problems": problems}
    output = root / "manifests" / "audit.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))
    raise SystemExit(1 if problems else 0)


if __name__ == "__main__":
    main()
