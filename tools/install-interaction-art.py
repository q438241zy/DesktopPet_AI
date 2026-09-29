"""Analyze generated RGBA sheets; copy originals without modifying bitmap pixels."""
import argparse, hashlib, json, os, shutil, time
from collections import deque
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('--install', action='store_true')
parser.add_argument('--key')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
folder = root / 'artwork/interaction-poses'

def write_json(path, data):
    temporary=path.with_suffix(path.suffix+'.tmp')
    for attempt in range(4):
        try:
            temporary.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
            os.replace(temporary,path)
            return
        except OSError:
            if attempt==3: raise
            time.sleep(.25)

def regions(im):
    w, h = im.size
    values = bytearray(im.getchannel('A').point(lambda a: 255 if a >= 48 else 0).tobytes())
    parts = []
    for seed in range(w*h):
        if not values[seed]: continue
        values[seed] = 0
        queue = deque([seed]); count = 0
        left = right = seed % w; top = bottom = seed // w
        while queue:
            p = queue.popleft(); x, y = p % w, p // w; count += 1
            left, right, top, bottom = min(left,x), max(right,x), min(top,y), max(bottom,y)
            for n in (p-1 if x else -1, p+1 if x+1<w else -1, p-w if y else -1, p+w if y+1<h else -1):
                if n >= 0 and values[n]: values[n] = 0; queue.append(n)
        if count >= 24: parts.append((count, [left, top, right+1, bottom+1]))
    parts.sort(reverse=True)
    if len(parts) < 16 or parts[15][0] < w*h*.003: raise ValueError('Expected 16 separate character silhouettes')
    figures = sorted([box[:] for _,box in parts[:16]], key=lambda b:(b[1]+b[3])/2)
    figures = [b for row in range(4) for b in sorted(figures[row*4:row*4+4], key=lambda b:(b[0]+b[2])/2)]
    def distance(a,b):
        return max(a[0]-b[2],b[0]-a[2],0)**2 + max(a[1]-b[3],b[1]-a[3],0)**2
    # Include small disconnected cubes and accessories in the nearest silhouette's crop.
    for _,box in parts[16:]:
        nearest = min(range(16), key=lambda i:distance(box,figures[i]))
        if distance(box,figures[nearest]) <= (h*.028)**2:
            a=figures[nearest]; figures[nearest]=[min(a[0],box[0]),min(a[1],box[1]),max(a[2],box[2]),max(a[3],box[3])]
    cells=[]
    for i,b in enumerate(figures):
        if b[0]<=0 or b[1]<=0 or b[2]>=w or b[3]>=h: raise ValueError(f'Figure {i} touches canvas edge: {b}')
        cells.append(dict(x=max(0,b[0]-2),y=max(0,b[1]-2),width=min(w,b[2]+2)-max(0,b[0]-2),height=min(h,b[3]+2)-max(0,b[1]-2)))
    return cells

selected={}
for path in sorted((folder/'results').glob('*.json')):
    job=json.loads(path.read_text(encoding='utf-8'))
    if job.get('revision',1)>=selected.get(job['key'],{}).get('revision',0): selected[job['key']]=job
if args.key and args.key not in selected: parser.error('Unknown appearance: '+args.key)
reports=[]
failures=[]
for key,job in selected.items():
    if job['id'].endswith('-3d'): continue
    if args.key and args.key!=key: continue
    print('Inspecting '+key,flush=True)
    source=Path(job['source'])
    with Image.open(source) as im:
        im.load()
        if im.mode!='RGBA': raise ValueError(key+': RGBA required')
        transparent=im.getchannel('A').histogram()[0]/(im.width*im.height)
        if transparent<.25: raise ValueError(key+': alpha missing')
        try:
            cells=regions(im)
        except ValueError as error:
            failures.append(dict(key=key,error=str(error)))
            print('REJECT '+key+': '+str(error),flush=True)
            continue
        signatures={hashlib.sha256(im.crop((c['x'],c['y'],c['x']+c['width'],c['y']+c['height'])).tobytes()).hexdigest() for c in cells}
        if len(signatures)!=16: raise ValueError(key+': duplicate poses')
        dimensions=im.size
    ratios=[1,1,1,.76,.82,.78,.60,.49,.34,.62,.62,.62,1,.94,.94,1]
    def clip(order,times,loop=True,props=False):
        result=dict(file=Path(job['dest']).relative_to(Path('src/DesktopPet.App/Assets/Characters')/job['id']).as_posix(),columns=4,rows=4,frames=order,frameMs=times,loop=loop,cells=cells,heightRatios=ratios)
        if props: result['bakedProps']=True
        return result
    motions={
        'listen':clip([0],[5000],False),
        'chat':clip([0,1,0,1],[250,340,220,310]),
        'think':clip([2],[5000],False),
        'jump':clip([3,4,5,0],[240,640,240,200],False),
        'land':clip([5,0],[250,160],False),
        'curl':clip([6,7],[600,2400],False),
        'sleep':clip([8],[5000],False),
        'build':clip([9,10,11],[850,1000,1550],False,True),
        'pickup':clip([12,13],[220,5000],False),
        'ball-ready':clip([0],[5000],False),
        'anticipate':clip([0],[5000],False),
        'ball-hit':clip([14,0],[650,300],False),
        'ball-miss':clip([15,0],[1000,300],False),
    }
    if args.install:
        target=root/job['dest']; target.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(source,target)
        path=root/'src/DesktopPet.App/Assets/Characters'/job['id']/'pet.json'
        data=json.loads(path.read_text(encoding='utf-8-sig'))
        target_motions=data['motions'] if job['outfit']=='original' else data['outfits'][job['outfit']]['motions']
        target_motions.update(motions)
        write_json(path,data)
    reports.append(dict(key=key,revision=job.get('revision',1),file=job['dest'],source=str(source),dimensions=dimensions,cells=cells,heightRatios=ratios,sha256=hashlib.sha256(source.read_bytes()).hexdigest(),transparentFraction=round(transparent,4)))
path=folder/'manifest.json'
if args.install:
    combined={r['key']:r for r in json.loads(path.read_text(encoding='utf-8'))} if path.exists() else {}
    combined.update((r['key'],r) for r in reports)
    write_json(path,[combined[k] for k in sorted(combined)])
(root/'artifacts').mkdir(exist_ok=True)
write_json(root/'artifacts/interaction-art-audit.json',dict(accepted=reports,rejected=failures))
print(f'{len(reports)}/24 appearance sheets inspected'+(' and installed.' if args.install else '.'))
if failures: raise SystemExit(1)
