"""Install reviewed original PNGs and measured crop/contact metadata, never edit pixels."""
import argparse, hashlib, json, shutil
from pathlib import Path
import cv2
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'artwork/interaction-five'
PACKS=ROOT/'src/DesktopPet.App/Assets/Characters'
DEMO=ROOT/'docs/demo/interaction-five'
NAMES=['neutral','high-prep','high-ready','high-contact','high-recoil','fist-up','fist-mid','fist-down','reveal-rock','reveal-scissors','reveal-paper','receive','gift','gift-empty','read','page','read-finish','photo','lift','happy']

def sprite(file,poses,ref):
    n=len(poses)
    cols=4 if n%4==0 else 3
    return dict(file=file,columns=cols,rows=n//cols,cells=[dict(zip(['x','y','width','height'],p['rect'])) for p in poses],referenceHeightPixels=ref,bakedProps=True)

def install(character,outfit,sources,poses):
    folder=PACKS/character
    dest=folder/'interactions'/outfit;dest.mkdir(parents=True,exist_ok=True)
    atlases={}
    for atlas,src in sources.items():
        source=Path(src['path']);target=dest/source.name
        shutil.copy2(source,target)
        assert hashlib.sha256(source.read_bytes()).digest()==hashlib.sha256(target.read_bytes()).digest()
        atlases[atlas]=sprite(target.relative_to(folder).as_posix(),src['poses'],src['referenceHeight'])
    result={'atlases':atlases,'poses':{}}
    for name,p in poses.items():
        result['poses'][name]=dict(atlas=p['atlas'],frame=p['frame'],footX=round(p['foot'][0]-p['rect'][0],3),footY=round(p['foot'][1]-p['rect'][1],3),anchors={k:dict(x=v[0],y=v[1]) for k,v in p.get('anchors',{}).items()},frontY=p.get('frontY',0),boxWidth=p.get('boxWidth',0))
    assert set(result['poses'])==set(NAMES)
    manifest=folder/'pet.json';data=json.loads(manifest.read_text(encoding='utf-8-sig'))
    target=data if outfit=='original' else data['outfits'][outfit]
    target['interactionFive']=result
    encoded=json.dumps(data,ensure_ascii=False,separators=(',',':'))+'\n'
    assert len(encoded.encode())<262144
    manifest.write_text(encoded,encoding='utf-8')
    return {'character':character,'outfit':outfit,'poseCount':len(poses),'sources':[{'file':x['file'],'sha256':hashlib.sha256((folder/x['file']).read_bytes()).hexdigest()} for x in atlases.values()]}

def approved_deepseek():
    geom=json.loads((DEMO/'art/geometry.json').read_text(encoding='utf-8'))
    reports=[]
    for style,char in [('chibi','whale'),('realistic','deepseek-adult')]:
        data=geom['styles'][style];source_poses={};poses={};sources={}
        for name in NAMES:
            p=dict(data['poses'][name if name in data['poses'] else 'neutral'])
            atlas=p.get('atlas',style)
            if style=='chibi' and not p.get('anchors'):
                fallback={'high-prep':('hand',[-90,-231]),'receive':('receive',[-3,-113]),'gift':('gift',[5,-113]),'read':('book',[0,-126]),'page':('book',[0,-126])}.get(name)
                if fallback:
                    source_ref=(data if atlas==style else geom['supplements'][atlas])['referenceHeight']
                    p['anchors']={fallback[0]:[round(v*source_ref/data['referenceHeight'],3) for v in fallback[1]]}
            source_poses.setdefault(atlas,[])
            p['frame']=len(source_poses[atlas]);p['atlas']=atlas
            source_poses[atlas].append(p);poses[name]=p
        # Duplicate aliases are retained to give each atlas a valid rectangular cell count.
        for atlas,plist in source_poses.items():
            s=data if atlas==style else geom['supplements'][atlas]
            while len(plist)%4:plist.append(dict(plist[-1]))
            sources[atlas]=dict(path=str(DEMO/s['file']),poses=plist,referenceHeight=s['referenceHeight'])
        reports.append(install(char,'original',sources,poses))
    return reports

def measure(source,names,rows):
    pix=np.asarray(Image.open(source).convert('RGBA'));h,w=pix.shape[:2]
    assert (pix[:,:,3]==0).mean()>.20,(source,'missing alpha')
    n,labels,stats,_=cv2.connectedComponentsWithStats((pix[:,:,3]>70).astype('uint8'),8)
    parts=[(i,list(map(int,s))) for i,s in enumerate(stats) if i and s[4]>max(1000,w*h/len(names)*.04) and s[3]>h/rows*.55]
    assert len(parts)==len(names),(source,'body count',len(parts),len(names))
    parts.sort(key=lambda p:p[1][1]+p[1][3]/2)
    cols=len(names)//rows;ordered=[]
    for row in range(rows):ordered.extend(sorted(parts[row*cols:(row+1)*cols],key=lambda p:p[1][0]))
    heights=[s[3] for _,s in ordered];reference=float(np.median(heights))
    assert max(heights)/min(heights)<1.075,(source,'size variation',heights)
    result={}
    for name,(idx,(x,y,bw,bh,area)) in zip(names,ordered):
        _,xs=np.nonzero(labels[y+bh-max(5,round(bh*.025)):y+bh,x:x+bw]==idx)
        foot=[round(x+float(np.median(xs)),2),y+bh]
        left=max(0,x-2);top=max(0,y-2);right=min(w,x+bw+2);bottom=min(h,y+bh+2)
        # Padding must not pull pixels from a neighbouring pose into this frame.
        neighbours=[i for i,s in enumerate(stats) if i and i!=idx and s[4]>1000]
        region=labels[top:bottom,left:right]
        if neighbours and np.isin(region,neighbours).any():
            left,top,right,bottom=x,y,x+bw,y+bh
        result[name]=dict(rect=[left,top,right-left,bottom-top],bounds=[x,y,bw,bh],foot=foot,anchors={})
    return result,reference

def selected_jobs():
    selection=ART/'selected-expansion.json'
    if not selection.exists():return []
    reports=[]
    for entry in json.loads(selection.read_text(encoding='utf-8')):
        poses={};sources={}
        for group in entry['groups']:
            path=ART/group['file'];group_poses,ref=measure(path,group['poses'],group.get('rows',1))
            atlas=group['key'];ordered=list(group_poses.values())
            for i,(name,p) in enumerate(group_poses.items()):
                p['atlas']=atlas;p['frame']=i
                spec=entry['anchors'].get(name,{})
                x,y,w,h=p['bounds'];fx,fy=p['foot']
                for key,(ax,ay) in spec.get('anchors',{}).items():p['anchors'][key]=[round(x+w*ax-fx,2),round(y+h*ay-fy,2)]
                if 'frontY' in spec:p['frontY']=round(y+h*spec['frontY']-fy,2)
                if 'boxWidth' in spec:p['boxWidth']=round(w*spec['boxWidth'],2)
                poses[name]=p
            sources[atlas]=dict(path=str(path),poses=ordered,referenceHeight=ref)
        reports.append(install(entry['character'],entry['outfit'],sources,poses))
    return reports

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--approved-only',action='store_true');args=parser.parse_args()
    reports=approved_deepseek()
    if not args.approved_only:reports+=selected_jobs()
    (ART/'installation.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'installed':len(reports),'appearances':[r['character']+'/'+r['outfit'] for r in reports]}))
