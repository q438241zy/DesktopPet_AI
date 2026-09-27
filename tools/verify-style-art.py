"""Inspect generated PNGs without altering any image pixels or alpha channels."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('--partial', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
requests = json.loads((root / 'artwork/wardrobe-expansion/requests.json').read_text(encoding='utf-8'))
targets = [job['target'] for job in requests['jobs']]
targets += [f'src/DesktopPet.App/Assets/Characters/deepseek-{style}/portrait.png' for style in ('3d', 'adult')]
report, errors, missing, hashes = [], [], [], set()
for target in targets:
    path = root / target
    if not path.exists():
        missing.append(target)
        continue
    with Image.open(path) as im:
        im.load()
        if im.mode != 'RGBA':
            errors.append(f'{target}: requires RGBA transparency, got {im.mode}')
            continue
        alpha = im.getchannel('A')
        histogram = alpha.histogram()
        w, h = im.size
        bbox = alpha.point(lambda value: 255 if value >= 48 else 0).getbbox()
        corners = [alpha.getpixel(p) for p in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1))]
        if max(corners) > 4 or histogram[0] < w * h * .08 or sum(histogram[240:]) < 10000:
            errors.append(f'{target}: alpha does not describe a transparent character cutout')
        if not bbox or w < 512 or h < 768 or max(w, h) > 6144:
            errors.append(f'{target}: invalid dimensions or empty silhouette')
        touches_edge = bool(bbox and (bbox[0] == 0 or bbox[1] == 0 or bbox[2] == w or bbox[3] == h))
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if digest in hashes:
            errors.append(f'{target}: duplicate image used for a different appearance')
        hashes.add(digest)
        report.append({'target': target, 'size': [w, h], 'visibleBounds': bbox, 'touchesEdge': touches_edge,
                       'transparentFraction': round(histogram[0] / (w * h), 4), 'sha256': digest})
output = root / 'artifacts/style-art-check.json'
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps({'checked': report, 'missing': missing, 'errors': errors}, ensure_ascii=False, indent=2), encoding='utf-8')
for error in errors:
    print('FAIL', error)
for item in report:
    if item['touchesEdge']:
        print('REVIEW EDGE', item['target'])
print(f'{len(report)}/48 images inspected; {len(missing)} pending; {len(errors)} errors.')
if errors or (missing and not args.partial):
    raise SystemExit(1)
