"""Inspect unedited transparent snack sheets, then install their playback metadata."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[1]
folder = root / 'artwork/snack-correction'
parser = argparse.ArgumentParser()
parser.add_argument('--install', action='store_true')
args = parser.parse_args()
reports = []
for result in sorted((folder / 'results').glob('*.json')):
    job = json.loads(result.read_text(encoding='utf-8'))
    source = folder / 'sources' / (job['id'] + '.png')
    with Image.open(source) as sheet:
        sheet.load()
        if sheet.mode != 'RGBA':
            raise ValueError(str(source) + ': RGBA required')
        pixels = np.asarray(sheet)
        alpha = pixels[:, :, 3]
        if np.mean(alpha == 0) < .2:
            raise ValueError(str(source) + ': transparent gutters required')
        for separation in (48, 96, 160, 200, 240):
            _, _, stats, _ = cv2.connectedComponentsWithStats((alpha >= separation).astype(np.uint8), 8)
            parts = sorted(stats[1:].tolist(), key=lambda p: p[4], reverse=True)
            if len(parts) >= 8 and parts[7][4] >= sheet.width * sheet.height * .025:
                break
        else:
            raise ValueError(str(source) + ': eight isolated figures required')
        figures = sorted(parts[:8], key=lambda p: p[1] + p[3] / 2)
        if figures[4][1] <= max(p[1] + p[3] for p in figures[:4]):
            raise ValueError(str(source) + ': row overlap')
        figures = sorted(figures[:4], key=lambda p: p[0]) + sorted(figures[4:], key=lambda p: p[0])
        if any(p[0] <= 0 or p[1] <= 0 or p[0]+p[2] >= sheet.width or p[1]+p[3] >= sheet.height for p in figures):
            raise ValueError(str(source) + ': cropped outer silhouette')
        cells = []
        hashes = set()
        for x, y, w, h, count in figures:
            # Measurements only: preserve all generated pixels and let native
            # ownership retain the original antialiased outline.
            margin = 3 if separation == 48 else 12
            left, top = max(0, x-margin), max(0, y-margin)
            right, bottom = min(sheet.width, x+w+margin), min(sheet.height, y+h+margin)
            if w > sheet.width / 4 * 1.12 or h > sheet.height / 2 * 1.06:
                raise ValueError(str(source) + ': frame spills into a neighbour')
            cell = dict(x=left, y=top, width=right-left, height=bottom-top)
            cells.append(cell)
            hashes.add(hashlib.sha256(pixels[top:bottom, left:right].tobytes()).hexdigest())
        if len(hashes) != 8:
            raise ValueError(str(source) + ': repeated source poses')
        reference = float(np.median([p[3] for p in figures]))
        sprite = dict(file='motions/snack-continuity.png', columns=4, rows=2, cells=cells,
                      frames=list(range(8)), frameMs=[240, 260, 340, 340, 380, 400, 420, 620],
                      loop=False, bakedProps=True, isolateCells=True, separationAlpha=separation,
                      heightRatios=[round(p[3]/reference, 6) for p in figures],
                      referenceHeightPixels=reference)
        target = root / 'src/DesktopPet.App/Assets/Characters' / job['id'] / sprite['file']
        reports.append(dict(id=job['id'], source=str(source.relative_to(root)),
                            sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                            dimensions=sheet.size, transparentFraction=float(np.mean(alpha == 0)),
                            sprite=sprite))
if args.install:
    if len(reports) != 8:
        raise ValueError('All eight characters must pass inspection before installation')
    for report in reports:
        target = root / 'src/DesktopPet.App/Assets/Characters' / report['id'] / report['sprite']['file']
        shutil.copyfile(root / report['source'], target)
        manifest = target.parents[1] / 'pet.json'
        character = json.loads(manifest.read_text(encoding='utf-8'))
        character['motions']['eat'] = report['sprite']
        character['motions']['feed'] = report['sprite']
        manifest.write_text(json.dumps(character, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(folder / 'manifest.json').write_text(json.dumps(reports, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'{len(reports)} original Q snack sheets inspected; installed={args.install}')
