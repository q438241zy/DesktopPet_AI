"""Export the native measured artwork to the HTML preview; PNGs stay shared."""
import argparse,base64,json,mimetypes,os
from pathlib import Path
from PIL import Image
import numpy as np
from sprite_ownership import owned_cells, encode
ROOT=Path(__file__).resolve().parents[1]
PACKS=ROOT/'src/DesktopPet.App/Assets/Characters'
NAMES={'whale':'DeepSeek','gpt':'GPT','claude':'Claude','gemini':'Gemini','grok':'Grok','qwen':'Qwen','zhipu':'GLM','kimi':'Kimi'}
OUTFITS={'original':'原装','swim':'泳装','wedding':'婚纱','sports':'运动服'}
def reference(folder,sprite):
    im=Image.open(folder/sprite['file']).convert('RGBA');w,h=im.size
    r=sprite.get('cells',[dict(x=0,y=0,width=w//sprite.get('columns',1),height=h//sprite.get('rows',1))])[0]
    x,y,cw,ch=[r[k] for k in ['x','y','width','height']]
    a=np.asarray(im)[y:y+ch,x:x+cw,3]>80;yy,xx=np.nonzero(a)
    l,t,bw,bh=int(xx.min()),int(yy.min()),int(xx.max()-xx.min()+1),int(yy.max()-yy.min()+1)
    _,feet=np.nonzero(a[t+bh-max(4,round(bh*.025)):t+bh])
    return dict(file=folder.name+'/'+sprite['file'],rect=[x+l,y+t,bw,bh],foot=[x+float(np.median(feet)),y+t+bh],height=bh)
def collect():
    out=[]
    for family,label in NAMES.items():
      for style,char in [('chibi',family),('realistic','deepseek-adult' if family=='whale' else family+'-adult')]:
        folder=PACKS/char;data=json.loads((folder/'pet.json').read_text(encoding='utf-8-sig'))
        for outfit,outfit_label in OUTFITS.items():
          node=data if outfit=='original' else data['outfits'][outfit];art=node.get('interactionFive')
          if not art:continue
          ref=art['atlases'][art['poses']['neutral']['atlas']]['referenceHeightPixels'];poses={};images={}
          masks={}
          for key,atlas in art['atlases'].items():
            images[char+'/'+outfit+'/'+key]=char+'/'+atlas['file']
            masks[key]=owned_cells(folder/atlas['file'],atlas)
          for name,p in art['poses'].items():
            atlas=art['atlases'][p['atlas']];cell=atlas['cells'][p['frame']];x,y,w,h=[cell[k] for k in ['x','y','width','height']]
            poses[name]=dict(atlas=char+'/'+outfit+'/'+p['atlas'],rect=[x,y,w,h],foot=[x+p['footX'],y+p['footY']],anchors={k:[v['x'],v['y']] for k,v in p['anchors'].items()},unit=ref/atlas['referenceHeightPixels'],frontY=p.get('frontY',0),boxWidth=p.get('boxWidth',0))
            mask=masks[p['atlas']][p['frame']] if masks[p['atlas']] else None
            if mask is not None:poses[name]['ownership']=encode(mask)
          out.append(dict(id=char+'/'+outfit,family=family,name=label,style=style,outfit=outfit,outfitName=outfit_label,images=images,art=dict(referenceHeight=ref,poses=poses),reference=reference(folder,data['atlas'] if outfit=='original' else node['idle'])))
    return out
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--destination',default=str(ROOT/'docs/demo/interaction-five'));p.add_argument('--asset-root');a=p.parse_args()
    dest=Path(a.destination);dest.mkdir(parents=True,exist_ok=True)
    root=a.asset_root or os.path.relpath(PACKS,dest).replace('\\','/')
    entries=collect();modules=dest/'packs';modules.mkdir(exist_ok=True)
    for entry in entries:
      files={**entry['images'],'native-reference':entry['reference']['file']};payload={}
      for key,file in files.items():
        path=PACKS/file;mime=mimetypes.guess_type(path)[0] or 'image/png'
        payload[key]='data:'+mime+';base64,'+base64.b64encode(path.read_bytes()).decode('ascii')
      module=entry['id'].replace('/','-')+'.js';entry['module']='packs/'+module
      (modules/module).write_text('globalThis.FIVE_SOURCE('+json.dumps(entry['id'])+','+json.dumps(payload,separators=(',',':'))+');\n',encoding='utf-8')
    (dest/'roster.js').write_text('globalThis.FIVE_ROSTER='+json.dumps(dict(assetRoot=root,appearances=entries),ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
    print(json.dumps(dict(appearances=len(entries),path=str(dest/'roster.js'))))
