"""Export declarative art geometry and unmodified photo sources for the v0.1 review.

No image pixels are edited. Browser-only photo modules embed the existing PNG bytes
so a file:// page can export a clean canvas without relaxing browser security.
"""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET
from PIL import Image
from sprite_ownership import owned_cells, encode

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'docs/demo/companion-v01'
ASSETS = ROOT / 'src/DesktopPet.App/Assets/Characters'
FAMILIES = [('whale', 'DeepSeek', 'deepseek-adult')] + [(s, n, s+'-adult') for s, n in [('gpt','GPT'),('claude','Claude'),('gemini','Gemini'),('grok','Grok'),('qwen','Qwen'),('zhipu','GLM'),('kimi','Kimi')]]
parser = argparse.ArgumentParser()
parser.add_argument('--target', type=Path, default=SOURCE)
args = parser.parse_args()
target = args.target.resolve()
if not target.is_relative_to(ROOT):
    raise SystemExit('Demo output must stay inside the project.')
target.mkdir(parents=True, exist_ok=True)
runtime_assets = target.parent.parent / 'Assets/Characters'
asset_root = runtime_assets if runtime_assets.exists() else ASSETS
version_xml = ET.parse(ROOT/'Version.props').getroot()
version = '.'.join(version_xml.findtext('.//DesktopPet'+key) for key in ['Major','Minor'])
data = dict(version=version, assetRoot=os.path.relpath(asset_root,target).replace('\\','/')+'/', families={}, items=[], stories=[])
images = {}
descriptors = {}
payloads = {}

