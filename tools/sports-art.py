"""Install reviewed imagegen originals; measure crops and flow, never edit bitmap files."""
import argparse, base64, gzip, hashlib, json, re, shutil
from pathlib import Path
import cv2
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'src/DesktopPet.App/Assets/Characters'
RESULTS=ROOT/'artwork/sports/results'
SIZE,FS=288,72

def write_manifest(path,pet):
    # Keep the existing 256 KiB pack limit. Compact repeated crop objects, rather
    # than increasing the untrusted-import limit for every new wardrobe entry.
    text=json.dumps(pet,ensure_ascii=False,indent=2)
    text=re.sub(r'\{\s+"x": (\d+),\s+"y": (\d+),\s+"width": (\d+),\s+"height": (\d+)\s+\}',
        lambda m:'{"x": '+m[1]+', "y": '+m[2]+', "width": '+m[3]+', "height": '+m[4]+'}',text)
    if len(text.encode('utf-8'))>262144:raise ValueError(f'{path}: manifest exceeds 256 KiB')
    path.write_text(text+'\n',encoding='utf-8')

def inspect(source,count,columns,rows,row_counts=None,indices=None):
    rgba=np.asarray(Image.open(source).convert('RGBA'));h,w=rgba.shape[:2]
    if (rgba[:,:,3]==0).mean()<.2:raise ValueError('Transparent alpha/gutters missing')
    for threshold in [80,120,180,220]:
        _,labels,stats,_=cv2.connectedComponentsWithStats((rgba[:,:,3]>=threshold).astype('uint8'),8)
        parts=sorted([(i,tuple(map(int,s))) for i,s in enumerate(stats) if i and s[4]>w*h*.0008],key=lambda v:v[1][4],reverse=True)
        if len(parts)>=count and parts[count-1][1][4]>w*h*.0015:break
    else:raise ValueError(f'Expected {count} isolated figures, found {len(parts)}')
    main=parts[:count]
    # Each authored grid row owns the next N silhouettes. Compare centres to
    # allow sleeping/seated poses to be shorter on the same foot baseline.
    main.sort(key=lambda v:v[1][1]+v[1][3])
    ordered=[]
    cursor=0
    for n in row_counts or [columns]*rows:
        ordered.extend(sorted(main[cursor:cursor+n],key=lambda v:v[1][0]+v[1][2]/2));cursor+=n
    # Include loose blocks/ornaments belonging to each character in the measured
    # crop. Raster originals stay intact; ownership is used only for flow.
    for label,(x,y,bw,bh,area) in parts[count:]:
        cx,cy=x+bw/2,y+bh/2
        nearest=min(range(len(ordered)),key=lambda i:((cx-(ordered[i][1][0]+ordered[i][1][2]/2))/(w/columns))**2+((cy-(ordered[i][1][1]+ordered[i][1][3]*.75))/(h/rows))**2)
        main_label,(mx,my,mw,mh,ma)=ordered[nearest]
        if abs(cx-(mx+mw/2))<w/columns*.65 and my-bh<cy<my+mh+bh:
            labels[labels==label]=main_label
            left,top,right,bottom=min(x,mx),min(y,my),max(x+bw,mx+mw),max(y+bh,my+mh)
            ordered[nearest]=(main_label,(left,top,right-left,bottom-top,ma+area))
    if indices is not None:ordered=[ordered[i] for i in indices]
    cells=[]
    for label,(x,y,bw,bh,area) in ordered:
        if x<=0 or y<=0 or x+bw>=w or y+bh>=h:raise ValueError('Figure touches canvas edge')
        if bw>w/columns*1.45 or bh>h/rows*1.3:raise ValueError('Merged figure or row: review component bounds')
        cells.append(dict(x=max(0,x-3),y=max(0,y-3),width=min(w,x+bw+3)-max(0,x-3),height=min(h,y+bh+3)-max(0,y-3)))
    return rgba,ordered,labels,cells,threshold

