"""Persist expansion progress and geometry failures without changing image pixels."""
import json,re
from pathlib import Path
from importlib.util import spec_from_file_location,module_from_spec
root=Path(__file__).resolve().parents[1]
s=spec_from_file_location('five',root/'tools/install-five-art.py');m=module_from_spec(s);s.loader.exec_module(m)
jobs=[]
for file in ['adult-deepseek-prompts.json','adult-gpt-claude-prompts.json','adult-remaining-prompts.json']:
 jobs+=json.loads((m.ART/file).read_text(encoding='utf-8-sig'))
reports=[]
for j in jobs:
 candidates=sorted(m.ART.glob(j['key']+'-v*.png'),key=lambda p:int(re.search(r'-v(\d+)\.png$',p.name)[1]),reverse=True)
 result=dict(key=j['key'],character=j['character'],outfit=j['outfit'],candidates=[])
 for file in candidates:
  version=int(re.search(r'-v(\d+)\.png$',file.name)[1]);rows=2 if j['outfit']=='wedding' and version>=3 else j.get('rows',1)
  if file.name=='gpt-adult-swim-highfive-v2.png':rows=2
  if file.name=='claude-adult-wedding-handoff-v2.png':rows=2
  try:
   poses,ref=m.measure(file,j['poses'],rows)
   heights=[p['bounds'][3] for p in poses.values()]
   variation=max(heights)/min(heights)-1
   result['candidates'].append(dict(file=file.name,ok=variation<=.035,rows=rows,referenceHeight=ref,heightVariation=round(variation,5),heights=heights,issue='' if variation<=.035 else 'standing height variation above 3.5%'))
  except (AssertionError,ValueError) as e:result['candidates'].append(dict(file=file.name,ok=False,rows=rows,issue=str(e)))
 result['geometryCandidate']=next((c['file'] for c in result['candidates'] if c['ok']),None)
 reports.append(result)
selected=json.loads((m.ART/'selected-expansion.json').read_text()) if (m.ART/'selected-expansion.json').exists() else []
status=dict(adultGroups=len(reports),generatedGroups=sum(bool(r['candidates']) for r in reports),geometryPassedGroups=sum(bool(r['geometryCandidate']) for r in reports),missing=[r['key'] for r in reports if not r['candidates']],needsRepair=[r['key'] for r in reports if r['candidates'] and not r['geometryCandidate']],reviewedExpansionAppearances=len(selected),note='Geometry candidates still require visual identity, anatomy and contact review; this is not a completion report.',groups=reports)
(m.ART/'expansion-progress.json').write_text(json.dumps(status,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in status.items() if k not in ['groups','missing']}))
