"""Install reviewed Gemini contact points; metadata only, original pixels stay intact."""
import json
from pathlib import Path
from PIL import Image
from importlib.util import spec_from_file_location,module_from_spec
root=Path(__file__).resolve().parents[1]
s=spec_from_file_location('five',root/'tools/install-five-art.py');m=module_from_spec(s);s.loader.exec_module(m)
jobs=json.loads((m.ART/'adult-remaining-prompts.json').read_text())
# Coordinates were reviewed on full atlas previews: hand palm, gift center,
# empty-box opening/front rim and book spine. Panoramas use 2048x683 previews.
points={
 'original':dict(size=(2048,683),high=[(196,141),(706,112),(1186,126),(1648,142)],receive=(1260,254),gift=(1726,246),slot=(309,222),front=232,box=79,book=(790,213),page=(1311,216)),
 'swim':dict(size=(2048,683),high=[(120,143),(695,111),(1220,130),(1701,142)],receive=(1268,267),gift=(1720,256),slot=(470,224),front=237,box=97,book=(785,211),page=(1270,216)),
 'sports':dict(size=(2048,683),high=[(235,150),(720,97),(1201,133),(1665,149)],receive=(1240,275),gift=(1703,262),slot=(371,216),front=229,box=78,book=(805,212),page=(1276,215)),
 'wedding':dict(size=(1254,1254),high=[(265,140),(824,83),(293,736),(806,760)],receive=(392,864),gift=(903,866),slot=(430,202),front=216,box=77,book=(877,196),page=(418,823)),
}
selection=m.ART/'selected-expansion.json'
entries=json.loads(selection.read_text()) if selection.exists() else []
entries=[e for e in entries if e['character']!='gemini-adult']
for outfit,p in points.items():
 groups=[j for j in jobs if j['character']=='gemini-adult' and j['outfit']==outfit]
 anchors={};metadata={};images={}
 for j in groups:
  measured,_=m.measure(m.ART/j['filename'],j['poses'],j['rows'])
  metadata.update(measured)
  for name in measured:images[name]=Image.open(m.ART/j['filename']).size
 def anchor(name,key,xy):
  iw,ih=images[name];dw,dh=p['size'];x,y,w,h=metadata[name]['bounds']
  anchors.setdefault(name,{'anchors':{}})['anchors'][key]=[round((xy[0]*iw/dw-x)/w,6),round((xy[1]*ih/dh-y)/h,6)]
 for name,xy in zip(['high-prep','high-ready','high-contact','high-recoil'],p['high']):anchor(name,'hand',xy)
 anchor('receive','receive',p['receive']);anchor('gift','gift',p['gift'])
 anchor('gift-empty','giftSlot',p['slot'])
 iw,ih=images['gift-empty'];x,y,w,h=metadata['gift-empty']['bounds']
 anchors['gift-empty'].update(frontY=round((p['front']*ih/p['size'][1]-y)/h,6),boxWidth=round(p['box']*iw/p['size'][0]/w,6))
 anchor('read','book',p['book']);anchor('page','book',p['page'])
 entries.append(dict(character='gemini-adult',outfit=outfit,groups=[dict(key=j['group'],file=j['filename'],poses=j['poses'],rows=j['rows']) for j in groups],anchors=anchors))
selection.write_text(json.dumps(entries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Selected four reviewed Gemini appearances with individual contact metadata')
