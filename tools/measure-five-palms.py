"""Find candidate palm centres for visual review; changes no image pixels."""
import json
from pathlib import Path
import cv2
import numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[1];art=root/'artwork/interaction-five'
reviews=json.loads((art/'chibi-contact-review-all.json').read_text())
report=[]
for review in reviews:
    im=np.asarray(Image.open(art/review['file']).convert('RGBA'));h,w=im.shape[:2];cw,ch=w/4,h/5
    points=[]
    for i,(gx,gy) in enumerate(review['points']['high'],1):
        cx,cy=i%4,i//4;ox,oy=cx*cw,cy*ch
        x0=int(ox+max(.1,gx-.15)*cw);x1=int(ox+min(.39 if i in (2,3) else .50,gx+.15)*cw)
        y0=int(oy+max(.22,gy-.18)*ch);y1=int(oy+min(.66,gy+.12)*ch)
        p=im[y0:y1,x0:x1].astype(float);r,g,b,a=p.transpose(2,0,1)
        mask=((a>100)&(r>190)&(g>155)&(b>150)&(r-g>5)&(r-g<65)&(r-b>10)&(r-b<90)).astype('uint8')
        # The cropped face touches the right ROI edge; keep isolated hand skin.
        _,labels,_,_=cv2.connectedComponentsWithStats(mask,8)
        edge=np.unique(labels[:,-1]);isolated=mask.copy()
        isolated[np.isin(labels,edge[edge!=0])]=0
        if isolated.any():mask=isolated
        distance=cv2.distanceTransform(mask,cv2.DIST_L2,5)
        yy,xx=np.indices(mask.shape);ux=(xx+x0-ox)/cw;uy=(yy+y0-oy)/ch
        score=distance*np.exp(-((ux-gx)**2+(uy-gy)**2)/(.16**2))
        y,x=np.unravel_index(score.argmax(),score.shape)
        point=[round(float(ux[y,x]),4),round(float(uy[y,x]),4)]
        points.append(point)
    report.append(dict(character=review['character'],outfit=review['outfit'],old=review['points']['high'],candidate=points))
(art/'palm-measurement-candidates.json').write_text(json.dumps(report,indent=2)+'\n')
for r in report:print(r['character']+'/'+r['outfit'],r['candidate'])
