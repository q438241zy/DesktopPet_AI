"""Inspect generated originals and install byte-for-byte copies with declarative frame regions.

This script does not redraw, retouch, resize or save any bitmap pixels. WPF crops
the recorded cells at runtime, preserving the image generator's original alpha.
"""
import argparse
import hashlib
import json
import shutil
from collections import deque
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('--install', action='store_true')
parser.add_argument('--key')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
results = root / 'artwork/contact-motion/results'

def figure_regions(alpha, separators=(), column_separators=()):
    """Read connected silhouettes to record tight crop metadata; never change pixels."""
    w, h = alpha.size
    pixels = bytearray(alpha.tobytes())
    # Reviewed row boundaries separate touching antialias fringes in the analysis graph.
    # The source image and all of its pixels remain untouched.
    for y in separators:
        pixels[y*w:(y+1)*w] = bytes(w)
    for x in column_separators:
        for y in range(h): pixels[y*w+x] = 0
    components = []
    for seed in range(w * h):
        if not pixels[seed]:
            continue
        pixels[seed] = 0
        queue = deque([seed])
        left = right = seed % w
        top = bottom = seed // w
        count = 0
        while queue:
            pos = queue.popleft()
            x, y = pos % w, pos // w
            count += 1
            left, right = min(left, x), max(right, x)
            top, bottom = min(top, y), max(bottom, y)
            for neighbor in (pos-1 if x else -1, pos+1 if x+1 < w else -1,
                             pos-w if y else -1, pos+w if y+1 < h else -1):
                if neighbor >= 0 and pixels[neighbor]:
                    pixels[neighbor] = 0
                    queue.append(neighbor)
        if count > w*h*.002:
            components.append((count, (left, top, right+1, bottom+1)))
    main = sorted(components, reverse=True)[:9]
    if len(main) != 9 or min(box[3]-box[1] for _, box in main) < h/6:
        raise ValueError(f'Expected nine separate full-body silhouettes: {main}')
    ordered = sorted((box for _, box in main), key=lambda b: (b[1]+b[3])/2)
    ordered = [box for row in range(3) for box in sorted(ordered[row*3:row*3+3], key=lambda b: b[0])]
    regions = []
    for box in ordered:
        if box[0] <= 0 or box[1] <= 0 or box[2] >= w or box[3] >= h:
            raise ValueError(f'A full-body silhouette reaches the canvas edge: {box} in {w}x{h}')
        min_y = max((y+1 for y in separators if y < box[1]), default=0)
        max_y = min((y for y in separators if y >= box[3]), default=h)
        min_x = max((x+1 for x in column_separators if x < box[0]), default=0)
        max_x = min((x for x in column_separators if x >= box[2]), default=w)
        regions.append((max(min_x, box[0]-3), max(min_y, box[1]-3), min(max_x, box[2]+3), min(max_y, box[3]+3)))
    return regions

reports = []
selected = {}
overrides = json.loads((results.parent / 'calibration.json').read_text(encoding='utf-8'))
for record in sorted(results.glob('*.json')):
    candidate = json.loads(record.read_text(encoding='utf-8'))
    if candidate.get('revision', 0) >= selected.get(candidate['key'], {}).get('revision', -1):
        selected[candidate['key']] = candidate
if args.key and args.key not in selected:
    parser.error(f'Unknown appearance: {args.key}')
for job in selected.values():
    if job['id'].endswith('-3d'): continue
    if args.key and job['key'] != args.key:
        continue
    print('Inspecting', job['key'], flush=True)
    if job.get('discarded'):
        continue
    job.update(overrides.get(job['key'], {}))
    source = Path(job['source'])
    with Image.open(source) as im:
        im.load()
        if im.mode != 'RGBA':
            raise ValueError(f'{job["key"]}: missing RGBA')
        alpha = im.getchannel('A').point(lambda v: 255 if v >= 48 else 0)
        w, h = im.size
        regions = figure_regions(alpha, job.get('separators', []), job.get('columnSeparators', []))
        cells, bounds, signatures = [], [], set()
        for y in range(3):
            for x in range(3):
                region = regions[y*3+x]
                box = alpha.crop(region).getbbox()
                if box is None or box[3] - box[1] < h / 6:
                    raise ValueError(f'{job["key"]}: missing figure {y*3+x}')
                cells.append(dict(x=region[0], y=region[1], width=region[2]-region[0], height=region[3]-region[1]))
                bounds.append(box)
                signatures.add(hashlib.sha256(im.crop(region).tobytes()).hexdigest())
        if len(signatures) != 9:
            raise ValueError(f'{job["key"]}: duplicate frames')
        transparent = im.getchannel('A').histogram()[0] / (w*h)
        if transparent < .25:
            raise ValueError(f'{job["key"]}: inadequate transparency')
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    target = root / job['dest']
    hands = [None] * 6 + [dict(height=v, span=job.get('handSpan', .11)) for v in job.get('handHeights', [.26, .25, .27])]
    for i, offset in enumerate(job.get('handOffsets', [0, 0, 0])):
        hands[i+6]['offset'] = offset
    def clip(order, times, *, loop=True, food=False, contact=False):
        entry = dict(file=Path(job['dest']).relative_to(Path('src/DesktopPet.App/Assets/Characters') / job['id']).as_posix(),
                     columns=3, rows=3, frames=order, frameMs=times, loop=loop, cells=cells)
        if food: entry['bakedProps'] = True
        if contact: entry['hands'] = hands
        return entry
    motions = {
        'eat': clip([0,1,2,1,0], [250,430,500,240,300], food=True),
        'meal': clip([3,4,5,3], [350,520,550,350], food=True),
        'ball-ready': clip([6], [5000], loop=False, contact=True),
        'anticipate': clip([6], [5000], loop=False, contact=True),
        'ball-hit': clip([6,7,8], [100,180,600], loop=False, contact=True),
        'ball-hold': clip([8], [5000], loop=False, contact=True),
    }
    if args.install:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        manifest = root / 'src/DesktopPet.App/Assets/Characters' / job['id'] / 'pet.json'
        data = json.loads(manifest.read_text(encoding='utf-8-sig'))
        destination = data['motions'] if job['outfit'] == 'original' else data['outfits'][job['outfit']]['motions']
        destination.update(motions)
        manifest.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    reports.append(dict(key=job['key'], revision=job.get('revision', 0), file=job['dest'], source=str(source), dimensions=[w,h],
                        bounds=bounds, cells=cells, hands=hands, sha256=digest, transparentFraction=round(transparent,4)))
report = root / 'artwork/contact-motion/manifest.json'
combined = {entry['key']: entry for entry in json.loads(report.read_text(encoding='utf-8'))} if args.key and report.exists() else {}
combined.update((entry['key'], entry) for entry in reports)
report.write_text(json.dumps([combined[key] for key in sorted(combined)], ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'{len(reports)}/24 appearance sheets inspected' + (' and installed.' if args.install else '.'))
