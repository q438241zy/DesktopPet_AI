"""Audit untouched generated atlases and author outfit-specific playback metadata."""
import argparse, hashlib, json, shutil
from pathlib import Path
import cv2
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[1]
folder=root/'artwork/chibi-continuity'
parser=argparse.ArgumentParser();parser.add_argument('--install',action='store_true');parser.add_argument('--key');args=parser.parse_args()

def inspect(source,columns,rows,kind):
    with Image.open(source) as im:
        im.load()
        if im.mode!='RGBA':raise ValueError('RGBA required')
        alpha=np.asarray(im)[:,:,3]
        if np.mean(alpha==0)<.2:raise ValueError('Transparent gutters missing')
        count=columns*rows
        # Hair antialiasing can join two otherwise separate opaque figures.
        # Use the lowest alpha threshold that separates every figure; retain all
        # softer pixels at runtime by growing ownership back into that fringe.
        for separation in [48,96,160,200,240]:
            _,_,stats,_=cv2.connectedComponentsWithStats((alpha>=separation).astype(np.uint8),8)
            parts=sorted(stats[1:].tolist(),key=lambda p:p[4],reverse=True)
            if len(parts)>=count and parts[count-1][4]>=im.width*im.height*.003:break
        else:raise ValueError('Opaque figures overlap; redraw required')
        figures=parts[:count]
        figures.sort(key=lambda p:p[1]+p[3]/2)
        groups=[]
        for p in figures:
            cy=p[1]+p[3]/2
            if not groups or cy-(groups[-1][-1][1]+groups[-1][-1][3]/2)>im.height/rows*.46:groups.append([])
            groups[-1].append(p)
        if len(groups)!=rows or any(len(g)!=columns for g in groups):raise ValueError(f'Unexpected grid {[len(g) for g in groups]}')
        figures=[p for g in groups for p in sorted(g,key=lambda p:p[0]+p[2]/2)]
        boxes=[[x,y,x+w,y+h] for x,y,w,h,_ in figures]
        def distance(a,b):return max(a[0]-b[2],b[0]-a[2],0)**2+max(a[1]-b[3],b[1]-a[3],0)**2
        for x,y,w,h,n in parts[count:]:
            if n<12:continue
            p=[x,y,x+w,y+h];i=min(range(count),key=lambda j:distance(p,boxes[j]))
            if distance(p,boxes[i])<(im.height/rows*.3)**2:
                b=boxes[i];boxes[i]=[min(b[0],p[0]),min(b[1],p[1]),max(b[2],p[2]),max(b[3],p[3])]
        cells=[]
        for i,(x,y,r,b) in enumerate(boxes):
            margin=3 if separation==48 else 12
            x=max(0,x-margin);y=max(0,y-margin);r=min(im.width,r+margin);b=min(im.height,b+margin)
            if r-x>im.width/columns*1.3 or b-y>im.height/rows*1.32:raise ValueError(f'Figure {i} crosses adjacent cells')
            cells.append(dict(x=x,y=y,width=r-x,height=b-y))
        hashes=[hashlib.sha256(im.crop((c['x'],c['y'],c['x']+c['width'],c['y']+c['height'])).tobytes()).hexdigest() for c in cells]
        if len(set(hashes))!=count:raise ValueError('Repeated source frames')
        reference=list(range(20)) if kind=='care' else [1,12,13,14,15,16,17,18,19] if kind=='body' else list(range(count))
        body_height=float(np.median([figures[i][3] for i in reference]))
        # Visible height ratios preserve a single pixel scale across the sheet.
        # A curled/lying pose becomes shorter without shrinking its head.
        ratios=[round((b[3]-b[1])/body_height,6) for b in boxes]
        if not all(.15<=r<=1.5 for r in ratios):raise ValueError('Pose scale outside supported range')
        return cells,ratios,im.size,float(np.mean(alpha==0)),separation,body_height

