"""Export crop ownership metadata for HTML; never rewrite source image pixels."""
import base64
import importlib.util
import json
from pathlib import Path
import numpy as np
from PIL import Image

_cache = {}
_spec = importlib.util.spec_from_file_location('sports_stage', Path(__file__).with_name('stage-sports-identity.py'))
_stage = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_stage)

def owned_cells(path, sprite):
    cells = sprite.get('cells', [])
    if not sprite.get('isolateCells') or ('/identity-' not in sprite['file'] and not sprite.get('exportOwnership')):
        return [None] * len(cells)
    key = (str(path), json.dumps(cells, sort_keys=True), sprite.get('separationAlpha', 80))
    if key not in _cache:
        rgba = np.asarray(Image.open(path).convert('RGBA'))
        ordered, labels, _, _ = _stage.components(rgba, len(cells), cells, sprite.get('separationAlpha', 80))
        _cache[key] = [(labels[c['y']:c['y']+c['height'], c['x']:c['x']+c['width']] == ordered[i][0]) for i,c in enumerate(cells)]
    return _cache[key]

def encode(mask):
    return None if mask is None else base64.b64encode(np.packbits(mask.reshape(-1)).tobytes()).decode('ascii')
