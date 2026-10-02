"""Measure original PNG poses and motion correspondence; never edit raster assets.

Temporary pixel arrays are used only to measure displacement metadata. The browser
samples the untouched originals, using fixed scale within each eight-pose action.
"""
import base64, gzip, hashlib, json, sys
from pathlib import Path
import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'docs/demo/cloud-club/art'
OUT = Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'Release/win-x64/Demo/CloudClub/poses.js'
SIZE, FS = 288, 72
images, poses, flow, report, owners = {}, {}, {}, [], {}
tracker=cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
tracker.setFinestScale(0)
for style in ['chibi','realistic']:
    cols=4 if style=='chibi' else 8
    for outfit in ['original','swim','wedding']:
        key=f'{style}-{outfit}'
        file=f'actions-{key}'+('-v2' if key=='chibi-original' else '')+'.png'
        p=ART/file; rgba=np.array(Image.open(p)); ih,iw=rgba.shape[:2]
        assert rgba.shape[2]==4 and (rgba[:,:,3]==0).mean()>.15,file
        count,labels,stats,_=cv2.connectedComponentsWithStats((rgba[:,:,3]>80).astype('uint8'),8)
        components=[(i,tuple(map(int,v))) for i,v in enumerate(stats) if i and v[4]>1000]
        assert len(components)==24,(file,'expected 24 isolated character poses',len(components))
        # Row membership follows foot baseline, so raised hands cannot swap rows.
        components.sort(key=lambda v:v[1][1]+v[1][3])
        ordered=[]
        for row in range(24//cols):ordered.extend(sorted(components[row*cols:(row+1)*cols],key=lambda v:v[1][0]+v[1][2]/2))
        # Component ownership is render metadata, not a modified colour image.
        # A raised hand's bounding rectangle may include another row's shoe tips.
        owner=np.zeros((ih,iw),np.uint8)
        for index,(label,_) in enumerate(ordered):owner[labels==label]=index+1
        for index in range(24):
            edge=cv2.dilate((owner==index+1).astype('uint8'),np.ones((5,5),np.uint8))
            owner[(owner==0)&(edge>0)&(rgba[:,:,3]>0)]=index+1
        owners[key]={'size':[iw,ih],'data':base64.b64encode(gzip.compress(owner.tobytes(),compresslevel=9)).decode()}
        normalized={}; images[key]='data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode()
        references=[]
        for action in range(3):
            # First frame is neutral. No frame-dependent body resizing.
            reference=ordered[action*8][1][3];references.append(reference)
            for step in range(8):
                index=action*8+step; label,(x,y,w,h,area)=ordered[index]
                low=labels[y+h-max(3,round(reference*.05)):y+h,x:x+w]
                _,xs=np.nonzero(low==label)
                footx=x+float(np.median(xs))
                # Include antialiasing within a small transparent gutter.
                l=max(0,x-2);t=max(0,y-2);r=min(iw,x+w+2);b=min(ih,y+h+2)
                c=rgba[t:b,l:r];factor=SIZE*.7/reference
                dx=SIZE*.5-(footx-l)*factor;dy=SIZE*.92-(y+h-t)*factor
                name=f'{key}:{index}'
                poses[name]={'atlas':key,'cell':[l/iw,t/ih,(r-l)/iw,(b-t)/ih],
                             'rect':[dx/SIZE,dy/SIZE,(r-l)*factor/SIZE,(b-t)*factor/SIZE],
                             'pixels':[l,t,r-l,b-t],'referenceHeight':reference,'owner':index+1}
                a=c[:,:,3:4].astype(np.float32)/255
                a*=((owner[t:b,l:r]==index+1)[:,:,None])
                rgb=c[:,:,:3]*a+220*(1-a)
                matrix=np.float32([[factor,0,dx],[0,factor,dy]])
                temp=cv2.warpAffine(rgb,matrix,(SIZE,SIZE),flags=cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT,borderValue=(220,220,220))
                normalized[index]=cv2.cvtColor(np.uint8(np.clip(temp,0,255)),cv2.COLOR_RGB2GRAY)
        for action in range(3):
            for step in range(7):
                a,b=action*8+step,action*8+step+1;fields=[]
                for first,second in [(a,b),(b,a)]:
                    field=tracker.calc(normalized[first],normalized[second],None)
                    # Feet remain planted; no vertical swimming of shoes or hems.
                    field[int(SIZE*.76):,:,1]=0
                    small=cv2.resize(field,(FS,FS),interpolation=cv2.INTER_AREA)/SIZE
                    pixels=np.zeros((FS,FS,4),np.uint8)
                    pixels[:,:,:2]=np.uint8(np.clip(np.rint(small/.6*255+128),0,255));pixels[:,:,3]=255
                    fields.append(base64.b64encode(gzip.compress(pixels.tobytes(),compresslevel=9)).decode())
                flow[f'{key}:{a}|{key}:{b}']=fields
        report.append({'appearance':key,'file':file,'dimensions':[iw,ih],'poses':24,'referenceHeights':references,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
        print('Measured',key,': 24 poses, 21 continuous transitions',flush=True)
payload={'images':images,'poses':poses,'flow':flow,'owners':owners,'flowSize':[FS,FS],'version':2}
OUT.parent.mkdir(parents=True,exist_ok=True)
OUT.write_text('globalThis.CLUB_POSES='+json.dumps(payload,separators=(',',':'))+';\n',encoding='utf-8')
concepts=[]
for style in ['chibi','realistic']:
    source=ART/f'school-{style}.png'
    concepts.append({'style':style,'file':source.name,'dimensions':list(Image.open(source).size),'sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
(ART/'pose-manifest.json').write_text(json.dumps({'sources':report,'concepts':concepts,'poses':poses},indent=2)+'\n',encoding='utf-8')
print('Wrote',OUT,'bytes',OUT.stat().st_size)
