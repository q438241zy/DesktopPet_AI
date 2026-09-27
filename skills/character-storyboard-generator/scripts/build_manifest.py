#!/usr/bin/env python3
"""Build deterministic V1.0 pose-by-expression manifests."""

from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path

SLUG_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
STATUSES = {"planned", "generating", "accepted", "rejected", "skipped-safety", "missing"}


def load_json(path: Path):
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def select(items, requested, kind):
    by_id = {item["id"]: item for item in items}
    if requested == "all":
        return items
    if not isinstance(requested, list) or not requested:
        raise ValueError(f"selection.{kind}_ids must be 'all' or a non-empty list")
    unknown = [item_id for item_id in requested if item_id not in by_id]
    if unknown:
        raise ValueError(f"Unknown {kind} IDs: {', '.join(unknown)}")
    return [by_id[item_id] for item_id in requested]


def validate_catalog(items, prefix):
    seen = set()
    for item in items:
        required = {"id", "slug", "label_zh", "group", "priority", "prompt"}
        if prefix == "E":
            required.add("category_zh")
        missing = required - item.keys()
        if missing:
            raise ValueError(f"{item.get('id', '?')} missing: {sorted(missing)}")
        if not re.fullmatch(rf"{prefix}\d{{2}}", item["id"]):
            raise ValueError(f"Invalid ID: {item['id']}")
        if item["id"] in seen or not SLUG_RE.fullmatch(item["slug"]):
            raise ValueError(f"Duplicate ID or invalid slug: {item}")
        seen.add(item["id"])


STYLE_LOCK = (
    "Art style: Japanese chibi anime style (in the general vein of Himouto! Umaru-chan): "
    "2-3 heads tall, oversized head, large simple expressive eyes, tiny simplified hands and feet, "
    "clean thin line art, flat cel-shaded colors with minimal soft shading, rounded cute silhouette. "
    "Reference ONLY the single approved identity image; do not blend styles from any other image."
)