def clips(kind):
    if kind=='portrait':
        return {'idle':([0],[5000],False),'listen':([0],[5000],False),
            'headpat':([0,1,2,1,0],[200,450,900,550,500],False),
            'poke':([0,3,4,3,0],[250,500,700,500,550],False),
            'tickle':([0,5,6,5,6,0],[180,300,500,300,620,300],False),
            'happy':([0,7,0],[350,1200,350],False),'sad':([0,8,0],[350,1200,350],False),
            'dizzy':([0,9,0],[600,800,600],True),
            'bonk':([10,11,10,0],[480,600,350,470],False)}
    if kind=='body':
        return {'chat':([0,1,0,1,0],[240,400,400,420,320],True),'think':([2],[5000],False),
            'jump':([3,4,5,0],[240,640,240,200],False),'land':([5,0],[250,160],False),
            'curl':([6,7],[600,5000],False),'sleep':([8],[5000],False),
            'build':([9,10,11],[1000,1500,1900],False),'pickup':([12,13],[220,5000],False),
            'ball-miss':([15,0],[1200,600],False)}
    if kind=='catch':
        return {'eat':([0,1,2,3,0],[400,600,700,900,400],False),
            'meal':([4,5,6,7,4],[600,850,1100,1150,500],False),
            'ball-ready':([8],[5000],False),'anticipate':([8],[5000],False),
            'ball-hit':([9,10,11],[120,380,1000],False),'ball-hold':([11],[5000],False)}
    if kind=='core':
        return {
          'idle':([0],[5000],False), 'listen':([0],[5000],False),
          'chat':([0,1,0,1,0],[260,420,360,520,300],True),
          'think':([0,2,2,0],[300,450,1700,350],False),
          'headpat':([0,3,4,3,0],[180,480,820,420,360],False),
          'poke':([0,5,6,5,0],[250,550,650,500,350],False),
          'tickle':([0,7,8,7,8,0],[180,300,420,300,650,350],False),
          'happy':([0,8,0],[350,800,350],False), 'sad':([0,21,0],[350,1000,350],False),
          'jump':([0,9,10,11,0],[120,220,620,260,100],False), 'land':([11,0],[260,200],False),
          'curl':([0,12,13],[400,650,4450],False), 'sleep':([14,15],[700,5000],False),
          'pickup':([16,17],[250,5000],False),
          'ball-ready':([18],[5000],False),'anticipate':([18],[5000],False),
          'ball-hit':([18,19,20],[160,300,1040],False),'ball-hold':([20],[5000],False),
          'ball-miss':([18,21,0],[350,900,550],False),
          'bonk':([22,23,22,0],[480,600,350,470],False),
          'dizzy':([16,17,16],[500,700,500],True),'farewell':([0,1,0],[250,1400,350],False)}
    if kind=='contact':
        return {'eat':(list(range(6)),[300,450,450,650,650,500],False),
                'meal':(list(range(6,12)),[400,600,650,800,950,800],False),
                'build':(list(range(12,18)),[450,700,850,900,900,600],False),
                'comb':([18,19,20,19,20,18],[450,750,900,750,1100,850],False),
                'wipe':([21,22,23,22,23,21],[400,600,850,600,1050,700],False)}
    if kind=='walk':return {'walk':(list(range(12)),[95]*12,True)}
    return {}

def measure(source,columns,rows,definitions,reference_index=0,reference_groups=None,row_counts=None,indices=None,reference_pixels=0):
    rgba,ordered,labels,cells,threshold=inspect(source,sum(row_counts) if row_counts else columns*rows,columns,rows,row_counts,indices)
    count=len(cells);normalized={};poses=[]
    tracker=cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM);tracker.setFinestScale(0)
    for i,((label,(x,y,w,h,area)),cell) in enumerate(zip(ordered,cells)):
        reference=reference_pixels or ordered[reference_groups[i] if reference_groups else reference_index][1][3]
        l,t,cw,ch=[cell[k] for k in ('x','y','width','height')]
        mask=labels==label
        _,xs=np.nonzero(mask[y:y+max(3,round(h*.25)),x:x+w])
        anchor=x+float(np.mean(xs)) if len(xs) else x+w/2
        factor=SIZE*.7/reference
        dx=SIZE*.5-(anchor-l)*factor;dy=SIZE*.92-(y+h-t)*factor
        poses.append({'rect':[dx/SIZE,dy/SIZE,cw*factor/SIZE,ch*factor/SIZE], 'pixels':[l,t,cw,ch], 'referenceHeight':reference})
        crop=rgba[t:t+ch,l:l+cw];ownership=cv2.dilate(mask[t:t+ch,l:l+cw].astype('uint8'),np.ones((5,5),np.uint8))
        alpha=crop[:,:,3:4].astype(np.float32)/255*ownership[:,:,None]
        rgb=crop[:,:,:3]*alpha+220*(1-alpha)
        temp=cv2.warpAffine(rgb,np.float32([[factor,0,dx],[0,factor,dy]]),(SIZE,SIZE),flags=cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT,borderValue=(220,220,220))
        normalized[i]=cv2.cvtColor(np.uint8(np.clip(temp,0,255)),cv2.COLOR_RGB2GRAY)
    pairs=set()
    for frames,_,loop in definitions.values():
        pairs.update((a,b) for a,b in zip(frames,frames[1:]) if a!=b)
        if loop and frames[-1]!=frames[0]:pairs.add((frames[-1],frames[0]))
    flows={}
    for a,b in sorted(pairs):
        fields=[]
        for first,second in [(a,b),(b,a)]:
            field=tracker.calc(normalized[first],normalized[second],None)
            small=cv2.resize(field,(FS,FS),interpolation=cv2.INTER_AREA)/SIZE
            encoded=np.uint8(np.clip(np.rint(small/.6*255+128),0,255))
            fields.append(base64.b64encode(gzip.compress(encoded.tobytes(),compresslevel=9)).decode())
        flows[f'{a}:{b}']=fields
    return dict(version=2,sha256=hashlib.sha256(Path(source).read_bytes()).hexdigest(),flowSize=FS,poses=poses,flow=flows),cells,threshold

