"""Export review-only whole-body geometry. Original image bytes stay unchanged."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from PIL import Image
from sprite_ownership import owned_cells, encode

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'docs/demo/companion-v01'
ASSETS = ROOT / 'src/DesktopPet.App/Assets/Characters'
FAMILIES = [('whale', 'DeepSeek', 'deepseek-adult')] + [(s, n, s+'-adult') for s, n in [('gpt','GPT'),('claude','Claude'),('gemini','Gemini'),('grok','Grok'),('qwen','Qwen'),('zhipu','GLM'),('kimi','Kimi')]]
parser = argparse.ArgumentParser()
parser.add_argument('--target', type=Path, default=SOURCE)
parser.add_argument('--idle-only', action='store_true', help='Export only existing idle gesture geometry; keep other generated review data.')
parser.add_argument('--play-only', action='store_true', help='Export existing full-body play frames and anchors without changing other geometry or pixels.')
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

def descriptor(cid, sprite, frame=None, anchor=None):
    cache_key = (cid, json.dumps(sprite,sort_keys=True), frame, json.dumps(anchor,sort_keys=True))
    if cache_key in descriptors:
        return descriptors[cache_key]
    if frame is not None:
        base=descriptor(cid,sprite)
        cells=list(base['cells'])
        if anchor:
            cells[frame]={**cells[frame],'footX':anchor['footX'],'footY':anchor['footY']}
        result={**base,'cells':cells,'frames':[frame],'frameMs':[5000]}
        descriptors[cache_key]=result
        return result
    file = ASSETS/cid/sprite['file']
    if file not in images:
        images[file] = Image.open(file).convert('RGBA')
    im=images[file]
    cols,rows=sprite.get('columns',3),sprite.get('rows',2)
    width,height=im.size
    cells=sprite.get('cells') or [dict(x=i%cols*(width//cols),y=i//cols*(height//rows),width=width//cols,height=height//rows) for i in range(cols*rows)]
    measured=[]
    masks=owned_cells(file,{**sprite,'cells':cells})
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
        import numpy as np
        band=np.asarray(alpha,dtype=np.float64)[top:min(bottom,top+max(1,int((bottom-top)*.3)))]
        column_mass=np.where(band>=48,band,0).sum(axis=0)
        mass=float(column_mass.sum());weighted=float((column_mass*(np.arange(w)+.5)).sum())
        pivot=weighted/mass if mass else w/2
        measured.append(dict(**c,footX=pivot,footY=bottom,visibleHeight=bottom-top,bounds=list(box),scale=(sprite.get('frameScaleFactors') or [1]*(cols*rows))[i]))
        if mask is not None:measured[-1]['ownership']=encode(mask)
    reference=sprite.get('referenceHeightPixels') or max(c['visibleHeight']*c['scale'] for c in measured)
    if anchor and frame is not None:
        measured[frame].update(footX=anchor['footX'],footY=anchor['footY'])
    result=dict(file=sprite['file'],cells=measured,reference=reference,frames=[frame] if frame is not None else sprite.get('frames',list(range(len(cells)))),frameMs=[5000] if frame is not None else sprite.get('frameMs',[160]*len(sprite.get('frames',cells))),facing=sprite.get('facing','right'))
    descriptors[cache_key]=result
    return result

def export_idle_art():
    # Reuse the approved outfit's complete stretch sequence. No pixel edits,
    # cross-outfit fallback, generated photo copies, or new image payloads.
    idle_art = {}
    for family, name, adult in FAMILIES:
        idle_art[family] = {}
        for style, cid in [('chibi', family), ('realistic', adult)]:
            pet = json.loads((ASSETS/cid/'pet.json').read_text(encoding='utf-8-sig'))
            idle_art[family][style] = {}
            for outfit in ['original', 'sports', 'swim', 'wedding']:
                clothes = pet if outfit == 'original' else pet['outfits'][outfit]
                def compact(desc):
                    return {**desc, 'cells': [c if i in desc['frames'] else None for i, c in enumerate(desc['cells'])]}
                gestures = {'stretch': compact(descriptor(cid, clothes['motions']['stretch']))}
                if style == 'chibi' and outfit in ['swim', 'wedding']:
                    # These older atlases overlap their neighboring cells. Export
                    # native ownership for a clean ponder clip. Their one-frame
                    # outfit portrait is not a smile: use the existing happy pose.
                    gestures['think'] = compact(descriptor(cid, {**clothes['motions']['think'], 'exportOwnership': True}))
                    five = clothes['interactionFive']
                    pose = five['poses']['happy']
                    gestures['smile'] = compact(descriptor(cid, {**five['atlases'][pose['atlas']], 'isolateCells': True, 'exportOwnership': True}, pose['frame'], pose))
                idle_art[family][style][outfit] = gestures
    (target/'idle-art.js').write_text('globalThis.CLOUD_IDLE_ART='+json.dumps(idle_art,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')


def export_play_art():
    result={}
    motion_keys=['walk','peek','headpat','poke','tickle','meal','feed','eat','listen','anticipate','build','ball-ready','ball-hit','ball-miss','stars','bubbles','stretch','comb','wipe','think','jump','curl','bonk']
    pose_keys=['receive','gift','gift-empty','read','page','read-finish','happy','high-prep','high-ready','high-contact','high-recoil','fist-up','fist-mid','fist-down','reveal-rock','reveal-scissors','reveal-paper']
    for family,name,adult in FAMILIES:
        result[family]={}
        for style,cid in [('chibi',family),('realistic',adult)]:
            pet=json.loads((ASSETS/cid/'pet.json').read_text(encoding='utf-8-sig'))
            result[family][style]={}
            for outfit in ['original','sports','swim','wedding']:
                clothes=pet if outfit=='original' else pet['outfits'][outfit]
                five=clothes['interactionFive'];looks={}
                for key in pose_keys:
                    p=five['poses'][key]
                    desc=descriptor(cid,{**five['atlases'][p['atlas']], 'exportOwnership':True},p['frame'],p)
                    looks[key]={**desc,'cells':[c if i in desc['frames'] else None for i,c in enumerate(desc['cells'])], 'anchors':p.get('anchors',{}),'frontY':p.get('frontY',0),'boxWidth':p.get('boxWidth',0)}
                for key in motion_keys:
                    if key not in clothes['motions']:
                        continue
                    sprite=clothes['motions'][key]
                    desc=descriptor(cid,{**sprite,'exportOwnership':True})
                    looks[key]={**desc,'cells':[c if i in desc['frames'] else None for i,c in enumerate(desc['cells'])]}
                result[family][style][outfit]=looks
            for im in images.values():im.close()
            images.clear();descriptors.clear()
        print('Play geometry: '+name,flush=True)
    from compact_play_masks import compact
    compact(result)
    (target/'play-art.js').write_text('globalThis.CLOUD_PLAY_ART='+json.dumps(result,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')

if args.play_only:
    export_play_art()
    print(json.dumps(dict(appearances=64,pixelChanges=0,bytes=(target/'play-art.js').stat().st_size)))
    sys.exit(0)

export_idle_art()
if args.idle_only:
    print(json.dumps(dict(target=str(target),idleAppearances=64,pixelChanges=0),ensure_ascii=False))
    sys.exit(0)

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
            smile=descriptor(cid,atlas,min(1,len(idle['cells'])-1))
            if five:
                if style=='realistic':
                    p=five['poses']['happy'];smile=descriptor(cid,five['atlases'][p['atlas']],p['frame'],p)
            stand=idle
            sit=idle
            if style=='chibi' and five:
                p=five['poses']['neutral'];stand=descriptor(cid,five['atlases'][p['atlas']],p['frame'],p)
                # These old neutral drawings still sit with knees forward. Reuse
                # the upright close-foot support pose, held still without gait.
                if outfit=='wedding' or (outfit=='swim' and family in ['gemini','qwen','zhipu','kimi']) or (outfit=='sports' and family in ['gpt','claude','qwen']):
                    walk=motions['walk'];stand=descriptor(cid,walk,walk.get('frames',list(range(12)))[7])
            elif style=='realistic':
                # The first curl pose is awake and seated; the next one is asleep.
                curl={**motions['curl'],'isolateCells':True,'exportOwnership':True};sit=descriptor(cid,curl,curl.get('frames',[6])[0])
            if 'idle-stand' in motions:
                standing=motions['idle-stand'];stand=descriptor(cid,standing,standing.get('frames',[0])[0])
            look=dict(idle=idle,stand=stand,sit=sit,smile=smile)
            for key,native in [('think','think'),('talk','chat'),('pat','headpat'),('walk','walk'),('peek','peek')]:
                look[key]=descriptor(cid,motions[native]) if native in motions else idle
            looks[outfit]=look
        entry['styles'][style]=dict(id=cid,looks=looks)
    data['families'][family]=entry

items=(ROOT/'src/DesktopPet.Core/Collectibles.cs').read_text(encoding='utf-8-sig')
for id,name,kind,tail in re.findall(r'new\("([a-z-]+)", "([^"]+)", ItemKind\.(\w+)([^\n]*)',items):
    data['items'].append(dict(id=id,name=name,kind='balls' if kind=='Sport' else 'food' if ', true' in tail else 'keepsakes'))
data['stories']=json.loads(subprocess.check_output(['node','-e','process.stdout.write(JSON.stringify(require(process.argv[1])))',str(SOURCE/'story-library.js')],encoding='utf-8'))
assert len(data['items'])==20 and len(data['stories'])==10
export_play_art()

if target!=SOURCE:
    for name in ['index.html','style.css','icons.js','personas.js','providers.js','model.js','agenda.js','agenda-ui.js','agenda.css','idle-postures.js','story-library.js','play-model.js','play-ui.js','play.css','app.js','preview-art.js','claude-review.html']:
        shutil.copyfile(SOURCE/name,target/name)
    (target/'art').mkdir(exist_ok=True)
    for file in (SOURCE/'art').glob('claude-sports-stand-v2*'):
        shutil.copyfile(file,target/'art'/file.name)
shutil.copyfile(ROOT/'docs/demo/interaction-five/items.js',target/'items.js')
(target/'data.js').write_text('globalThis.CLOUD_DATA='+json.dumps(data,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
print(json.dumps(dict(target=str(target),version=version,families=len(FAMILIES),appearances=64,items=len(data['items']),stories=len(data['stories']),pixelChanges=0),ensure_ascii=False))
