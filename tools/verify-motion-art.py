"""Read-only checks of new walk sheets: six intact, distinct transparent cells."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('--sources', action='store_true', help='Inspect generated results before installation')
parser.add_argument('--partial', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
requests = json.loads((root / 'artwork/motion-continuity/requests.json').read_text(encoding='utf-8'))
requests = [job for job in requests if '-3d/' not in job['dest'].replace('\\','/')]
report, errors, missing, hashes = [], [], [], set()
for job in requests:
    key, target = job['key'], job['dest']
    path = root / target
    if args.sources:
        result = root / f'artwork/motion-continuity/results/{key}.json'
        correction = result.with_name(key + '-padding.json')
        if correction.exists():
            result = correction
        if not result.exists():
            missing.append(key)
            continue
        path = Path(json.loads(result.read_text(encoding='utf-8'))['source'])
    if not path.exists():
        missing.append(key)
        continue
    with Image.open(path) as im:
        im.load()
        if im.mode != 'RGBA' or im.size != (1536, 1024):
            errors.append(f'{key}: expected RGBA 1536x1024, got {im.mode} {im.size}')
            continue
        alpha = im.getchannel('A')
        hist = alpha.histogram()
        if hist[0] < im.width * im.height * .15:
            errors.append(f'{key}: insufficient transparent background')
        bounds, frame_hashes, edges = [], set(), []
        for i in range(6):
            x, y = i % 3 * 512, i // 3 * 512
            cell = alpha.crop((x, y, x + 512, y + 512))
            box = cell.point(lambda a: 255 if a >= 48 else 0).getbbox()
            bounds.append(box)
            if not box or sum(cell.histogram()[240:]) < 10000:
                errors.append(f'{key}: empty or weak silhouette in frame {i}')
            elif box[0] == 0 or box[1] == 0 or box[2] == 512 or box[3] == 512:
                edges.append(i)
            frame_hashes.add(hashlib.sha256(im.crop((x, y, x + 512, y + 512)).tobytes()).hexdigest())
        if edges:
            errors.append(f'{key}: frame edges touched: {edges}')
        if len(frame_hashes) != 6:
            errors.append(f'{key}: duplicate frames')
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if digest in hashes:
            errors.append(f'{key}: duplicate sheet across appearances')
        hashes.add(digest)
        report.append({'key': key, 'target': target, 'bounds': bounds,
                       'transparentFraction': round(hist[0] / (im.width * im.height), 4), 'sha256': digest})
output = root / 'artifacts/motion-art-check.json'
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps({'checked': report, 'missing': missing, 'errors': errors}, ensure_ascii=False, indent=2), encoding='utf-8')
for error in errors:
    print('FAIL', error)
print(f'{len(report)}/{len(requests)} walk sheets inspected; {len(missing)} pending; {len(errors)} errors.')
if errors or (missing and not args.partial):
    raise SystemExit(1)
