"""Copy untouched imagegen atlases and attach measured, outfit-specific dance joints."""
import json, shutil, hashlib
from pathlib import Path
import cv2
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[1]
folder=root/'artwork/dance-repair'
sources=json.loads((folder/'sources.json').read_text(encoding='utf-8-sig'))
calibration=json.loads((folder/'calibration.json').read_text(encoding='utf-8-sig'))
report=[]
for id,source in sources.items():
    provenance=source
    source=Path(source)
    pack=root/'src/DesktopPet.App/Assets/Characters'/id
    target=pack/'motions/dance-base-atlas.png'
    if not source.is_file(): source=target
    with Image.open(source) as im:
        assert im.mode=='RGBA'
        alpha=np.asarray(im)[:,:,3]
        assert (alpha==0).mean()>.25
        _,_,stats,_=cv2.connectedComponentsWithStats((alpha>=48).astype('uint8'),8)
        boxes=sorted(sorted(stats[1:].tolist(),key=lambda s:s[4],reverse=True)[:3],key=lambda s:s[0]+s[2]/2)
        assert min(b[4] for b in boxes)>im.width*im.height*.035
        cells=[]
        for x,y,w,h,_ in boxes:
            l=max(0,x-6);t=max(0,y-6);r=min(im.width,x+w+6);b=min(im.height,y+h+6)
            cells.append(dict(x=l,y=t,width=r-l,height=b-t))
        size=im.size
    if source.resolve()!=target.resolve(): shutil.copyfile(source,target)
    c=json.loads((pack/'pet.json').read_text(encoding='utf-8-sig'))
    for i,outfit in enumerate(['original','swim','wedding']):
        key=id+'/'+outfit
        if key not in calibration: continue
        measured=calibration[key];cell=cells[i]
        points=[dict(x=(p[0]-cell['x'])/cell['width'],y=(p[1]-cell['y'])/cell['height']) for p in measured['joints']]
        assert len(points)==17 and all(0<=p['x']<=1 and 0<=p['y']<=1 for p in points)
        rig=dict(joints=points,waist=(measured['waist']-cell['y'])/cell['height'],hem=(measured['hem']-cell['y'])/cell['height'],skirtWidth=measured['skirtWidth']/cell['width'],clothWidths=[w/cell['width'] for w in measured['clothWidths']])
        if 'frontHem' in measured:
            rig.update(frontHem=(measured['frontHem']-cell['y'])/cell['height'],openingWidth=measured['openingWidth']/cell['width'])
        if 'armRadius' in measured:
            rig['armRadius']=measured['armRadius']/cell['height']
        assert .2<rig['waist']<.65 and rig['waist']<rig['hem']<=1, key
        assert 0<rig['skirtWidth']<=.5 and all(0<w<=.6 for w in rig['clothWidths']), key
        motions=c['motions'] if outfit=='original' else c['outfits'][outfit]['motions']
        motions['dance']=dict(file='motions/dance-base-atlas.png',columns=3,rows=1,cells=cells,frames=[i],frameMs=[1000],loop=False,isolateCells=True,danceRig=rig)
    (pack/'pet.json').write_text(json.dumps(c,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    report.append(dict(id=id,file=target.relative_to(root).as_posix(),source=provenance,dimensions=size,cells=cells,sha256=hashlib.sha256(source.read_bytes()).hexdigest()))
(folder/'manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