def prompt_for(project, pose, expression):
    medium = (
        "Preserve the exact animation character design from the approved identity reference."
        if project["input_type"] == "animation"
        else "Use the approved chibi identity derived from the photographed person."
    )
    alpha = (
        "Prepare for transparent PNG delivery using a perfectly flat removable chroma-key background."
        if project["canvas"].get("transparent", True)
        else "Use a plain clean background with no scene details."
    )
    minor_lock = (
        "Minor-safety lock: age-appropriate nonsexualized proportions and clothing; no seductive styling, revealing clothing, body emphasis, fetish elements, provocative angle, or suggestive framing."
        if project["adult_status"] != "adult"
        else "Keep the result tasteful, fully clothed, and non-explicit."
    )
    return " ".join([
        medium,
        STYLE_LOCK,
        f"Pose: {pose['prompt']}.",
        f"Expression: {expression['prompt']}.",
        "Exactly one character, full body visible, generous padding, readable silhouette.",
        "Keep face, hairstyle, outfit, palette, accessories, and proportions identical to the approved reference.",
        "No captions, speech bubbles, props, extra limbs, watermark, logo, signature, or background scene.",
        "Completely watermark-free output even if the source carried one.",
        minor_lock,
        alpha,
    ])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("project", type=Path)
    parser.add_argument("--skill-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--create-output-dirs", action="store_true")
    args = parser.parse_args()

    project_path = args.project.resolve()
    root = project_path.parent
    project = load_json(project_path)
    for key in ("spec_version", "character_id", "character_name", "input_type", "adult_status", "canvas", "variant_count", "selection"):
        if key not in project:
            raise ValueError(f"Missing project field: {key}")
    if project["spec_version"] != "v1.0":
        raise ValueError("This skill version only supports spec_version v1.0")
    if not SLUG_RE.fullmatch(project["character_id"]):
        raise ValueError("character_id must be lowercase ASCII kebab-case")
    if project["input_type"] not in {"animation", "photo", "auto"}:
        raise ValueError("input_type must be animation, photo, or auto")
    if project["input_type"] == "auto":
        raise ValueError("Inspect the source and resolve input_type to animation or photo before building a manifest")
    if project["adult_status"] not in {"adult", "minor", "unknown"}:
        raise ValueError("adult_status must be adult, minor, or unknown")
    if not isinstance(project["variant_count"], int) or not 1 <= project["variant_count"] <= 99:
        raise ValueError("variant_count must be an integer from 1 to 99")
    width, height = project["canvas"].get("width"), project["canvas"].get("height")
    canvas_mode = project["canvas"].get("mode", "fixed")
    if canvas_mode not in {"fixed", "adaptive"}:
        raise ValueError("canvas.mode must be fixed or adaptive")
    if not isinstance(width, int) or not isinstance(height, int) or width < 256 or height < 256:
        raise ValueError("canvas width and height must be integers of at least 256")
    for source in project.get("source_images", []):
        if not (root / source).is_file():
            raise ValueError(f"Source image not found: {source}")
    identity_reference = project.get("identity_reference")
    if identity_reference and not (root / identity_reference).is_file():
        raise ValueError(f"Identity reference not found: {identity_reference}")

    refs = args.skill_root / "references"
    poses = load_json(refs / "poses.json")
    expressions = load_json(refs / "expressions.json")
    validate_catalog(poses, "P")
    validate_catalog(expressions, "E")
    poses = select(poses, project["selection"].get("pose_ids", "all"), "pose")
    expressions = select(expressions, project["selection"].get("expression_ids", "all"), "expression")

    rows = []
    skipped = []
    for pose in poses:
        for expression in expressions:
            if expression.get("requires_adult") and project["adult_status"] != "adult":
                skipped.append({"pose_id": pose["id"], "expression_id": expression["id"], "status": "skipped-safety"})
                continue
            for variant in range(1, project["variant_count"] + 1):
                filename = (
                    f"{project['character_id']}__{pose['id']}-{pose['slug']}__"
                    f"{expression['id']}-{expression['slug']}__v{variant:02d}__v1.0.png"
                )
                relative = Path("成果") / f"{expression['category_zh']}-{expression['label_zh']}" / filename
                task_id = f"{project['character_id']}:{pose['id']}:{expression['id']}:v{variant:02d}:v1.0"
                rows.append({
                    "task_id": task_id,
                    "spec_version": "v1.0",
                    "character_id": project["character_id"],
                    "source_reference": project.get("identity_reference", project.get("source_images", [""])[0]),
                    "pose_id": pose["id"], "pose_slug": pose["slug"], "pose_label_zh": pose["label_zh"],
                    "expression_id": expression["id"], "expression_slug": expression["slug"], "expression_label_zh": expression["label_zh"],
                    "variant": variant, "status": "planned", "relative_output": relative.as_posix(),
                    "width": project["canvas"]["width"], "height": project["canvas"]["height"],
                    "canvas_mode": canvas_mode,
                    "transparent": bool(project["canvas"].get("transparent", True)),
                    "prompt": prompt_for(project, pose, expression),
                })
                if args.create_output_dirs:
                    (root / relative).parent.mkdir(parents=True, exist_ok=True)

    manifest_dir = root / "manifests"
    manifest_dir.mkdir(parents=True, exist_ok=True)
    jsonl_path = manifest_dir / "tasks.jsonl"
    csv_path = manifest_dir / "tasks.csv"
    with jsonl_path.open("w", encoding="utf-8", newline="\n") as handle:
        for row in rows:
            handle.write(json.dumps(row, ensure_ascii=False) + "\n")
    with csv_path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0].keys()) if rows else ["task_id"])
        writer.writeheader()
        writer.writerows(rows)
    summary = {
        "spec_version": "v1.0", "task_count": len(rows), "skipped_safety_count": len(skipped),
        "pose_count": len(poses), "expression_count_selected": len(expressions),
        "variant_count": project["variant_count"], "statuses": sorted(STATUSES), "skipped": skipped,
    }
    (manifest_dir / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
