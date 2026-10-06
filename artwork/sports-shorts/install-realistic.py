"""Metadata installer for visually reviewed 3D sports atlases; original pixels untouched."""
import argparse, importlib.util, json, shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('sports_art',ROOT/'tools/sports-art.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
p=argparse.ArgumentParser();p.add_argument('--kind');p.add_argument('--id');p.add_argument('--inspect',action='store_true');args=p.parse_args()
reports=[]
for path in sorted((ROOT/'artwork/sports-shorts/results').glob('*-adult-shorts-*.json')):
    j=json.loads(path.read_text(encoding='utf-8-sig'))
    if args.kind and j['kind']!=args.kind:continue
    if args.id and j['id']!=args.id:continue
    if ' as ' in j['source']:
        j['source']=j['source'].split(' as ')[-1];path.write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    if args.inspect:
        try:
            _,_,_,cells,t=art.inspect(j['source'],sum(j.get('rowCounts',[])) or j['columns']*j['rows'],j['columns'],j['rows'],j.get('rowCounts'),j.get('indices'))
            print('PASS',j['key'],len(cells),t,flush=True)
        except Exception as e:print('FAIL',j['key'],str(e),flush=True)
        continue
    if not j.get('selected'):continue
    if j['kind']=='body':
        j['clips']=j.get('clips') or art.clips('body')
        j['clips']['ball-hit']=([14,0],[900,600],False)
        j['clips']['farewell']=([0,1,0],[300,1400,400],False)
    elif j['kind']=='catch':
        j['clips']=j.get('clips') or art.clips('catch');j['clips'].pop('ball-hit',None)
    path.write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    try:
        report=art.install(j);reports.append(report);print('PASS',j['key'],flush=True)
    except Exception as e:print('FAIL',j['key'],str(e),flush=True)
if not args.inspect:
    out=ROOT/'artwork/sports-shorts/realistic-coverage.json'
    old={r['key']:r for r in json.loads(out.read_text())} if out.exists() else {}
    old.update({r['key']:r for r in reports});out.write_text(json.dumps(list(old.values()),indent=2)+'\n',encoding='utf-8')
