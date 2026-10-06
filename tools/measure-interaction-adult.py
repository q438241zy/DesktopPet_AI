"""Measure new adult art without modifying any raster pixels.

One scale per four-pose atlas; separate feet and contact anchors keep the
interactive stage stable. Q poses retain the approved I2 source and metadata.
"""
import base64
import hashlib
import json
from pathlib import Path
import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
DEMO = ROOT / 'docs/demo/interaction-five'
ART = DEMO / 'art'
result = json.loads((ART / 'geometry.json').read_text(encoding='utf-8'))
result['version'] = 3
result['images'] = {}
result['supplements'] = {k:v for k,v in result['supplements'].items() if k.startswith('chibi')}
q = result['styles']['chibi']
for key, data in [('chibi', q), *result['supplements'].items()]:
    result['images'][key] = 'data:image/png;base64,' + base64.b64encode((DEMO / data['file']).read_bytes()).decode()

groups = {
    'highfive': ['high-prep','high-ready','high-contact','high-recoil'],
    'rps': ['fist-up','fist-mid','fist-down','reveal-rock'],
    'handoff': ['reveal-scissors','reveal-paper','receive','gift'],
    'book': ['gift-empty','read','page','read-finish'],
    'photo': ['neutral','lift','photo','happy']
}
all_poses = {}
sources = {}
for group, names in groups.items():
    filename = f'real-adult-{group}-final.png'
    path = ART / filename
    pixels = np.asarray(Image.open(path).convert('RGBA'))
    height, width = pixels.shape[:2]
    assert (pixels[:,:,3] == 0).mean() > .2, (filename, 'genuine alpha required')
    count, labels, stats, _ = cv2.connectedComponentsWithStats((pixels[:,:,3] > 70).astype('uint8'), 8)
    components = [(i,tuple(map(int,s))) for i,s in enumerate(stats) if i and s[4] > 10000]
    assert len(components) == 4, (filename, len(components), 'four isolated full characters required')
    components.sort(key=lambda item:item[1][0])
    reference = float(np.median([box[3] for _,box in components]))
    atlas = 'realistic-'+group
    measured = {}
    for name, (index,(x,y,w,h,area)) in zip(names,components):
        # Transparent image output may trim the outer canvas to a strand/shoe.
        # Full silhouettes are visually reviewed; metadata must remain in bounds.
        assert x >= 0 and y >= 0 and x+w <= width and y+h <= height, (filename,name,'out of bounds')
        bottom = labels[y+h-max(8,round(reference*.025)):y+h,x:x+w]
        _,xs = np.nonzero(bottom == index)
        foot = [round(x+float(np.median(xs)),2),y+h]
        left, top, right, bottom_edge = max(0,x-2),max(0,y-2),min(width,x+w+2),min(height,y+h+2)
        p = {'rect':[left,top,right-left,bottom_edge-top],
             'bounds':[x,y,w,h], 'foot':foot, 'area':area, 'atlas':atlas, 'anchors':{}}
        measured[name] = p
        all_poses[name] = p
    sources[atlas] = {'file':'art/'+filename,'dimensions':[width,height],'referenceHeight':reference,'poses':measured,
                      'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    result['images'][atlas] = 'data:image/png;base64,' + base64.b64encode(path.read_bytes()).decode()
    print(group, width, height, 'reference',reference, [(n,p['bounds']) for n,p in measured.items()])

reference = sources['realistic-photo']['referenceHeight']
for atlas,data in sources.items():
    unit = reference / data['referenceHeight']
    data['unit'] = unit
    for p in data['poses'].values(): p['unit'] = unit

# Fractions are inspected against the original full-resolution source, never
# inferred from a previous character with different proportions.
anchors_file = ART / 'adult-contact-anchors.json'
anchors = json.loads(anchors_file.read_text(encoding='utf-8'))
for name, spec in anchors.items():
    p = all_poses[name]
    x,y,w,h = p['bounds']
    for key,(fx,fy) in spec.get('anchors',{}).items():
        p['anchors'][key] = [round(x+w*fx-p['foot'][0],2),round(y+h*fy-p['foot'][1],2)]
    if 'frontY' in spec: p['frontY'] = round(y+h*spec['frontY']-p['foot'][1],2)
    if 'boxWidth' in spec: p['boxWidth'] = round(w*spec['boxWidth'],2)

result['styles']['realistic'] = {'file':sources['realistic-photo']['file'],
    'dimensions':sources['realistic-photo']['dimensions'], 'referenceHeight':reference,
    'poses':all_poses,'sha256':sources['realistic-photo']['sha256']}
result['supplements'].update(sources)
result['adultReference'] = {'file':'art/deepseek-original-reference.png',
    'sha256':hashlib.sha256((ART/'deepseek-original-reference.png').read_bytes()).hexdigest()}
(ART/'geometry.json').write_text(json.dumps({k:v for k,v in result.items() if k!='images'},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(DEMO/'art.js').write_text('globalThis.FIVE_ART = '+json.dumps(result,separators=(',',':'))+';\n',encoding='utf-8')
print('Q artwork unchanged; 20 adult poses selected, one fixed scale per atlas; no pixel edits.')
