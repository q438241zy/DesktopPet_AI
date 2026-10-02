"""Measure existing atlases and author scale metadata; never change bitmap pixels.

Legacy Q atlases shrink the face in their middle frames. Match the unchanged
hair/face details to the idle reference with SIFT and a RANSAC similarity fit.
Only these legacy sheets get frame corrections. New pose sheets retain one
pixel scale so sitting, bending and lying are physical poses, not resizing.
"""
import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('--install', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
assets = root / 'src/DesktopPet.App/Assets/Characters'
output = root / 'artwork/scale-consistency'
output.mkdir(exist_ok=True)
cv2.setRNGSeed(240)
sift = cv2.SIFT_create(nfeatures=800)
matcher = cv2.BFMatcher()
reports = []


def frames(folder, sprite):
    pixels = np.asarray(Image.open(folder / sprite['file']).convert('RGBA'))
    cols, rows = sprite.get('columns', 3), sprite.get('rows', 2)
    result = []
    for i in range(cols * rows):
        cell = sprite.get('cells', [None] * (cols * rows))[i]
        if cell:
            x, y, w, h = (cell[k] for k in ['x', 'y', 'width', 'height'])
        else:
            w, h = pixels.shape[1] // cols, pixels.shape[0] // rows
            x, y = i % cols * w, i // cols * h
        result.append(pixels[y:y+h, x:x+w])
    return result


def height(pixels):
    ys = np.where(pixels[:, :, 3] >= 48)[0]
    return int(ys.max() - ys.min() + 1)


def features(pixels, reference=False):
    gray = cv2.cvtColor(pixels[:, :, :3], cv2.COLOR_RGB2GRAY)
    mask = (pixels[:, :, 3] >= 80).astype('uint8') * 255
    if reference:
        ys = np.where(mask)[0]
        mask[int(ys.min() + (ys.max() - ys.min()) * .67):] = 0
    return sift.detectAndCompute(gray, mask)


def match(reference, pixels):
    rk, rd = reference
    k, d = features(pixels)
    if d is None or rd is None:
        return None
    pairs = matcher.knnMatch(rd, d, k=2)
    good = [p[0] for p in pairs if len(p) == 2 and p[0].distance < .76 * p[1].distance]
    if len(good) < 4:
        return None
    matrix, inliers = cv2.estimateAffinePartial2D(
        np.float32([rk[g.queryIdx].pt for g in good]),
        np.float32([k[g.trainIdx].pt for g in good]),
        method=cv2.RANSAC, ransacReprojThreshold=3)
    if matrix is None:
        return None
    scale = float(np.hypot(matrix[0, 0], matrix[1, 0]))
    if not .45 <= scale <= 1.65:
        return None
    return dict(scale=round(scale, 6), inliers=int(inliers.sum()), matches=len(good))


for manifest in sorted(assets.glob('*/pet.json')):
    data = json.loads(manifest.read_text(encoding='utf-8'))
    if data.get('category') == '3d' or data['id'].endswith('-3d'):
        continue
    category = data.get('category', 'chibi')
    for outfit in ['original', 'swim', 'wedding']:
        appearance = data if outfit == 'original' else data['outfits'][outfit]
        idle = appearance['atlas' if outfit == 'original' else 'idle']
        idle_frame = frames(manifest.parent, idle)[0]
        reference = features(idle_frame, True)
        reference_extent = max(idle_frame.shape[:2])
        sheets = {'idle': idle, **appearance['motions']}
        if outfit == 'original' and data.get('dizzy'):
            sheets['dizzy'] = data['dizzy']
        measured = {}
        for action, sprite in sheets.items():
            key = (sprite['file'], tuple(sprite.get('frames', [])))
            if key in measured:
                sprite.update(measured[key])
                continue
            pixels = frames(manifest.parent, sprite)
            orders = sprite.get('frames', list(range(len(pixels))))
            authored = {}
            detail = dict(character=data['id'], outfit=outfit, action=action,
                          file=sprite['file'], sha256=hashlib.sha256((manifest.parent / sprite['file']).read_bytes()).hexdigest())
            # New drawings already depict the lowered body. The old .76 value
            # applied a second shrink to every Q block-building pose.
            if category == 'chibi' and action == 'build':
                authored['referenceHeightPixels'] = float(np.median([height(pixels[i]) for i in orders]))
                detail['method'] = 'fixed seated sheet pixels; remove duplicate .76 shrink'
            elif category == 'realistic' and action != 'idle' and action != 'walk':
                # Interaction frame 0 is the neutral standing figure, even when
                # a particular clip plays only its seated or lying cells.
                neutral = orders[0] if sprite.get('danceRig') else 0
                values = [height(p) for p in pixels] if 'contact' in sprite['file'] else [height(pixels[neutral])]
                authored['referenceHeightPixels'] = float(np.median(values))
                detail['method'] = 'fixed neutral body pixels; posture and props do not rescale frames'
            elif category == 'chibi' and outfit == 'original' and 'cells' not in sprite:
                fits = [match(reference, p) for p in pixels]
                valid = [f['scale'] for f in fits if f and f['inliers'] >= 7]
                # Middle legacy poses share one drawing scale; a median prevents
                # expression changes from making the head pulse between frames.
                middle = [f['scale'] for f in fits[1:-1] if f and f['inliers'] >= 7]
                median = float(np.median(middle or valid)) if valid else 1.
                factors = []
                for i, (p, fit) in enumerate(zip(pixels, fits)):
                    scale = fit['scale'] if fit and fit['inliers'] >= 12 else median
                    if 0 < i < len(pixels) - 1 and (action in ['jump', 'think', 'pickup', 'peek', 'shaken', 'shaken-strong']
                                                  or fit and abs(scale - median) < .10):
                        scale = median
                    if action == 'idle' and (not fit or fit['inliers'] < 12):
                        scale = 1.
                    # Leave tiny illustration differences alone; calibrate the
                    # actual 10–40% source shrink, not facial-expression noise.
                    if abs(scale - 1) < .04:
                        scale = 1.
                    factor = max(p.shape[:2]) / reference_extent / scale
                    if not .25 <= factor <= 4:
                        raise ValueError(f'Unsafe scale: {data["id"]}/{action}/{i}: {factor}')
                    factors.append(round(factor, 6))
                factors[0] = 1. if action == 'idle' else factors[0]
                authored['frameScaleFactors'] = factors
                detail.update(method='legacy face/hair similarity fit', fits=fits, frameScaleFactors=factors)
            if authored:
                sprite.update(authored)
                detail.update(authored)
                reports.append(detail)
            measured[key] = authored
        print(data['id'] + '/' + outfit, flush=True)
    if args.install:
        manifest.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

(output / 'calibration.json').write_text(json.dumps(reports, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'{len(reports)} measured sprite definitions; installed={args.install}', flush=True)
