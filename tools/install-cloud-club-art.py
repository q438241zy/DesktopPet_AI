"""Install original selected artwork and measure continuous motion metadata.

Never modify source raster pixels. Temporary arrays measure motion only; the
desktop textures the untouched images with isolated component ownership.
"""
import base64, gzip, hashlib, json, shutil, sys
from pathlib import Path
import cv2
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'src/DesktopPet.App/Assets/Characters'
SIZE,FS=288,72
tracker=cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
tracker.setFinestScale(0)
report=[]
for folder in sorted(ASSETS.iterdir()):
    if not (folder/'pet.json').exists(): continue
    ident=folder.name; style='realistic' if ident.endswith('-adult') else 'chibi'
    for outfit in ['original','swim','wedding']:
        target=folder/'motions'/f'cloud-club-{outfit}.png'
        if ident in ['whale','deepseek-adult']:
            file=f'actions-{style}-{outfit}'+('-v2' if style=='chibi' and outfit=='original' else '')+'.png'
            source=ROOT/'docs/demo/cloud-club/art'/file
            if not target.exists() or target.read_bytes()!=source.read_bytes(): shutil.copyfile(source,target)
        if not target.exists():
            if '--available' in sys.argv: continue
            raise FileNotFoundError(target)
        sha=hashlib.sha256(target.read_bytes()).hexdigest(); meta=target.with_suffix('.json')
        manifest=folder/'pet.json'; pet=json.loads(manifest.read_text(encoding='utf-8-sig'))
        if meta.exists() and json.loads(meta.read_text())['sha256']==sha and '--force' not in sys.argv:
            data=json.loads(meta.read_text())
        else:
            rgba=np.array(Image.open(target));ih,iw=rgba.shape[:2]
            assert rgba.shape[2]==4 and (rgba[:,:,3]==0).mean()>.15,(ident,outfit,'transparent alpha')
            _,labels,stats,_=cv2.connectedComponentsWithStats((rgba[:,:,3]>80).astype('uint8'),8)
            components=[(i,tuple(map(int,s))) for i,s in enumerate(stats) if i and s[4]>1000]
            if len(components)!=24 and '--available' in sys.argv:
                print('REJECTED',ident,outfit,'expected 24 isolated figures',len(components),flush=True);continue
            assert len(components)==24,(ident,outfit,'expected 24 isolated figures',len(components))
            cols=4 if iw<ih else 8
            components.sort(key=lambda v:v[1][1]+v[1][3]); ordered=[]
            for row in range(24//cols):ordered.extend(sorted(components[row*cols:(row+1)*cols],key=lambda v:v[1][0]+v[1][2]/2))
            owner=np.zeros((ih,iw),np.uint8)
            for i,(label,_) in enumerate(ordered):owner[labels==label]=i+1
            for i in range(24):
                edge=cv2.dilate((owner==i+1).astype('uint8'),np.ones((5,5),np.uint8))
                owner[(owner==0)&(edge>0)&(rgba[:,:,3]>0)]=i+1
            poses=[]; normalized={}
            for index,(label,(x,y,w,h,area)) in enumerate(ordered):
                reference=ordered[index//8*8][1][3]
                _,xs=np.nonzero(labels[y+h-max(3,round(reference*.05)):y+h,x:x+w]==label)
                footx=x+float(np.median(xs))
                l=max(0,x-2);t=max(0,y-2);r=min(iw,x+w+2);b=min(ih,y+h+2)
                c=rgba[t:b,l:r];factor=SIZE*.7/reference
                dx=SIZE*.5-(footx-l)*factor;dy=SIZE*.92-(y+h-t)*factor
                poses.append({'rect':[dx/SIZE,dy/SIZE,(r-l)*factor/SIZE,(b-t)*factor/SIZE],
                              'pixels':[l,t,r-l,b-t],'referenceHeight':reference})
                alpha=c[:,:,3:4].astype(np.float32)/255*((owner[t:b,l:r]==index+1)[:,:,None])
                rgb=c[:,:,:3]*alpha+220*(1-alpha)
                temp=cv2.warpAffine(rgb,np.float32([[factor,0,dx],[0,factor,dy]]),(SIZE,SIZE),flags=cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT,borderValue=(220,220,220))
                normalized[index]=cv2.cvtColor(np.uint8(np.clip(temp,0,255)),cv2.COLOR_RGB2GRAY)
            flows={}
            for action in range(3):
                for step in range(7):
                    a,b=action*8+step,action*8+step+1;fields=[]
                    for first,second in [(a,b),(b,a)]:
                        field=tracker.calc(normalized[first],normalized[second],None)
                        field[int(SIZE*.76):,:,1]=0
                        small=cv2.resize(field,(FS,FS),interpolation=cv2.INTER_AREA)/SIZE
                        encoded=np.uint8(np.clip(np.rint(small/.6*255+128),0,255))
                        fields.append(base64.b64encode(gzip.compress(encoded.tobytes(),compresslevel=9)).decode())
                    flows[str(a)]=fields
            data={'version':1,'sha256':sha,'flowSize':FS,'poses':poses,'flow':flows}
            meta.write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
        motions=pet['motions'] if outfit=='original' else pet['outfits'][outfit]['motions']
        for action,offset in [('stars',0),('bubbles',8),('stretch',16)]:
            motions[action]={'file':f'motions/cloud-club-{outfit}.png','columns':4,'rows':6,'frames':list(range(offset,offset+8)),
                'frameMs':[500]*8,'loop':action=='bubbles','bakedProps':action=='bubbles','isolateCells':True,'separationAlpha':80,
                'referenceHeightPixels':data['poses'][offset]['referenceHeight'],
                'cells':[dict(zip(['x','y','width','height'],p['pixels'])) for p in data['poses']]}
        manifest.write_text(json.dumps(pet,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        report.append({'id':ident,'outfit':outfit,'poses':24,'transitions':21,'sha256':sha})
        print(f'{ident}/{outfit}: installed 24 poses + 21 continuous transitions',flush=True)
out=ROOT/'artwork/cloud-club/coverage.json';out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('Installed',len(report),'appearances')
