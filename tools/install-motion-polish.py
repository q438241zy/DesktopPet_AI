"""Inspect alpha, record crop metadata and copy untouched imagegen output into character packs."""
import argparse, hashlib, json, shutil
from pathlib import Path
import cv2
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[1]
folder=root/'artwork/motion-polish'
parser=argparse.ArgumentParser();parser.add_argument('--install',action='store_true');parser.add_argument('--key');args=parser.parse_args()
source_folder=root/'.artifacts/motion-polish/sources'

def regions(im,columns,rows):
    alpha=np.asarray(im)[:,:,3]
    _,_,stats,_=cv2.connectedComponentsWithStats((alpha>=48).astype(np.uint8),8)
    parts=sorted(stats[1:].tolist(),key=lambda b:b[4],reverse=True)
    count=columns*rows
    if len(parts)<count or parts[count-1][4]<im.width*im.height*.003:
        raise ValueError(f'Expected {count} separate figures; sizes {[b[4] for b in parts[:count]]}')
    boxes=[[x,y,x+w,y+h] for x,y,w,h,_ in parts[:count]]
    boxes.sort(key=lambda b:(b[1]+b[3])/2)
    # A generated 4x3 layout must never be silently read as a 3x4 sequence.
    groups=[]
    for box in boxes:
        center=(box[1]+box[3])/2
        if not groups or center-(groups[-1][-1][1]+groups[-1][-1][3])/2>im.height/max(rows,columns)*.4:
            groups.append([])
        groups[-1].append(box)
    if len(groups)!=rows or any(len(g)!=columns for g in groups):
        raise ValueError(f'Observed grid {[len(g) for g in groups]}, expected {rows} rows of {columns}')
    boxes=[b for r in range(rows) for b in sorted(boxes[r*columns:(r+1)*columns],key=lambda b:(b[0]+b[2])/2)]
    def distance(a,b):return max(a[0]-b[2],b[0]-a[2],0)**2+max(a[1]-b[3],b[1]-a[3],0)**2
    for x,y,w,h,n in parts[count:]:
        if n<16:continue
        b=[x,y,x+w,y+h];i=min(range(count),key=lambda j:distance(b,boxes[j]))
        if distance(b,boxes[i])<(im.height/rows*.38)**2:
            a=boxes[i];boxes[i]=[min(a[0],b[0]),min(a[1],b[1]),max(a[2],b[2]),max(a[3],b[3])]
    cells=[]
    for i,(x,y,r,b) in enumerate(boxes):
        x=max(0,x-3);y=max(0,y-3);r=min(im.width,r+3);b=min(im.height,b+3)
        if r-x>im.width/columns*1.23 or b-y>im.height/rows*1.27:raise ValueError(f'Figure {i} overlaps neighbours')
        cells.append(dict(x=x,y=y,width=r-x,height=b-y))
    return cells

selected={}
for p in sorted((folder/'results').glob('*.json')):
    j=json.loads(p.read_text(encoding='utf-8'))
    if j.get('revision',1)>=selected.get(j['key'],{}).get('revision',0):selected[j['key']]=j
reports=[]; failures=[]
for key,j in selected.items():
    if j['id'].endswith('-3d'): continue
    if args.key and args.key!=key:continue
    try:
        source=Path(j['source']);columns=j.get('columns',3);rows=j.get('rows',4)
        with Image.open(source) as im:
            im.load()
            if im.mode!='RGBA':raise ValueError('RGBA required')
            transparent=im.getchannel('A').histogram()[0]/(im.width*im.height)
            if transparent<.25:raise ValueError('Transparent background missing')
            cells=regions(im,columns,rows)
            frames=j.get('frames',list(range(columns*rows)))
            hashes=[hashlib.sha256(im.crop((c['x'],c['y'],c['x']+c['width'],c['y']+c['height'])).tobytes()).hexdigest() for c in cells]
            if len(set(hashes[i] for i in frames))!=len(frames):raise ValueError('Duplicate frames')
            dimensions=im.size
        action=j['action'];base=Path('motions') if j['outfit']=='original' else Path('outfits')/j['outfit']
        relative=(base/(action+'-smooth.png')).as_posix()
        sprite=dict(file=relative,columns=columns,rows=rows,cells=cells,frames=frames,
                    frameMs=[80]*12 if action=='walk' else [240,260,280,300,300,320,300,320,340,380,400,960],loop=action=='walk',isolateCells=True)
        if action=='walk':sprite['facing']='right'
        else:sprite.update(bakedProps=True,heightRatios=[.76]*(columns*rows))
        target=root/'src/DesktopPet.App/Assets/Characters'/j['id']/relative
        if args.install:
            target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
            source_folder.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,source_folder/(key+'.png'))
            p=root/'src/DesktopPet.App/Assets/Characters'/j['id']/'pet.json'
            c=json.loads(p.read_text(encoding='utf-8-sig'))
            motions=c['motions'] if j['outfit']=='original' else c['outfits'][j['outfit']]['motions']
            motions[action]=sprite;p.write_text(json.dumps(c,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        reports.append(dict(key=key,id=j['id'],outfit=j['outfit'],action=action,file=target.relative_to(root).as_posix(),source=str(source),sha256=hashlib.sha256(source.read_bytes()).hexdigest(),dimensions=dimensions,cells=cells,frames=frames,transparentFraction=round(transparent,4)))
    except Exception as e:failures.append(dict(key=key,error=str(e)))
if args.install:
    p=folder/'manifest.json';old={r['key']:r for r in json.loads(p.read_text())} if p.exists() else {}
    old.update((r['key'],r) for r in reports);p.write_text(json.dumps(list(old.values()),indent=2),encoding='utf-8')
(root/'artifacts/motion-polish-audit.json').write_text(json.dumps(dict(accepted=reports,rejected=failures),indent=2),encoding='utf-8')
print(json.dumps(dict(accepted=len(reports),rejected=failures),ensure_ascii=False))
if failures:raise SystemExit(1)
