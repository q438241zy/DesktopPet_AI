"""Analyse untouched imagegen sprites; emit layout and optical-flow metadata for the browser renderer.

No raster output is generated or edited. Temporary arrays are only used to measure
correspondences. All displayed colour/alpha comes from the original PNG files.
"""
import base64, gzip, hashlib, json
from pathlib import Path
import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'docs/demo/motion-study'
W, H = 320, 480
FW, FH = 80, 120
spec = {
    'idle': ('adult-idle.png', 4, 2),
    'idleExtra': ('adult-idle-extra.png', 3, 2),
    'tt': ('adult-dance-tt.png', 4, 2),
    'heart': ('adult-dance-step.png', 4, 2),
    'wave': ('adult-dance-wave.png', 4, 2),
    'nextLevel': ('adult-dance-next-level.png', 4, 2),
    'loveDive': ('adult-dance-love-dive.png', 4, 2),
    'nextBetween': ('adult-dance-next-level-inbetweens.png', 4, 2),
    'diveBetween': ('adult-dance-love-dive-inbetweens.png', 4, 2),
    'tickle': ('adult-tickle.png', 4, 2),
}
images, poses, normalized, silhouettes, heads, source_report = {}, {}, {}, {}, {}, []
for key, (filename, cols, rows) in spec.items():
    path = DEST / 'assets' / filename
    original = np.asarray(Image.open(path).convert('RGBA'))
    ih, iw = original.shape[:2]
    assert (original[:, :, 3] == 0).mean() > .25, filename
    images[key] = 'data:image/png;base64,' + base64.b64encode(path.read_bytes()).decode()
    measurements = []
    count = cols * rows
    for index in range(count):
        x0, x1 = round(index % cols * iw / cols), round((index % cols + 1) * iw / cols)
        y_edges = [0,548,ih] if key == 'tickle' else [round(n*ih/rows) for n in range(rows+1)]
        y0, y1 = y_edges[index // cols:index // cols+2]
        alpha = original[y0:y1, x0:x1, 3]
        ys, xs = np.nonzero(alpha >= 80)
        assert len(xs) > 5000, (key,index)
        l, t, r, b = int(xs.min()), int(ys.min()), int(xs.max()+1), int(ys.max()+1)
        # Anchor the stable waist, not the moving arm/hair outline.
        band = alpha[t+round((b-t)*.36):t+round((b-t)*.45)]
        by, bx = np.nonzero(band >= 100)
        anchor = float(np.median(bx))
        measurements.append((x0+l, y0+t, r-l, b-t, x0+anchor))
    reference_height = max(m[3] for m in measurements)
    for index, (x,y,w,h,anchor) in enumerate(measurements):
        # Common scale within a sheet preserves lowered heads and bent knees.
        scale = H * .90 / reference_height
        dx = W*.5 - (anchor-x)*scale
        dy = H*.96 - h*scale
        layout = [dx/W, dy/H, w*scale/W, h*scale/H]
        poses[f'{key}:{index}'] = {'atlas':key,'cell':[x/iw,y/ih,w/iw,h/ih], 'rect':layout,
                                  'pixels':[x,y,w,h], 'height':reference_height}
        # Analysis only: alpha-composite against neutral grey for image tracking.
        crop = original[y:y+h,x:x+w]
        colour=crop[:,:,:3].astype('int16')
        skin=((colour[:,:,0]>150)&(colour[:,:,0]>colour[:,:,1]+12)&(colour[:,:,0]>colour[:,:,2]+24)&(crop[:,:,3]>150)).astype('uint8')
        skin_count,labels,stats,centres=cv2.connectedComponentsWithStats(skin)
        candidates=[i for i in range(1,skin_count) if stats[i,4]>90 and .06*H<dy+centres[i,1]*scale<.27*H and .28*W<dx+centres[i,0]*scale<.78*W]
        selected=max(candidates,key=lambda i:stats[i,4]) if candidates else None
        heads[f'{key}:{index}']=[[float(dx+centres[selected,0]*scale),float(dy+centres[selected,1]*scale)]] if selected else [[160,96]]
        a = crop[:,:,3:4].astype(np.float32)/255
        rgb = crop[:,:,:3].astype(np.float32)*a + 215*(1-a)
        matrix = np.float32([[scale,0,dx],[0,scale,dy]])
        temp = cv2.warpAffine(rgb, matrix, (W,H), flags=cv2.INTER_LINEAR,
                              borderMode=cv2.BORDER_CONSTANT,borderValue=(215,215,215))
        normalized[f'{key}:{index}'] = cv2.cvtColor(np.uint8(np.clip(temp,0,255)), cv2.COLOR_RGB2GRAY)
        silhouettes[f'{key}:{index}'] = cv2.warpAffine(crop[:,:,3],matrix,(W,H),flags=cv2.INTER_LINEAR)
    source_report.append({'file':'assets/'+filename,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                          'dimensions':[iw,ih],'usedPoses':count})

parts_path=DEST/'assets/adult-walk-parts.png'
images['parts']='data:image/png;base64,'+base64.b64encode(parts_path.read_bytes()).decode()
source_report.append({'file':'assets/adult-walk-parts.png','sha256':hashlib.sha256(parts_path.read_bytes()).hexdigest(),
                      'dimensions':list(Image.open(parts_path).size),'usedParts':4})
front_path=DEST/'assets/adult-dance-front-parts.png'
images['frontParts']='data:image/png;base64,'+base64.b64encode(front_path.read_bytes()).decode()
source_report.append({'file':'assets/adult-dance-front-parts.png','sha256':hashlib.sha256(front_path.read_bytes()).hexdigest(),
                      'dimensions':list(Image.open(front_path).size),'usedParts':4})
gesture_path=DEST/'assets/adult-dance-hand-gestures.png'
images['gestures']='data:image/png;base64,'+base64.b64encode(gesture_path.read_bytes()).decode()
source_report.append({'file':'assets/adult-dance-hand-gestures.png','sha256':hashlib.sha256(gesture_path.read_bytes()).hexdigest(),
                      'dimensions':list(Image.open(gesture_path).size),'usedParts':4})
hand_data=json.loads((ROOT/'artwork/motion-study/hand-landmarks.json').read_text(encoding='utf-8'))
hands={f'{key}:{i}':value for key,values in hand_data.items() if key!='coordinateSpace' for i,value in enumerate(values)}

# Each entry is a target drawing and travel/hold duration. No texture-changing
# long waits are hidden inside a timer; the animator owns the complete phrase.
clips = {
 'tt': {'name':'TT 手势', 'sequence':[0,1,2,3,2,4,5,4,2,6,7],
        'times':[500,600,800,800,500,800,800,800,600,700,1100]},
 'heart': {'name':'比心轻踏', 'sequence':[0,6,1,2,3,2,4,5,4,2,1,6,7],
           'times':[400,500,480,620,750,500,450,750,500,500,450,500,800]},
 'wave': {'name':'侧步招手', 'sequence':[0,3,4,1,4,3,0,6,2,5,2,6,7],
          'times':[400,500,500,600,500,500,450,500,550,600,550,500,800]},
 'nextLevel': {'name':'Next Level 折臂',
               'sequence':[p for i in range(8) for p in [f'nextLevel:{i}',f'nextBetween:{i}']],
               'times':[500]*15+[250]},
 'loveDive': {'name':'LOVE DIVE 镜面',
              'sequence':['loveDive:0','diveBetween:0','diveBetween:1','loveDive:2','diveBetween:2','loveDive:3','diveBetween:3','loveDive:4','diveBetween:4','loveDive:5','diveBetween:5','loveDive:6','diveBetween:6','loveDive:7','diveBetween:7'],
              'times':[500]*14+[250]},
 'tickle': {'name':'挠痒', 'sequence':[0,1,2,3,4,5,4,6,7],
            'times':[160,300,360,400,330,420,380,500,650]},
}
idles=[{'pose':f'idle:{i}','name':name,'label':label} for i,(name,label) in enumerate([
    ('自然站立','自然'),('轻拢双手','拢手'),('侧头看看','侧望'),('整理头发','理发'),('闭目呼吸','闭目')])]
idles += [{'pose':f'idleExtra:{i}','name':name,'label':label} for i,(name,label) in enumerate([
    ('托腮想想','托腮'),('轻抱双臂','抱臂'),('轻搭腰侧','搭腰'),('整理袖口','理袖'),('抬头看看','抬望')])]
pairs=set()
for key,c in clips.items():
    if key != 'tickle':
        factor=8000/sum(c['times'])
        c['times']=[round(t*factor) for t in c['times']]
        c['times'][-1]+=8000-sum(c['times'])
    c['frames']=[i if isinstance(i,str) else f'{key}:{i}' for i in c.pop('sequence')]
    c['duration']=sum(c['times'])
    pairs.update(zip(c['frames'],c['frames'][1:]))
    for idle in idles:pairs.add((idle['pose'],c['frames'][0]))
    for pose in c['frames']:pairs.add((pose,'idle:0'))
idle_poses=[i['pose'] for i in idles]+[f'idle:{i}' for i in range(5,8)]
for a in idle_poses:
    for b in idle_poses:
        if a!=b:pairs.add((a,b))
# Flow is displacement metadata, not interpolated bitmap frames. Store both
# directions; reversing only one field makes disoccluded hands smear.
flow = {}; tracker=cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
tracker.setFinestScale(0)
def legs(mask):
    """Measure two horizontal leg centres without guessing colour matches.

    Bright apron lace and boot cuffs look alike to optical flow. Match silhouettes
    below the hem instead, retaining y so lace cannot be pulled down a thigh.
    """
    result=[]
    last=(W*.455,W*.545)
    for y in range(H-1,int(H*.60)-1,-1):
        row=mask[max(0,y-1):min(H,y+2)].max(axis=0)>=80
        edges=np.diff(np.r_[False,row,False].astype('int8'))
        runs=[(a,b) for a,b in zip(np.where(edges==1)[0],np.where(edges==-1)[0]) if b-a>3]
        if len(runs)>=2:
            selected=sorted(sorted(runs,key=lambda x:x[1]-x[0],reverse=True)[:2])
            last=tuple((a+b)/2 for a,b in selected)
        elif len(runs)==1:
            a,b=runs[0]
            if b-a>18:last=(a+(b-a)*.27,a+(b-a)*.73)
        result.append(last)
    return np.asarray(result[::-1],dtype='float32')
leg_centres={k:legs(v) for k,v in silhouettes.items()}
def face_motion(first,second):
    """Track the head as one surface; dense garment matches doubled eyes/cheeks."""
    mask=np.zeros((H,W),dtype='uint8');mask[round(H*.08):round(H*.27),round(W*.35):round(W*.65)]=255
    corners=cv2.goodFeaturesToTrack(first,80,.015,4,mask=mask)
    if corners is None:return None
    target,valid,_=cv2.calcOpticalFlowPyrLK(first,second,corners,None,winSize=(21,21),maxLevel=3)
    returned,back,_=cv2.calcOpticalFlowPyrLK(second,first,target,None,winSize=(21,21),maxLevel=3)
    keep=(valid[:,0]>0)&(back[:,0]>0)&(np.linalg.norm(returned[:,0]-corners[:,0],axis=1)<1.4)
    if keep.sum()<6:return None
    matrix,inliers=cv2.estimateAffinePartial2D(corners[keep,0],target[keep,0],method=cv2.RANSAC,ransacReprojThreshold=2)
    if matrix is None or inliers.sum()<6:return None
    scale=np.hypot(matrix[0,0],matrix[1,0]);angle=np.arctan2(matrix[1,0],matrix[0,0])
    if not .90<scale<1.1 or abs(angle)>.4:return None
    yy,xx=np.mgrid[:H,:W];coords=np.stack([xx,yy],axis=2).astype('float32')
    offset=coords@matrix[:,:2].T+matrix[:,2]-coords
    radius=np.sqrt(((xx-W*.5)/(W*.15))**2+((yy-H*.17)/(H*.095))**2)
    weight=np.clip((1.4-radius)/.4,0,1).astype('float32');weight=weight*weight*(3-2*weight)
    return offset.astype('float32'),weight
for a,b in sorted(pairs):
    pair='|'.join(sorted([a,b]))
    if pair in flow:continue
    a,b=pair.split('|'); fields=[]
    for first,second in [(a,b),(b,a)]:
        field=tracker.calc(normalized[first],normalized[second],None)
        head=face_motion(normalized[first],normalized[second])
        if head is not None:
            offset,weight=head;field=field*(1-weight[:,:,None])+offset*weight[:,:,None]
        ca,cb=leg_centres[first],leg_centres[second]
        y0=int(H*.60)
        for y in range(y0,H):
            left,right=ca[y-y0];dl,dr=cb[y-y0]-ca[y-y0]
            blend=np.clip((np.arange(W)-left)/max(8,right-left),0,1)
            horizontal=np.clip(dl*(1-blend)+dr*blend,-W*.15,W*.15)
            weight=float(np.clip((y/H-.61)/.04,0,1));weight=weight*weight*(3-2*weight)
            field[y,:,0]=field[y,:,0]*(1-weight)+horizontal*weight
            field[y,:,1]*=1-weight
        field=cv2.resize(field,(FW,FH),interpolation=cv2.INTER_AREA)
        field[:,:,0]/=W;field[:,:,1]/=H
        encoded=np.empty((FH,FW,4),dtype=np.uint8)
        encoded[:,:,:2]=np.uint8(np.clip(np.rint(field/.6*255+128),0,255))
        encoded[:,:,2]=0;encoded[:,:,3]=255
        fields.append(base64.b64encode(gzip.compress(encoded.tobytes(),mtime=0)).decode())
    flow[pair]=fields
print('Measured',len(poses),'poses;',len(flow),'bidirectional transitions',flush=True)
feet={}
for key,mask in silhouettes.items():
    if key not in hands:continue
    boot=(mask>=80).astype('uint8');boot[:round(H*.82)]=0
    count,labels,stats,centres=cv2.connectedComponentsWithStats(boot)
    selected=sorted([i for i in range(1,count) if stats[i,4]>90],key=lambda i:stats[i,4],reverse=True)[:2]
    if len(selected)==2:
        measured=[]
        for i in sorted(selected,key=lambda i:centres[i,0]):
            ys,xs=np.nonzero(labels==i);bottom=int(ys.max());sole=xs[ys>=bottom-4]
            measured.append([float(np.mean(sole)),bottom+1])
        feet[key]=measured
    else:feet[key]=[[140,460.8],[180,460.8]]
# Crossed boots merge into one alpha component. Keep the visible alternating
# heel lift from the reference rather than treating those beats as a stand.
feet['heart:3']=[[145,460.8],[185,444]]
feet['heart:5']=[[133,444],[176,460.8]]
payload={'version':'demo-2026-09-29-r2','poses':poses,'images':images,'flow':flow,'idles':idles,
         'hands':hands,'feet':feet,'heads':heads,
         'flowSize':[FW,FH],'flowEncoding':'gzip','clips':clips,'sources':source_report,
         'scope':'DeepSeek · 3D真人 · 原装 / Demo only'}
(DEST/'assets.js').write_text('window.MOTION_ASSETS = '+json.dumps(payload,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
(DEST/'manifest.json').write_text(json.dumps({k:v for k,v in payload.items() if k not in ['images','flow']},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Saved metadata and offline image bundle:',DEST,flush=True)
