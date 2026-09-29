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
    'tt': ('adult-dance-tt.png', 4, 2),
    'heart': ('adult-dance-step.png', 4, 2),
    'wave': ('adult-dance-wave.png', 4, 2),
    'tickle': ('adult-tickle.png', 4, 2),
}
images, poses, normalized, silhouettes, source_report = {}, {}, {}, {}, []
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

# Each entry is a target drawing and travel/hold duration. No texture-changing
# long waits are hidden inside a timer; the animator owns the complete phrase.
clips = {
 'tt': {'name':'TT 手势', 'sequence':[0,1,2,3,2,4,5,4,2,6,7],
        'times':[500,600,800,800,500,800,800,800,600,700,1100]},
 'heart': {'name':'比心轻踏', 'sequence':[0,6,1,2,3,2,4,5,4,2,1,6,7],
           'times':[400,500,480,620,750,500,450,750,500,500,450,500,800]},
 'wave': {'name':'侧步招手', 'sequence':[0,3,4,1,4,3,0,6,2,5,2,6,7],
          'times':[400,500,500,600,500,500,450,500,550,600,550,500,800]},
 'tickle': {'name':'挠痒', 'sequence':[0,1,2,3,4,5,4,6,7],
            'times':[160,300,360,400,330,420,380,500,650]},
}
pairs=set()
for key,c in clips.items():
    if key != 'tickle':
        factor=8000/sum(c['times'])
        c['times']=[round(t*factor) for t in c['times']]
        c['times'][-1]+=8000-sum(c['times'])
    c['frames']=[f'{key}:{i}' for i in c.pop('sequence')]
    c['duration']=sum(c['times'])
    pairs.update(zip(c['frames'],c['frames'][1:]))
    pairs.add(('idle:0',c['frames'][0]))
    pairs.add((c['frames'][-1],'idle:0'))
for a in range(8):
    for b in range(5):
        if a!=b:pairs.add((f'idle:{a}',f'idle:{b}'))
for i in range(5,8):pairs.add(('idle:0',f'idle:{i}'))
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
for a,b in sorted(pairs):
    pair='|'.join(sorted([a,b]))
    if pair in flow:continue
    a,b=pair.split('|'); fields=[]
    for first,second in [(a,b),(b,a)]:
        field=tracker.calc(normalized[first],normalized[second],None)
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
payload={'version':'demo-2026-09-29','poses':poses,'images':images,'flow':flow,
         'flowSize':[FW,FH],'flowEncoding':'gzip','clips':clips,'sources':source_report,
         'scope':'DeepSeek · 真人 · 原装 / Demo only'}
(DEST/'assets.js').write_text('window.MOTION_ASSETS = '+json.dumps(payload,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
(DEST/'manifest.json').write_text(json.dumps({k:v for k,v in payload.items() if k not in ['images','flow']},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Saved metadata and offline image bundle:',DEST,flush=True)