def descriptor(cid, sprite, frame=None, anchor=None):
    cache_key = (cid, json.dumps(sprite,sort_keys=True), frame, json.dumps(anchor,sort_keys=True))
    if cache_key in descriptors:
        return descriptors[cache_key]
    file = ASSETS/cid/sprite['file']
    if file not in images:
        images[file] = Image.open(file).convert('RGBA')
    im=images[file]
    cols,rows=sprite.get('columns',3),sprite.get('rows',2)
    width,height=im.size
    cells=sprite.get('cells') or [dict(x=i%cols*(width//cols),y=i//cols*(height//rows),width=width//cols,height=height//rows) for i in range(cols*rows)]
    measured=[]
    masks=owned_cells(file,sprite)
    for i,c in enumerate(cells):
        x,y,w,h=[c[k] for k in ['x','y','width','height']]
        if x<0 or y<0 or x+w>width or y+h>height:
            raise ValueError(f'Out-of-bounds art cell {file}: {c}')
        alpha=im.getchannel('A').crop((x,y,x+w,y+h))
        mask=masks[i] if masks else None
        if mask is not None:
            import numpy as np
            alpha=Image.fromarray(np.asarray(alpha)*mask)
        box=alpha.point(lambda a:255 if a>=48 else 0).getbbox() or (0,0,w,h)
        # Keep one source-pixel scale across the complete clip, never fit each pose.
        top,bottom=box[1],box[3]
        mass=weighted=0
        px=alpha.load()
        for yy in range(top,min(bottom,top+max(1,int((bottom-top)*.3)))):
            for xx in range(w):
                a=px[xx,yy]
                if a>=48:mass+=a;weighted+=(xx+.5)*a
        pivot=weighted/mass if mass else w/2
        measured.append(dict(**c,footX=pivot,footY=bottom,visibleHeight=bottom-top,scale=(sprite.get('frameScaleFactors') or [1]*(cols*rows))[i]))
        if mask is not None:measured[-1]['ownership']=encode(mask)
    reference=sprite.get('referenceHeightPixels') or max(c['visibleHeight']*c['scale'] for c in measured)
    if anchor and frame is not None:
        measured[frame].update(footX=anchor['footX'],footY=anchor['footY'])
    result=dict(file=sprite['file'],cells=measured,reference=reference,frames=[frame] if frame is not None else sprite.get('frames',list(range(len(cells)))),frameMs=[5000] if frame is not None else sprite.get('frameMs',[160]*len(sprite.get('frames',cells))),facing=sprite.get('facing','right'))
    descriptors[cache_key]=result
    return result

def photo_module(cid,desc):
    file=ASSETS/cid/desc['file']
    sha=hashlib.sha256(file.read_bytes()).hexdigest()[:20]
    name=sha+'.js'
    if name not in payloads:
        payloads[name]=file
    return {'module':'.generated/photos/'+name,'key':sha}

for family,name,adult in FAMILIES:
    entry=dict(name=name,styles={})
    for style,cid in [('chibi',family),('realistic',adult)]:
        pet=json.loads((ASSETS/cid/'pet.json').read_text(encoding='utf-8-sig'))
        looks={}
        for outfit in ['original','sports','swim','wedding']:
            clothes=pet if outfit=='original' else pet['outfits'][outfit]
            atlas=pet['atlas'] if outfit=='original' else clothes['idle']
            motions=clothes.get('motions',{})
            idle=descriptor(cid,atlas,0)
            five=clothes.get('interactionFive')
            photo=idle
            smile=descriptor(cid,atlas,min(1,len(idle['cells'])-1))
            if five:
                p=five['poses']['photo'];photo=descriptor(cid,five['atlases'][p['atlas']],p['frame'],p)
                if style=='realistic':
                    p=five['poses']['happy'];smile=descriptor(cid,five['atlases'][p['atlas']],p['frame'],p)
            look=dict(idle=idle,smile=smile,photo=photo,photoData=photo_module(cid,photo))
            for key,native in [('think','think'),('talk','chat'),('pat','headpat'),('walk','walk'),('peek','peek')]:
                look[key]=descriptor(cid,motions[native]) if native in motions else idle
            looks[outfit]=look
        entry['styles'][style]=dict(id=cid,looks=looks)
    data['families'][family]=entry

items=(ROOT/'src/DesktopPet.Core/Collectibles.cs').read_text(encoding='utf-8-sig')
for id,name,kind,tail in re.findall(r'new\("([a-z-]+)", "([^"]+)", ItemKind\.(\w+)([^\n]*)',items):
    data['items'].append(dict(id=id,name=name,kind='balls' if kind=='Sport' else 'food' if ', true' in tail else 'keepsakes'))
stories=(ROOT/'src/DesktopPet.Core/StoryLibrary.cs').read_text(encoding='utf-8-sig')
for id,title,body in re.findall(r'new CompanionStory\("([^"]+)","([^"]+)",new\[\] \{(.*?)\}\)',stories,re.S):
    data['stories'].append(dict(id=id,title=title,sentences=re.findall(r'"([^"\n]+)"',body)))
assert len(data['items'])==20 and len(data['stories'])==3

if target!=SOURCE:
    for name in ['index.html','style.css','icons.js','personas.js','providers.js','model.js','app.js']:
        shutil.copyfile(SOURCE/name,target/name)
shutil.copyfile(ROOT/'docs/demo/interaction-five/items.js',target/'items.js')
(target/'data.js').write_text('globalThis.CLOUD_DATA='+json.dumps(data,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
photo_dir=target/'.generated/photos'
photo_dir.mkdir(parents=True,exist_ok=True)
for name,file in payloads.items():
    key=name[:-3];mime='image/webp' if file.suffix=='.webp' else 'image/png'
    body='globalThis.receivePhotoSource('+json.dumps(key)+','+json.dumps('data:'+mime+';base64,'+base64.b64encode(file.read_bytes()).decode())+');\n'
    destination=photo_dir/name
    if not destination.exists() or destination.read_text(encoding='utf-8')!=body:
        cached=SOURCE/'.generated/photos'/name
        if target!=SOURCE and cached.exists() and cached.read_text(encoding='utf-8')==body:
            if destination.exists():
                destination.unlink()
            try:
                os.link(cached,destination)
            except OSError:
                destination.write_text(body,encoding='utf-8')
        else:
            destination.write_text(body,encoding='utf-8')
print(json.dumps(dict(target=str(target),version=version,families=len(FAMILIES),appearances=64,photoModules=len(payloads),photoBytes=sum((photo_dir/n).stat().st_size for n in payloads),items=len(data['items']),stories=len(data['stories'])),ensure_ascii=False))