def motions(kind):
    if kind=='touch':return {'poke':([0,1,2,3],[260,260,260,420],True),'tickle':([4,5,6,7],[140,180,160,360],True),'ball-ready':([8,9,10,11],[320,360,320,800],True)}
    if kind=='care':return {'chat':([0,1,2,3],[200,140,240,360],True),'think':([4,5,6,7],[200,250,350,1200],True),'headpat':([8,9,10,11],[240,320,520,520],True),'poke':([12,13,14,15],[260,260,260,420],True),'tickle':([16,17,18,19],[140,180,160,360],True),'bonk':([20,21,22,23],[400,150,450,900],False)}
    if kind=='body':return {'jump':([0,1,2,3],[240,120,520,440],False),'curl':([4,5,6,7],[380,520,650,5000],False),'sleep':([8,9,10,11],[280,400,520,5000],False),'pickup':([12,13,14,15],[100,140,180,5000],False),'ball-ready':([16,17,18,19],[320,360,320,800],True),'ball-hit':([20,21],[180,1320],False),'ball-miss':([22,23],[300,1500],False)}
    return {'eat':(list(range(8)),[200,300,380,360,320,380,400,660],False),'meal':(list(range(8,16)),[340,480,500,400,440,480,600,960],False)}

reports=[];failures=[]
for p in sorted((folder/'results').glob('*.json')):
    j=json.loads(p.read_text(encoding='utf-8-sig'))
    if args.key and args.key!=j['key']:continue
    try:
        source=Path(j['source']);kind=j['action'];columns=j.get('columns',4);rows=j['rows']
        base=Path('motions') if j['outfit']=='original' else Path('outfits')/j['outfit']
        relative=(base/(kind+'-continuity.png')).as_posix()
        target=root/'src/DesktopPet.App/Assets/Characters'/j['id']/relative
        backup=root/'.artifacts/chibi-continuity/sources'/(j['key']+'.png')
        if not source.exists():source=backup if backup.exists() else target
        cells,ratios,dimensions,transparent,separation,body_height=inspect(source,columns,rows,kind)
        sprites={}
        for action,(frames,times,loop) in motions(kind).items():
            # The chin-rest drawing has an open mouth. Select the calm neutral
            # pose instead so this quiet thinking clip never resembles speech.
            if j['key']=='claude-wedding-care' and action=='think':frames=[4,8,6,7]
            sprites[action]=dict(file=relative,columns=columns,rows=rows,cells=cells,frames=frames,frameMs=times,loop=loop,isolateCells=True,separationAlpha=separation,heightRatios=ratios,referenceHeightPixels=body_height)
            if action in ('eat','meal','bonk'):sprites[action]['bakedProps']=True
        if args.install:
            target.parent.mkdir(parents=True,exist_ok=True)
            if source.resolve()!=target.resolve():shutil.copyfile(source,target)
            backup.parent.mkdir(parents=True,exist_ok=True)
            if source.resolve()!=backup.resolve():shutil.copyfile(source,backup)
            manifest=target.parents[1]/'pet.json' if j['outfit']=='original' else target.parents[2]/'pet.json'
            c=json.loads(manifest.read_text(encoding='utf-8'))
            current=c['motions'] if j['outfit']=='original' else c['outfits'][j['outfit']]['motions']
            current.update(sprites)
            manifest.write_text(json.dumps(c,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        reports.append(dict(key=j['key'],id=j['id'],outfit=j['outfit'],kind=kind,file=target.relative_to(root).as_posix(),source=str(source),sha256=hashlib.sha256(source.read_bytes()).hexdigest(),dimensions=dimensions,cells=cells,heightRatios=ratios,separationAlpha=separation,referenceHeightPixels=body_height,transparentFraction=round(transparent,4),actions=list(sprites)))
    except Exception as error:failures.append(dict(key=j['key'],error=str(error)))
if args.install:
    output=folder/'manifest.json';old={r['key']:r for r in json.loads(output.read_text())} if output.exists() else {}
    old.update((r['key'],r) for r in reports);output.write_text(json.dumps(list(old.values()),ensure_ascii=False,indent=2),encoding='utf-8')
(root/'artifacts/chibi-continuity-audit.json').write_text(json.dumps(dict(accepted=reports,rejected=failures),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(accepted=len(reports),rejected=failures),ensure_ascii=False))
if failures:raise SystemExit(1)
