"""Measure sprite placement and contact anchors; original PNG pixels are untouched."""
import base64
import hashlib
import json
import runpy
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'docs/demo/interaction-five/art'
if (ART / 'adult-selected.json').exists():
    # Keep the historical command safe: never put the rejected dwarf artwork
    # back into the current Demo when updating measured placement metadata.
    runpy.run_path(str(ROOT / 'tools/measure-interaction-adult.py'), run_name='__main__')
    raise SystemExit(0)
result = {'version': 2, 'styles': {}, 'images': {}, 'supplements': {}}
names = ['neutral', 'lift', 'highfive', 'rock', 'scissors', 'paper',
         'receive', 'gift', 'open', 'read', 'page', 'photo']
for style, filename in [('chibi', 'deepseek-chibi-v1.png'), ('realistic', 'deepseek-realistic-v2.png')]:
    source = ART / filename
    pixels = np.array(Image.open(source))
    height, width = pixels.shape[:2]
    assert pixels.shape[2] == 4 and (pixels[:, :, 3] == 0).mean() > .2, 'Real alpha required'
    count, labels, stats, _ = cv2.connectedComponentsWithStats((pixels[:, :, 3] > 70).astype('uint8'), 8)
    items = [(index, tuple(map(int, box))) for index, box in enumerate(stats) if index and box[4] > 3000]
    assert len(items) == 12, (filename, len(items))
    items.sort(key=lambda item: item[1][1] + item[1][3])
    ordered = []
    for row in range(4):
        ordered.extend(sorted(items[row*3:(row+1)*3], key=lambda item: item[1][0]))
    reference = ordered[0][1][3]
    poses = {}
    for name, (index, (x, y, w, h, area)) in zip(names, ordered):
        assert x > 1 and y > 1 and x+w < width-1 and y+h < height-1, (filename, name, 'clipped')
        low = labels[y+h-max(5, round(reference*.035)):y+h, x:x+w]
        _, xs = np.nonzero(low == index)
        footx = x + float(np.median(xs))
        # Shoe alignment is positional only. One fixed scale for the entire style.
        footy = y+h
        poses[name] = {'rect': [max(0, x-2), max(0, y-2), min(width-x+2, w+4), min(height-y+2, h+4)],
                       'foot': [round(footx, 2), footy], 'bounds': [x, y, w, h], 'area': area}
    result['styles'][style] = {'file': 'art/'+filename, 'dimensions': [width, height],
                                'referenceHeight': reference, 'poses': poses,
                                'sha256': hashlib.sha256(source.read_bytes()).hexdigest()}
    result['images'][style] = 'data:image/png;base64,' + base64.b64encode(source.read_bytes()).decode()
    print(style, '12 full sprites, fixed scale', reference, 'PNG', width, height)
    for name, pose in poses.items():
        print(name, pose['bounds'], pose['foot'])
supplement_names = ['high-prep','high-ready','high-contact','high-recoil',
                    'fist-up','fist-mid','fist-down','reveal-rock',
                    'reveal-scissors','reveal-paper','gift-empty','read-finish']
hand_fractions = {
    'chibi': {'high-ready': (.19,.34),'high-contact':(.18,.40),'high-recoil':(.24,.55)},
    'realistic': {'high-ready':(.17,.15),'high-contact':(.25,.28),'high-recoil':(.20,.27)}
}
for style in ['chibi','realistic']:
    filename = f'interaction-{style}-v2.png'
    source = ART/filename
    pixels = np.array(Image.open(source)); height,width=pixels.shape[:2]
    assert pixels.shape[2]==4 and (pixels[:,:,3]==0).mean()>.2, 'Genuine transparent sprites required'
    count,labels,stats,_=cv2.connectedComponentsWithStats((pixels[:,:,3]>70).astype('uint8'),8)
    items=[(index,tuple(map(int,box))) for index,box in enumerate(stats) if index and box[4]>3000]
    assert len(items)==12,(filename,len(items))
    items.sort(key=lambda item:item[1][1]+item[1][3]);ordered=[]
    for row in range(3):ordered.extend(sorted(items[row*4:(row+1)*4],key=lambda item:item[1][0]))
    reference=ordered[0][1][3];key=style+'-interaction'
    unit=result['styles'][style]['referenceHeight']/reference
    measured={}
    for name,(index,(x,y,w,h,area)) in zip(supplement_names,ordered):
        assert x>1 and y>1 and x+w<width-1 and y+h<height-1,(filename,name,'clipped')
        low=labels[y+h-max(5,round(reference*.035)):y+h,x:x+w];_,xs=np.nonzero(low==index)
        footx=x+float(np.median(xs));footy=y+h
        pose={'rect':[x-2,y-2,w+4,h+4],'foot':[round(footx,2),footy],
              'bounds':[x,y,w,h],'area':area,'atlas':key,'unit':unit,'anchors':{}}
        if name in hand_fractions[style]:
            ax,ay=hand_fractions[style][name];pose['anchors']['hand']=[round(x+w*ax-footx,2),round(y+h*ay-footy,2)]
        if name=='gift-empty':
            pose['anchors']['giftSlot']=[round(x+w*.5-footx,2),round(y+h*(.61 if style=='chibi' else .425)-footy,2)]
            pose['frontY']=round(y+h*(.646 if style=='chibi' else .46)-footy,2)
            pose['boxWidth']=round(w*.31,2)
        measured[name]=pose;result['styles'][style]['poses'][name]=pose
        print(style,name,pose['bounds'],pose['foot'],pose['anchors'])
    result['supplements'][key]={'file':'art/'+filename,'dimensions':[width,height],'referenceHeight':reference,'unit':unit,'poses':measured,
                                  'sha256':hashlib.sha256(source.read_bytes()).hexdigest()}
    result['images'][key]='data:image/png;base64,'+base64.b64encode(source.read_bytes()).decode()
(ART/'geometry.json').write_text(json.dumps({k: v for k, v in result.items() if k != 'images'}, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
(ART.parent/'art.js').write_text('globalThis.FIVE_ART = '+json.dumps(result, separators=(',', ':'))+';\n', encoding='utf-8')
