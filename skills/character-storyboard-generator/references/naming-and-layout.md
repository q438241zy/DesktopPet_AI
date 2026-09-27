# Naming and folder layout — V1.0

## Canonical identifiers

- `character_id`: lowercase ASCII kebab-case, for example `xiaomai` or `lin-chen`.
- Pose ID: fixed `P01`–`P30`; never renumber existing entries within V1.0.
- Expression ID: fixed `E01`–`E40`; never renumber existing entries within V1.0.
- Variant: two digits starting at `01`.
- Version token: exactly `v1.0`.

## Canonical filename

```text
{character_id}__{pose_id}-{pose_slug}__{expression_id}-{expression_slug}__v{variant:02d}__v1.0.png
```

Example:

```text
xiaomai__P06-wave-goodbye__E01-joy__v01__v1.0.png
```

## Canonical tree

```text
<project>/
├─ source/                         # untouched source images
├─ identity/                       # approved cutout and identity/chibi sheet
├─ manifests/                      # tasks.jsonl, tasks.csv, audit.json
├─ 成果/
│  ├─ 喜-开心/
│  ├─ 怒-愤怒/
│  └─ 乐-噗呲一笑/
├─ _rejected/                      # failed attempts; never canonical
└─ project.json
```

Canonical relative output path:

```text
成果/{emotion_category_zh}-{expression_label_zh}/{filename}
```

The user sees only two levels: `成果` and one combined emotion-expression folder. All poses for the same expression stay together. Do not pre-create empty folders. IDs and slugs in the filename are authoritative.

## Manifest status values

Use only `planned`, `generating`, `accepted`, `rejected`, `skipped-safety`, or `missing`. Never infer `accepted` merely from a filename; validate the file first.

## Versioning

- Keep V1.0 IDs stable.
- Use a higher `variant` for a regenerated visual that follows the same V1.0 specification.
- Use a new skill/spec version only when pose meaning, expression meaning, naming, or required output contract changes.
