"""Turn visually authored per-cell contact points into measured crop metadata.

Coordinates refer to the original atlas cell, not a stretched body. PNG bytes
remain untouched. Each reviewed appearance has its own palm and prop points.
"""
import argparse,json
from pathlib import Path
from PIL import Image
from importlib.util import spec_from_file_location,module_from_spec
root=Path(__file__).resolve().parents[1]
s=spec_from_file_location('five',root/'tools/install-five-art.py');m=module_from_spec(s);s.loader.exec_module(m)
def main():
 p=argparse.ArgumentParser();p.add_argument('review');args=p.parse_args()
 reviews=json.loads(Path(args.review).read_text(encoding='utf-8-sig'))
 selection=m.ART/'selected-expansion.json';entries=json.loads(selection.read_text())
 for review in reviews:
  file=review['file'];points=review['points'];metadata,ref=m.measure(m.ART/file,m.NAMES,5)
  iw,ih=Image.open(m.ART/file).size;anchors={}
  def point(name,key,uv):
   i=m.NAMES.index(name);cx,cy=i%4,i//4;x,y,w,h=metadata[name]['bounds']
   ax=((cx+uv[0])*iw/4-x)/w;ay=((cy+uv[1])*ih/5-y)/h
   if not 0<=ax<=1 or not 0<=ay<=1:raise ValueError((file,name,'contact outside body',ax,ay))
   anchors.setdefault(name,{'anchors':{}})['anchors'][key]=[round(ax,6),round(ay,6)]
  for name,uv in zip(m.NAMES[1:5],points['high']):point(name,'hand',uv)
  for name,key in [('receive','receive'),('gift','gift'),('gift-empty','giftSlot'),('read','book'),('page','book')]:point(name,key,points[name])
  x,y,w,h=metadata['gift-empty']['bounds'];anchors['gift-empty'].update(frontY=round((3*ih/5+points['front']*ih/5-y)/h,6),boxWidth=round(points['width']*iw/4/w,6))
  entries=[e for e in entries if (e['character'],e['outfit'])!=(review['character'],review['outfit'])]
  entries.append(dict(character=review['character'],outfit=review['outfit'],groups=[dict(key='five',file=file,poses=m.NAMES,rows=5)],anchors=anchors))
 selection.write_text(json.dumps(entries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('Selected '+str(len(reviews))+' individually reviewed Q appearances')
if __name__=='__main__':main()
