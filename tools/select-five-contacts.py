"""Convert visually reviewed atlas coordinates into measured native anchors."""
import argparse,json
from pathlib import Path
from PIL import Image
from importlib.util import spec_from_file_location,module_from_spec
root=Path(__file__).resolve().parents[1]
s=spec_from_file_location('five',root/'tools/install-five-art.py');m=module_from_spec(s);s.loader.exec_module(m)
def main():
 parser=argparse.ArgumentParser();parser.add_argument('review');args=parser.parse_args()
 review=json.loads(Path(args.review).read_text(encoding='utf-8-sig'))
 jobs=json.loads((m.ART/review['manifest']).read_text(encoding='utf-8-sig'))
 selection=m.ART/'selected-expansion.json';entries=json.loads(selection.read_text()) if selection.exists() else []
 for outfit,p in review['points'].items():
  groups=[j for j in jobs if j['character']==review['character'] and j['outfit']==outfit]
  if len(groups)!=5:raise ValueError('Expected five reviewed groups')
  for j in groups:
   override=review.get('sources',{}).get(outfit,{}).get(j['group'])
   if override:j.update(filename=override['file'],rows=override['rows'])
  anchors={};metadata={};images={}
  for j in groups:
   measured,_=m.measure(m.ART/j['filename'],j['poses'],j['rows']);metadata.update(measured)
   for name in measured:images[name]=(*Image.open(m.ART/j['filename']).size,p.get('sizes',{}).get(j['group'],p['size']))
  def anchor(name,key,xy):
   iw,ih,(dw,dh)=images[name];x,y,w,h=metadata[name]['bounds']
   ax,ay=(xy[0]*iw/dw-x)/w,(xy[1]*ih/dh-y)/h
   if not 0<=ax<=1 or not 0<=ay<=1:raise ValueError((name,'contact outside character',ax,ay))
   anchors.setdefault(name,{'anchors':{}})['anchors'][key]=[round(ax,6),round(ay,6)]
  for name,xy in zip(['high-prep','high-ready','high-contact','high-recoil'],p['high']):anchor(name,'hand',xy)
  anchor('receive','receive',p['receive']);anchor('gift','gift',p['gift']);anchor('gift-empty','giftSlot',p['slot'])
  iw,ih,(dw,dh)=images['gift-empty'];x,y,w,h=metadata['gift-empty']['bounds']
  anchors['gift-empty'].update(frontY=round((p['front']*ih/dh-y)/h,6),boxWidth=round(p['box']*iw/dw/w,6))
  anchor('read','book',p['book']);anchor('page','book',p['page'])
  entries=[e for e in entries if (e['character'],e['outfit'])!=(review['character'],outfit)]
  entries.append(dict(character=review['character'],outfit=outfit,groups=[dict(key=j['group'],file=j['filename'],poses=j['poses'],rows=j['rows']) for j in groups],anchors=anchors))
 selection.write_text(json.dumps(entries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('Selected '+review['character']+' '+str(len(review['points']))+' reviewed appearances')
if __name__=='__main__':main()
