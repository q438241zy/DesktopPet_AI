"""Refresh unchanged native export frames with the current HTML and membership rules."""
import argparse
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
parser.add_argument("--membership-rules", required=True, type=Path)
parser.add_argument("--coverage", type=Path, help="Current native --export-coverage directory; preserve old frame images.")
args = parser.parse_args()
html = args.source.read_text(encoding="utf-8")
match = re.search(r'<script id="data" type="application/json">(.*?)</script>', html, re.S)
if not match:
    raise SystemExit("Native Demo payload missing.")
payload = json.loads(match.group(1))
version_props = (root / "Version.props").read_text(encoding="utf-8")
version = '.'.join(re.search(rf"<DesktopPet{part}>(.*?)</DesktopPet{part}>", version_props).group(1) for part in ['Major','Minor'])
payload["renderVersion"] = payload.get("renderVersion", payload["version"])
payload["version"] = version
payload["membership"] = json.loads(args.membership_rules.read_text(encoding="utf-8"))
if args.coverage:
    payload['coverage'] = json.loads((args.coverage/'action-coverage.json').read_text(encoding='utf-8-sig'))
    payload['actions'] = json.loads((args.coverage/'actions.json').read_text(encoding='utf-8-sig'))
    # New flows run in CompanionV01; keep their old recordings out of the menu.
    payload['interactiveUpdates'] = ['gift','read','butterfly']
    for actions in payload['clips'].values():
        actions.pop('photo',None)
old_suffix = "；会员：黄金及以上开放"
suffix = "；会员：测试期间全部开放" if payload["membership"].get("testingOpen") else old_suffix
for row in payload["coverage"]:
    if row["action"] == "chat":
        row["detail"] = row["detail"].removesuffix(old_suffix).removesuffix("；会员：测试期间全部开放") + suffix
for actions in payload["clips"].values():
    if "chat" in actions:
        actions["chat"]["detail"] = actions["chat"]["detail"].removesuffix(old_suffix).removesuffix("；会员：测试期间全部开放") + suffix
for frame in payload["frames"]:
    if not (args.source.parent / frame).is_file():
        raise SystemExit("Missing original native frame: " + frame)
template = (root / "docs/demo/template.html").read_text(encoding="utf-8")
result = template.replace("__DEMO_DATA__", json.dumps(payload, ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c"))
args.source.write_text(result, encoding="utf-8")
(args.source.parent / "action-coverage.json").write_text(json.dumps(payload["coverage"], ensure_ascii=False, indent=2), encoding="utf-8")
membership_note = "會員權益：測試期間全部開放；正式分級待公布。" if payload["membership"].get("testingOpen") else "會員權益：聊天需黃金及以上；其他姿態分級待公布。"
lines = ["# 動作覆蓋清單", "", membership_note, "",
         "| 角色 | 風格 | 服裝 | 動作 | 狀態 | 說明 |", "|---|---|---|---|---|---|"]
lines += ["| {character} | {category} | {outfit} | {title} | {status} | {detail} |".format(**r) for r in payload["coverage"]]
(args.source.parent / "動作清單.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Refreshed {version}; kept {len(payload['frames'])} original native frames ({payload['renderVersion']}).")
