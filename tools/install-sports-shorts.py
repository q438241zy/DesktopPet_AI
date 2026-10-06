"""Install only the separately reviewed SHORTS batch; never promote retired pants.

This only copies untouched imagegen originals and measures animation metadata.
Use --check after the asset workers have completed to validate the full wardrobe.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('sports_art',Path(__file__).with_name('sports-art.py'))
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
RESULTS=ROOT/'artwork/sports-shorts/results'
EXPECTED=['whale','gpt','claude','gemini','grok','qwen','zhipu','kimi','deepseek-adult','gpt-adult','claude-adult','gemini-adult','grok-adult','qwen-adult','zhipu-adult','kimi-adult']
REQUIRED=['idle','listen','walk','chat','think','headpat','poke','tickle','eat','meal','jump','land','curl','sleep','build','pickup','ball-ready','anticipate','ball-hit','ball-hold','ball-miss','bonk','dizzy','happy','farewell','stars','bubbles','stretch','comb','wipe']

def check():
    rows=[];errors=[]
    reviewed=set()
    for record in RESULTS.glob('*.json'):
        job=json.loads(record.read_text(encoding='utf-8-sig'))
        if not job.get('selected'):continue
        target=ROOT/job['dest'];reviewed.add(target.resolve())
        if not target.is_file():errors.append(f'{job["key"]}: reviewed image missing');continue
        if 'superseded-long-pants' in job['source']:errors.append(f'{job["key"]}: withdrawn source selected')
    for cid in EXPECTED:
        path=art.ASSETS/cid/'pet.json';p=json.loads(path.read_text(encoding='utf-8-sig'))
        outfit=p.get('outfits',{}).get('sports',{});motions=outfit.get('motions',{})
        for key in REQUIRED:
            if key not in motions:errors.append(f'{cid}: missing {key}')
        files=set()
        for key,sprite in [('idle',outfit.get('idle')), *motions.items()]:
            if not sprite:continue
            file=sprite['file'];files.add(file)
            if not file.startswith('outfits/sports/'):errors.append(f'{cid}/{key}: wrong outfit {file}')
            if not (path.parent/file).is_file():errors.append(f'{cid}/{key}: missing image')
            if key in ['eat','meal','build','comb','wipe','bonk','bubbles'] and not sprite.get('bakedProps'):errors.append(f'{cid}/{key}: duplicate-prop risk')
            if key in ['ball-ready','anticipate','ball-hold']:
                hands=sprite.get('hands',[])
                for i in sprite.get('frames',range(sprite['columns']*sprite['rows'])):
                    if i>=len(hands) or hands[i] is None:errors.append(f'{cid}/{key}: missing hand contact at frame {i}')
            if key=='bubbles' and not sprite.get('bubbleSources'):errors.append(f'{cid}: missing measured bubble ring')
            if key=='walk' and not .1<=sprite.get('walkStride',0)<=1.2:errors.append(f'{cid}: missing measured walk stride')
        for file in files:
            image=path.parent/file
            if image.resolve() not in reviewed:errors.append(f'{cid}: image has no selected visual review: {file}')
            if not image.is_file():continue
            meta=image.with_suffix('.json')
            if meta.is_file():
                data=json.loads(meta.read_text(encoding='utf-8-sig'))
                if data.get('sha256')!=hashlib.sha256(image.read_bytes()).hexdigest():errors.append(f'{cid}: pose metadata is stale: {file}')
        rows.append(dict(character=cid,motions=len(motions),files=sorted(files)))
    report=dict(appearances=len(rows),rows=rows,errors=errors)
    out=ROOT/'artwork/sports-shorts/coverage.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for e in errors:print('FAIL',e)
    print(f'{len(rows)} sports appearances, {len(errors)} missing requirements.')
    if errors:raise SystemExit(1)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');parser.add_argument('--key');args=parser.parse_args()
    if args.check:check()
    else:
        jobs=[]
        for p in RESULTS.glob('*.json'):
            j=json.loads(p.read_text(encoding='utf-8-sig'))
            if j.get('selected') and (not args.key or j['key']==args.key):jobs.append(j)
        if not jobs:raise SystemExit('No reviewed shorts jobs matched.')
        for j in jobs:
            if j['id'] not in EXPECTED:raise ValueError('Unknown character')
            if 'superseded-long-pants' in j['source']:raise ValueError('Cannot install withdrawn pants art')
            print('Installed',art.install(j)['key'],flush=True)
