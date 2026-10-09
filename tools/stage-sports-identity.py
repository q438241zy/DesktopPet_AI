"""Stage reviewed clothing edits in an isolated runtime; never update the daily app.

Original raster files are copied unchanged. Source action timing and anatomical
scale are inherited, including the legacy small-pose corrections. This utility
does not grant visual acceptance: each input requires a current review hash.
"""
import argparse
import base64
import copy
import gzip
import hashlib
import importlib.util
import json
import shutil
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'src/DesktopPet.App/Assets/Characters'
ART=ROOT/'artwork/sports-identity'
SIZE,FS=288,72

def module(name):
 spec=importlib.util.spec_from_file_location(name,ROOT/'tools'/f'{name}.py')
 value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value

def rectangles(image,sprite):
 if sprite.get('cells'):return sprite['cells']
 w,h=image.shape[1]//sprite['columns'],image.shape[0]//sprite['rows']
 return [dict(x=i%sprite['columns']*w,y=i//sprite['columns']*h,width=w,height=h) for i in range(sprite['columns']*sprite['rows'])]

def crop_geometry(rgba,cell):
 x,y,w,h=(cell[k] for k in ('x','y','width','height'))
 a=rgba[y:y+h,x:x+w,3];ys,xs=np.where(a>=48)
 if not len(ys):raise ValueError('Empty figure crop')
 top,bottom=int(ys.min()),int(ys.max())+1
 head=a.copy();head[:top]=0;head[round(top+(bottom-top)*.3):]=0;head[head<48]=0
 weights=head.sum(axis=0,dtype=np.float64);anchor=float(np.sum(weights*(np.arange(w)+.5))/weights.sum())
 return dict(top=top,bottom=bottom,height=bottom-top,extent=max(w,h),anchor=anchor,visible=(bottom-top)/max(w,h))

def components(rgba,count,expected,min_alpha=80):
 h,w=rgba.shape[:2]
 if (rgba[:,:,3]==0).mean()<.2:raise ValueError('Background is not transparent')
 # Some generated alpha mattes have opaque interiors at 252/253 and broad
 # translucent fringes. Seed complete figures from those interiors; the native
 # loader restores their original antialiasing alpha without altering the PNG.
 for threshold in [80,120,180,220,240,248,252,253,254,255]:
  if threshold<min_alpha:continue
  _,labels,stats,_=cv2.connectedComponentsWithStats((rgba[:,:,3]>=threshold).astype('uint8'),8)
  parts=sorted([(i,[int(v) for v in s]) for i,s in enumerate(stats) if i and s[4]>w*h*.0005],key=lambda r:r[1][4],reverse=True)
  if len(parts)>=count and parts[count-1][1][4]>w*h*.0015:break
 else:raise ValueError(f'Expected {count} complete figures, found {len(parts)}')
 main=parts[:count];ordered=[None]*count;used=set()
 # Assign closest pairs first; each logical pose owns one full connected figure.
 choices=[]
 for i,c in enumerate(expected):
  for j,(_,s) in enumerate(main):
   score=((s[0]+s[2]/2-c['x']-c['width']/2)/c['width'])**2+((s[1]+s[3]/2-c['y']-c['height']/2)/c['height'])**2
   choices.append((score,i,j))
 for score,i,j in sorted(choices):
  if ordered[i] is None and j not in used:
   if score>1.2:raise ValueError('Figure no longer matches its source location')
   ordered[i]=main[j];used.add(j)
 for label,(x,y,bw,bh,area) in parts[count:]:
  cx,cy=x+bw/2,y+bh/2
  i=min(range(count),key=lambda i:((cx-(ordered[i][1][0]+ordered[i][1][2]/2))/expected[i]['width'])**2+((cy-(ordered[i][1][1]+ordered[i][1][3]/2))/expected[i]['height'])**2)
  own,(mx,my,mw,mh,ma)=ordered[i]
  if abs(cx-mx-mw/2)<expected[i]['width']*.65 and my-bh<cy<my+mh+bh:
   labels[labels==label]=own
   l,t,r,b=min(x,mx),min(y,my),max(x+bw,mx+mw),max(y+bh,my+mh)
   ordered[i]=(own,[l,t,r-l,b-t,ma+area])
 # Expand the selected interiors through the source alpha, just as ArtCache
 # does at runtime. A fixed two-pixel dilation would clip a dense soft fringe
 # when a sheet needs a high separation threshold. This is ownership metadata,
 # not a replacement alpha channel, and no source image is ever written.
 owners=np.where(np.isin(labels,[item[0] for item in ordered]),labels,0).astype('float32')
 # Follow denser hair contours before faint bridges can cross to the next row.
 # Use the same alpha bands as the native loader, preserving all source pixels.
 for level in sorted({v for v in [threshold-1,248,240,220,180,120,80,48,16,1] if 0<v<threshold},reverse=True):
  foreground=rgba[:,:,3]>=level
  while True:
   neighbours=cv2.erode(np.where(owners>0,owners,16777216).astype('float32'),np.ones((3,3),np.uint8))
   fill=foreground&(owners==0)&(neighbours<16777216)
   if not np.any(fill):break
   owners[fill]=neighbours[fill]
 labels=owners.astype('int32')
 expanded=[]
 for owner,_ in ordered:
  ys,xs=np.where((labels==owner)&(rgba[:,:,3]>=48))
  x,y=int(xs.min()),int(ys.min());bw,bh=int(xs.max())+1-x,int(ys.max())+1-y
  expanded.append((owner,[x,y,bw,bh,len(xs)]))
 ordered=expanded
 cells=[]
 for _,(x,y,bw,bh,_) in ordered:
  l,t=max(0,x-3),max(0,y-3);r,b=min(w,x+bw+3),min(h,y+bh+3)
  cells.append(dict(x=l,y=t,width=r-l,height=b-t))
 return ordered,labels,cells,threshold

def authored(rgba,analysis,definitions,scales,source_hash):
 ordered,labels,cells,threshold=analysis;poses=[];normalized={}
 tracker=cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM);tracker.setFinestScale(0)
 for i,((owner,(x,y,w,h,_)),cell,scale) in enumerate(zip(ordered,cells,scales)):
  l,t,cw,ch=(cell[k] for k in ('x','y','width','height'))
  mask=labels==owner;local=rgba[t:t+ch,l:l+cw].copy()
  ownership=mask[t:t+ch,l:l+cw].astype('uint8')
  local[:,:,3]*=ownership
  g=crop_geometry(local,dict(x=0,y=0,width=cw,height=ch))
  factor=scale*SIZE;dx=.5*SIZE-g['anchor']*factor;dy=.92*SIZE-g['bottom']*factor
  poses.append(dict(rect=[dx/SIZE,dy/SIZE,cw*scale,ch*scale],pixels=[l,t,cw,ch],referenceHeight=.7/scale))
  a=local[:,:,3:4].astype(np.float32)/255;rgb=local[:,:,:3]*a+220*(1-a)
  temp=cv2.warpAffine(rgb,np.float32([[factor,0,dx],[0,factor,dy]]),(SIZE,SIZE),flags=cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT,borderValue=(220,220,220))
  normalized[i]=cv2.cvtColor(np.uint8(np.clip(temp,0,255)),cv2.COLOR_RGB2GRAY)
 pairs=set()
 for d in definitions.values():
  frames=d['frames'];pairs.update((a,b) for a,b in zip(frames,frames[1:]) if a!=b)
  if d['loop'] and frames[-1]!=frames[0]:pairs.add((frames[-1],frames[0]))
 flows={}
 for a,b in sorted(pairs):
  fields=[]
  for first,second in [(a,b),(b,a)]:
   field=tracker.calc(normalized[first],normalized[second],None)
   small=cv2.resize(field,(FS,FS),interpolation=cv2.INTER_AREA)/SIZE
   encoded=np.uint8(np.clip(np.rint(small/.6*255+128),0,255))
   fields.append(base64.b64encode(gzip.compress(encoded.tobytes(),compresslevel=9)).decode())
  flows[f'{a}:{b}']=fields
 return dict(version=2,sha256=source_hash,flowSize=FS,poses=poses,flow=flows),cells,threshold

def source_scale(folder,pet,sprite,source_rgba,source_cells,neutral_visible,drawn=True,action=''):
 metrics=[crop_geometry(source_rgba,c) for c in source_cells]
 count=len(metrics);legacy=sprite.get('frameScaleFactors') or [1]*count
 side=(folder/sprite['file']).with_suffix('.json')
 if sprite['file'].startswith(('motions/cloud-club-','motions/cloud-care-')) and side.is_file():
  data=json.loads(side.read_text(encoding='utf-8-sig'))
  return [p['rect'][2]/p['pixels'][2]*neutral_visible/.7 for p in data['poses']]
 reference=sprite.get('referenceHeightPixels',0)
 if reference:return [neutral_visible/reference*f for f in legacy]
 if action=='walk':return [neutral_visible/max(m['height'] for m in metrics)*f for f in legacy]
 result=[]
 for i,(g,f) in enumerate(zip(metrics,legacy)):
  scale=1/g['extent']
  if drawn and (pet.get('category','chibi')!='chibi' or sprite.get('heightRatios')):
   scale*=min(2.2,max(.6,neutral_visible/max(.1,g['visible'])))*(sprite.get('heightRatios') or [1]*count)[i]
  result.append(scale*f)
 return result

def owned_geometry(rgba,analysis,index):
 cell=analysis[2][index];l,t,w,h=(cell[k] for k in ('x','y','width','height'))
 owner=analysis[0][index][0]
 local=rgba[t:t+h,l:l+w].copy()
 local[:,:,3]*=(analysis[1][t:t+h,l:l+w]==owner).astype('uint8')
 return crop_geometry(local,dict(x=0,y=0,width=w,height=h))

def measured_contacts(job,rgba,analysis):
 result={};points=job.get('measurementPixels',{})
 for field in ('hands','bubbleSources','effectAnchors'):
  values=points.get(field)
  if values is None:continue
  if len(values)!=len(analysis[2]):raise ValueError(f'{job["key"]}: {field} count')
  out=[]
  for i,value in enumerate(values):
   if value is None:out.append(None);continue
   cell=analysis[2][i];g=owned_geometry(rgba,analysis,i)
   def anchor(p):
    x,y=(p[0]-cell['x'])/cell['width'],(p[1]-cell['y'])/cell['height']
    if not 0<=x<=1 or not 0<=y<=1:raise ValueError(f'{job["key"]}/{field}/{i}: point outside crop')
    return dict(x=round(x,6),y=round(y,6))
   if field=='hands':out.append(dict(height=round((value[1]-cell['y']-g['top'])/g['height'],6),offset=round((value[0]-cell['x']-g['anchor'])/g['height'],6),span=round(value[2]/g['height'],6)))
   elif field=='bubbleSources':out.append(anchor(value))
   else:out.append({key:anchor(p) for key,p in value.items()})
  result[field]=out
 return result

def point_mapper(original,candidate):
 """Measure image correspondence for existing anchors; never write image pixels."""
 h,w=original.shape[:2];ratio=candidate.shape[1]/w
 scale=min(1,768/max(w,h));shape=(round(w*scale),round(h*scale))
 def gray(rgba):
  a=rgba[:,:,3:4].astype('float32')/255
  composite=rgba[:,:,:3]*a+220*(1-a)
  return cv2.cvtColor(cv2.resize(np.uint8(composite),shape),cv2.COLOR_RGB2GRAY)
 tracker=cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
 field=tracker.calc(gray(original),gray(candidate),None)
 def point(x,y):
  sx,sy=min(shape[0]-1,max(0,round(x*scale))),min(shape[1]-1,max(0,round(y*scale)))
  dx,dy=field[sy,sx]/scale
  return (float(x+dx)*ratio,float(y+dy)*ratio)
 return point

def stage_five(folder,pet,jobs,out,original_visible,neutral_visible):
 original_art=pet['interactionFive'];art=copy.deepcopy(original_art);reports=[];missing=[]
 for name,sprite in original_art['atlases'].items():
  matching=[j for j in jobs if j['kind'].startswith('five-') and ROOT/j['references'][0]==folder/sprite['file']]
  if len(matching)!=1:missing.append(name);continue
  job=matching[0];original=np.asarray(Image.open(folder/sprite['file']).convert('RGBA'));rgba=np.asarray(Image.open(ROOT/job['source']).convert('RGBA'))
  ratio=rgba.shape[1]/original.shape[1];count=job['columns']*job['rows'];old_cells=rectangles(original,sprite)
  if abs(rgba.shape[0]-original.shape[0]*ratio)>2:raise ValueError('Canvas edge difference exceeds two pixels: '+job['key'])
  if len(old_cells)==count and len({tuple(c.values()) for c in old_cells})==count:
   physical=old_cells;mapping=list(range(count))
  else:
   physical=rectangles(original,dict(columns=job['columns'],rows=job['rows']))
   mapping=[min(range(count),key=lambda i:((c['x']+c['width']/2-physical[i]['x']-physical[i]['width']/2)/physical[i]['width'])**2+((c['y']+c['height']/2-physical[i]['y']-physical[i]['height']/2)/physical[i]['height'])**2) for c in old_cells]
  expected=[{k:round(v*ratio) for k,v in c.items()} for c in physical]
  analysis=components(rgba,count,expected);cells=analysis[2];mapper=point_mapper(original,rgba)
  relative='interactions/sports/identity-'+job['kind']+'.png';target=out/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(ROOT/job['source'],target)
  art['atlases'][name]=dict(file=relative,columns=job['columns'],rows=job['rows'],cells=cells,referenceHeightPixels=round(sprite['referenceHeightPixels']*ratio*neutral_visible/original_visible,6),bakedProps=True,isolateCells=True,separationAlpha=analysis[3])
  for pose_name,old in original_art['poses'].items():
   if old['atlas']!=name:continue
   index=mapping[old['frame']];cell=cells[index];g=owned_geometry(rgba,analysis,index);old_cell=old_cells[old['frame']]
   ox,oy=old_cell['x']+old['footX'],old_cell['y']+old['footY']
   # Feet are measured afresh; contact anchors follow image correspondence.
   label=analysis[0][index][0];x,y,bw,bh,_=analysis[0][index][1]
   _,xs=np.nonzero(analysis[1][y+bh-max(4,round(bh*.025)):y+bh,x:x+bw]==label)
   fx=x+float(np.median(xs)) if len(xs) else cell['x']+g['anchor'];fy=cell['y']+g['bottom']
   anchors={}
   for anchor,p in old['anchors'].items():
    px,py=mapper(ox+p['x'],oy+p['y']);anchors[anchor]=dict(x=round(px-fx,3),y=round(py-fy,3))
   front=mapper(ox,oy+old.get('frontY',0))[1]-fy if old.get('frontY') else 0
   measured=job.get('fiveMeasurementPixels',{}).get(pose_name,{})
   for anchor,p in measured.get('anchors',{}).items():
    if anchor not in old['anchors'] or not(cell['x']<=p[0]<=cell['x']+cell['width'] and cell['y']<=p[1]<=cell['y']+cell['height']):raise ValueError(f'{job["key"]}/{pose_name}: measured anchor outside its source crop')
    anchors[anchor]=dict(x=round(p[0]-fx,3),y=round(p[1]-fy,3))
   if 'frontY' in measured:
    if not cell['y']<=measured['frontY']<=cell['y']+cell['height']:raise ValueError('Measured box wall outside its source crop')
    front=measured['frontY']-fy
   box_width=measured.get('boxWidth',old.get('boxWidth',0)*ratio)
   if not 0<=box_width<=cell['width']:raise ValueError('Measured box width outside its source crop')
   art['poses'][pose_name]=dict(atlas=name,frame=index,footX=round(fx-cell['x'],3),footY=round(fy-cell['y'],3),anchors=anchors,frontY=round(front,3),boxWidth=round(box_width,3))
  reports.append(dict(key=job['key'],file=relative,sha256=hashlib.sha256(target.read_bytes()).hexdigest(),anchorMethod='visually measured source-pixel overrides where recorded; otherwise original optical correspondence; native visual review required'))
 return art,reports,missing

def stage(cid,destination):
 folder=ASSETS/cid;pet=json.loads((folder/'pet.json').read_text(encoding='utf-8-sig'))
 jobs=[]
 for path in sorted((ART/'results').glob('*.json')):
  j=json.loads(path.read_text(encoding='utf-8-sig'));review=j.get('visualReview') or {}
  if j['id']!=cid or not j.get('selected') or not review.get('passed'):continue
  for key,file in [('candidateSha256',j['source']),('originalSha256',j['references'][0])]:
   if hashlib.sha256((ROOT/file).read_bytes()).hexdigest()!=review.get(key):raise ValueError(f'Stale review: {j["key"]}')
  original=ROOT/j['references'][0]
  source_sprite=next((s for s in [pet['atlas'],*pet['motions'].values(),*pet.get('interactionFive',{}).get('atlases',{}).values(),*([pet['dizzy']] if pet.get('dizzy') else [])] if folder/s['file']==original),{})
  j.setdefault('columns',source_sprite.get('columns',1));j.setdefault('rows',source_sprite.get('rows',1))
  jobs.append(j)
 bases=[j for j in jobs if j['kind'] in ('basic','original-basic','original-six','portrait','original-portrait')]
 if len(bases)!=1:raise ValueError(f'{cid}: expected one reviewed base, found {len(bases)}')
 base=bases[0];base_rgba=np.asarray(Image.open(ROOT/base['source']).convert('RGBA'))
 original_idle=np.asarray(Image.open(folder/pet['atlas']['file']).convert('RGBA'))
 original_idle_cells=rectangles(original_idle,pet['atlas']);original_visible=crop_geometry(original_idle,original_idle_cells[0])['visible']
 ratio=base_rgba.shape[1]/original_idle.shape[1]
 base_expected=[{k:round(v*ratio) for k,v in c.items()} for c in original_idle_cells]
 base_analysis=components(base_rgba,base['columns']*base['rows'],base_expected)
 # Keep the original idle canvas extent. Tightening only its crop would change
 # the height assumed by every effect and by the five-interaction renderer.
 base_analysis=(base_analysis[0],base_analysis[1],base_expected,base_analysis[3])
 neutral_visible=crop_geometry(base_rgba,base_expected[0])['visible']
 outfit=copy.deepcopy(pet['outfits']['sports']);outfit['motions']={}
 out=destination/'Assets/Characters'/cid;out.mkdir(parents=True,exist_ok=True)
 report=[]
 for j in jobs:
  if j['kind'].startswith('five-'):continue
  original=ROOT/j['references'][0];source_rgba=np.asarray(Image.open(original).convert('RGBA'));rgba=np.asarray(Image.open(ROOT/j['source']).convert('RGBA'))
  resolution=rgba.shape[1]/source_rgba.shape[1]
  if abs(rgba.shape[0]-source_rgba.shape[0]*resolution)>2:raise ValueError('Canvas edge difference exceeds two pixels: '+j['key'])
  count=j['columns']*j['rows'];definitions={}
  if j==base:
   sprite=pet['atlas'];definitions={'idle':dict(frames=[0],frameMs=[5000],loop=False),'listen':dict(frames=[0],frameMs=[5000],loop=False)}
   if count==6:
    definitions.update({k:dict(frames=[i],frameMs=[5000],loop=False) for k,i in [('happy',1),('sleep',3),('sad',5),('faint',4)]})
  elif j['kind']=='dizzy':
   sprite=pet['dizzy'];definitions={'dizzy':dict(frames=[0],frameMs=[5000],loop=False)}
  elif j['kind']=='emotions':
   sprite=pet['outfits']['sports']['idle']
   for key,(frames,times,loop) in module('sports-art').clips('portrait').items():
    if key not in ('idle','listen'):definitions[key]=dict(frames=frames,frameMs=times,loop=loop)
  elif j.get('clips'):
   sprite=copy.deepcopy(pet['interactionFive']['atlases'][j['sourceAtlas']])
   if j.get('physicalOrder'):sprite.pop('cells',None)
   definitions=j['clips']
  else:
   candidates=[(key,s) for key,s in pet['motions'].items() if folder/s['file']==original]
   if not candidates:raise ValueError('No original motion definitions: '+j['key'])
   sprite=candidates[0][1]
   for key,s in candidates:
    frames=s.get('frames') or list(range(count))
    definitions[key]=dict(frames=frames,frameMs=s.get('frameMs') or [240]*len(frames),loop=s.get('loop',True))
   if j['kind']=='body':
    for action in ('ball-ready','ball-hit','anticipate'):definitions.pop(action,None)
   if j['kind']=='contact' and pet.get('category','chibi')!='chibi':
    definitions.update({'ball-ready':dict(frames=[6],frameMs=[5000],loop=False),'anticipate':dict(frames=[6],frameMs=[5000],loop=False),'ball-hit':dict(frames=[6,7,8],frameMs=[160,380,900],loop=False)})
   if 'jump' in definitions and pet.get('category','chibi')=='chibi':definitions['land']=dict(frames=[count-1],frameMs=[400],loop=False)
  source_cells=rectangles(source_rgba,sprite)
  expected=[{k:round(v*resolution) for k,v in c.items()} for c in source_cells]
  try:analysis=base_analysis if j==base else components(rgba,count,expected,j.get('separationMinAlpha',80))
  except ValueError as error:raise ValueError(f'{j["key"]}: {error}') from error
  if j['kind']=='emotions':
   # New emotion drawings are authored from the approved long-legged portrait,
   # not calibrated back to the rejected short-bodied pose guide.
   height=analysis[0][0][1][3];scales=[.7/height]*count
  else:
   old_scales=source_scale(folder,pet,sprite,source_rgba,source_cells,original_visible,j!=base,'walk' if 'walk' in definitions else '')
   scales=[s*.7/neutral_visible/resolution for s in old_scales]
  digest=hashlib.sha256((ROOT/j['source']).read_bytes()).hexdigest()
  data,cells,threshold=authored(rgba,analysis,definitions,scales,digest)
  contacts=measured_contacts(j,rgba,analysis)
  relative='outfits/sports/identity-'+j['kind']+'.png';target=out/relative;target.parent.mkdir(parents=True,exist_ok=True)
  shutil.copyfile(ROOT/j['source'],target);target.with_suffix('.json').write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
  for key,d in definitions.items():
   value=dict(file=relative,columns=j['columns'],rows=j['rows'],cells=cells,isolateCells=True,separationAlpha=threshold,referenceHeightPixels=data['poses'][d['frames'][0]]['referenceHeight'],**d)
   # Retain the explicit calibration authored for historically small source
   # poses. The native mesh already includes it; metadata makes it auditable
   # and lets scale regression distinguish it from accidental crop scaling.
   if sprite.get('frameScaleFactors'):value['frameScaleFactors']=sprite['frameScaleFactors']
   if key in ('eat','meal','feed','build','comb','wipe','bonk','bubbles'):value['bakedProps']=True
   if key=='walk':value.update(facing=sprite.get('facing','right'),walkStride=pet['outfits']['sports']['motions'].get('walk',{}).get('walkStride',0))
   if key=='idle':outfit['idle']=value
   # Explicit measured points may be added after reviewing the staged render.
   for field in ('hands','bubbleSources','effectAnchors'):
    if field in contacts:value[field]=contacts[field]
    elif j.get(field):value[field]=j[field]
   outfit['motions'][key]=value
  report.append(dict(key=j['key'],file=relative,sha256=digest,original=j['references'][0],actions=list(definitions),scales=scales,transitions=len(data['flow'])))
 five,five_report,five_missing=stage_five(folder,pet,jobs,out,original_visible,neutral_visible)
 # The approved high-five drawings also contain the original open-hand wave.
 # Give farewell its own authored sequence without changing the five actions.
 if pet.get('category','chibi')!='chibi' and 'farewell' not in outfit['motions']:
  candidate=next((j for j in jobs if j['kind']=='five-highfive'),None)
  if candidate:
   source=ROOT/candidate['source'];rgba=np.asarray(Image.open(source).convert('RGBA'))
   source_sprite=next(s for s in pet['interactionFive']['atlases'].values() if folder/s['file']==ROOT/candidate['references'][0])
   cells=next(s['cells'] for s in five['atlases'].values() if s['file'].endswith('identity-five-highfive.png'))
   analysis=components(rgba,4,cells)
   resolution=rgba.shape[1]/Image.open(ROOT/candidate['references'][0]).width
   scale=.7*original_visible/(neutral_visible*source_sprite['referenceHeightPixels']*resolution)
   definitions={'farewell':dict(frames=[0,1,3,0],frameMs=[300,700,700,350],loop=False)}
   digest=hashlib.sha256(source.read_bytes()).hexdigest();data,measured,threshold=authored(rgba,analysis,definitions,[scale]*4,digest)
   relative='outfits/sports/identity-farewell.png';target=out/relative;shutil.copyfile(source,target);target.with_suffix('.json').write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
   outfit['motions']['farewell']=dict(file=relative,columns=4,rows=1,cells=measured,isolateCells=True,separationAlpha=threshold,referenceHeightPixels=.7/scale,**definitions['farewell'])
 outfit['interactionFive']=five
 pet['outfits']['sports']=outfit
 module('sports-art').write_manifest(out/'pet.json',pet)
 result=dict(character=cid,stageOnly=True,nativeVerified=False,artwork=report,missing=[k for k in ['walk','chat','think','headpat','poke','tickle','eat','meal','jump','land','curl','sleep','build','pickup','ball-ready','ball-hit','ball-miss','bonk','dizzy','happy','farewell','comb','wipe','stars','bubbles','stretch'] if k not in outfit['motions']])
 result.update(interactionFive=five_report,missingFive=five_missing,readyForNativeVerification=not result['missing'] and not five_missing)
 (out/'sports-identity-stage.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 return result

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('character');p.add_argument('--output',default='.artifacts/sports-identity-stage');a=p.parse_args();dest=(ROOT/a.output).resolve()
 if not dest.is_relative_to((ROOT/'.artifacts').resolve()):raise ValueError('Staging must remain under project .artifacts')
 result=stage(a.character,dest);print(json.dumps(dict(character=a.character,sheets=len(result['artwork']),missing=result['missing'],stage=str(dest),nativeVerified=False),ensure_ascii=False))