def install(job):
    source=Path(job['source']);kind=job['kind'];folder=ASSETS/job['id']
    columns=job.get('columns',4);rows=job.get('rows',3 if kind=='walk' else 6)
    definitions=job.get('clips') or clips(kind)
    if kind=='club' and not job.get('clips'):
        definitions={key:(list(range(offset,offset+8)),[500]*8,key=='bubbles') for key,offset in [('stars',0),('bubbles',8),('stretch',16)]}
    outfits=job.get('outfits',['sports'])
    if kind=='care' and not job.get('clips'):
        definitions={}
        for o in range(len(outfits)):
            definitions[f'{o}:comb']=([o*8+i for i in [0,1,2,1,2,3]],[450,750,900,750,1100,850],False)
            definitions[f'{o}:wipe']=([o*8+i for i in [4,5,6,5,6,7]],[400,600,850,600,1050,700],False)
    count=len(job.get('indices',[])) or sum(job.get('rowCounts',[])) or columns*rows
    data,cells,threshold=measure(source,columns,rows,definitions,reference_index=job.get('referenceIndex',0),reference_groups=job.get('referenceGroups',[i//8*8 for i in range(count)] if kind in ['club','care'] else None),row_counts=job.get('rowCounts'),indices=job.get('indices'),reference_pixels=job.get('referenceHeightPixels',0))
    sprite_columns,sprite_rows=next((c,len(cells)//c) for c in [4,3,2,6,1,5] if len(cells)%c==0 and len(cells)//c<=6)
    target=ROOT/job['dest'];target.parent.mkdir(parents=True,exist_ok=True)
    if target.resolve()!=source.resolve():shutil.copyfile(source,target)
    target.with_suffix('.json').write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
    manifest=folder/'pet.json';pet=json.loads(manifest.read_text(encoding='utf-8-sig'))
    relative=target.relative_to(folder).as_posix()
    for action,(frames,times,loop) in definitions.items():
        outfit=outfits[int(action.split(':')[0])] if kind=='care' else 'sports'
        if kind=='care':action=action.split(':')[1]
        appearance=pet if outfit=='original' else pet['outfits'].setdefault(outfit,dict(name='短袖运动服',motions={}))
        if outfit=='sports':appearance['name']='短袖运动服'
        sprite=dict(file=relative,columns=sprite_columns,rows=sprite_rows,cells=cells,frames=frames,frameMs=times,loop=loop,isolateCells=True,separationAlpha=threshold,referenceHeightPixels=data['poses'][frames[0] if kind in ['club','care'] else 0]['referenceHeight'])
        if action=='walk' and job.get('walkStride'):
            if not .1<=job['walkStride']<=1.2:raise ValueError(f'{job["key"]}: invalid measured walkStride')
            sprite['walkStride']=job['walkStride']
        if action in ['eat','meal','build','comb','wipe','bonk','bubbles']:sprite['bakedProps']=True
        if action in ['ball-ready','anticipate','ball-hold']:
            sprite['hands']=[None]*len(cells)
            for index in frames:sprite['hands'][index]=dict(height=.76 if job['style']=='chibi' else .54,offset=0,span=.15 if job['style']=='chibi' else .095)
        if action in job.get('hands',{}):
            if len(job['hands'][action])!=len(cells):raise ValueError(f'{job["key"]}: hands length mismatch for {action}')
            sprite['hands']=job['hands'][action]
        if action=='bubbles' and job.get('bubbleSources'):
            if len(job['bubbleSources'])!=len(cells):raise ValueError(f'{job["key"]}: bubbleSources length mismatch')
            sprite['bubbleSources']=job['bubbleSources']
        if job.get('effectAnchors'):
            if len(job['effectAnchors'])!=len(cells):raise ValueError(f'{job["key"]}: effectAnchors length mismatch')
            sprite['effectAnchors']=job['effectAnchors']
        if action=='idle':appearance['idle']=sprite
        appearance['motions'][action]=sprite
    write_manifest(manifest,pet)
    return dict(key=job['key'],id=job['id'],kind=kind,file=job['dest'],sha256=data['sha256'],columns=columns,rows=rows,cells=cells,transitions=len(data['flow']))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--install',action='store_true');parser.add_argument('--key');args=parser.parse_args()
    selected={}
    for path in RESULTS.glob('*.json'):
        j=json.loads(path.read_text(encoding='utf-8-sig'))
        if j.get('selected'):selected[(j['id'],j['kind'])]=j
    report=[];failed=[]
    for j in selected.values():
        if args.key and args.key!=j['key']:continue
        try:
            if args.install:report.append(install(j))
            else:inspect(j['source'],j.get('columns',4)*j.get('rows',6),j.get('columns',4),j.get('rows',6))
            print('PASS',j['key'],flush=True)
        except Exception as e:failed.append(dict(key=j['key'],error=str(e)));print('FAIL',j['key'],str(e),flush=True)
    if args.install:
        out=ROOT/'artwork/sports/coverage.json';old={r['key']:r for r in json.loads(out.read_text())} if out.exists() else {};old.update((r['key'],r) for r in report);out.write_text(json.dumps(list(old.values()),indent=2)+'\n',encoding='utf-8')
    if failed:raise SystemExit(1)
