"""Apply manually inspected coordinates; measure source alpha, never alter pixels."""
import importlib.util,json
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('sports_art',ROOT/'tools/sports-art.py');art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
manual_path=ROOT/'artwork/sports-shorts/realistic-manual-measurements.json'
m=json.loads(manual_path.read_text())
m['hands']['zhipu']=[[173,1240,46],[414,1202,108],[642,1225,50],[876,1239,48]]
m['bubbleRingPixels']={
 'deepseek':[[8,122,781],[9,425,672],[10,616,640],[11,846,641],[12,182,941],[15,860,972]],
 'gpt':[[8,123,828],[9,441,726],[10,628,690],[11,857,690],[12,177,999],[15,880,1037]],
 'claude':[[8,112,836],[9,424,733],[10,622,690],[11,849,690],[12,176,1004],[15,873,1048]],
 'gemini':[[8,122,798],[9,429,705],[10,622,663],[11,857,664],[12,180,961],[15,875,1001]],
 'grok':[[8,138,831],[9,447,735],[10,649,695],[11,876,695],[12,186,1002],[15,890,1040]],
 'qwen':[[8,108,858],[9,435,752],[10,630,709],[11,865,712],[12,181,1030],[15,887,1071]],
 'zhipu':[[8,121,819],[9,436,723],[10,636,683],[11,875,688],[12,179,992],[15,891,1030]],
 'kimi':[[8,125,834],[9,432,731],[10,627,695],[11,862,694],[12,182,999],[15,881,1038]]}
# Contact heel positions manually read on the first walk8 frame at source resolution.
m['walkHeelPixels']={'deepseek':[111,334],'gpt':[84,325],'claude':[83,322],'gemini':[106,335],'grok':[93,342],'qwen':[71,309],'zhipu':[106,337],'kimi':[94,340]}
manual_path.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for path in sorted((ROOT/'artwork/sports-shorts/results').glob('*-adult-shorts-*.json')):
 j=json.loads(path.read_text(encoding='utf-8-sig'))
 if not j.get('selected'):continue
 family=j['id'].removesuffix('-adult');kind=j['kind']
 if kind not in ['catch','club','walk','care']:continue
 rgba,ordered,labels,cells,threshold=art.inspect(j['source'],j['columns']*j['rows'],j['columns'],j['rows'])
 if kind=='catch':
  contacts=[None]*len(cells)
  for index,(cx,cy,span) in enumerate(m['hands'][family],8):
   label,(x,y,w,h,area)=ordered[index];mask=labels==label
   _,xs=np.nonzero(mask[y:y+max(3,round(h*.25)),x:x+w]);anchor=x+float(np.mean(xs)) if len(xs) else x+w/2
   contacts[index]=dict(height=(cy-y)/h,offset=(cx-anchor)/h,span=span/h)
   assert 0<contacts[index]['height']<1
  j['hands']={action:contacts for action in ['ball-ready','anticipate','ball-hold']}
  j['measurement']={'palms':m['hands'][family],'basis':'source pixels: centre x/y, outer palm span; normalized to per-figure visible height and measured top-quarter head anchor'}
 elif kind=='club':
  j['referenceGroups']=[0]*len(cells);j['bubbleSources']=[None]*len(cells)
  for index,x,y in m['bubbleRingPixels'][family]:
   cell=cells[index];v=dict(x=(x-cell['x'])/cell['width'],y=(y-cell['y'])/cell['height'])
   assert 0<=v['x']<=1 and 0<=v['y']<=1,(j['key'],index,v,cell)
   j['bubbleSources'][index]=v
  j['measurement']={'bubbleRingPixels':m['bubbleRingPixels'][family],'basis':'physical atlas indices; ring centre in original pixels, normalized to measured sprite crop'}
 elif kind=='walk':
  left,right=m['walkHeelPixels'][family];height=ordered[0][1][3]
  j['walkStride']=round(2*(right-left)/height,5);j['clips']['walk'][1]=[145]*8;j['facing']='right'
  j['measurement']={'heelPixels':[left,right],'standingHeightPixels':height,'cycleMs':1160,'formula':'2 * (frontHeelX - rearHeelX) / visible standing height','basis':'manual contact-heel visual estimate at native atlas resolution'}
 else:j['referenceGroups']=[0]*len(cells)
 path.write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print('CALIBRATED',j['key'])
