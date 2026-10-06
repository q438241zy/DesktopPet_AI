"""Install reviewed legacy care originals and audit all 48 wardrobe appearances.
Pixels are never altered. Crop/flow measurements and pose selections are metadata.
"""
import argparse, hashlib, importlib.util, json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'artwork/care-contact'
spec=importlib.util.spec_from_file_location('sports_art',ROOT/'tools/sports-art.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
OUTFITS=['original','swim','wedding']
IDS=['whale','gpt','claude','gemini','grok','qwen','zhipu','kimi','deepseek-adult','gpt-adult','claude-adult','gemini-adult','grok-adult','qwen-adult','zhipu-adult','kimi-adult']

def selected(ids):
    jobs=[];seen=set()
    for path in sorted((ART/'results').glob('*.json')):
        job=json.loads(path.read_text(encoding='utf-8-sig'))
        if not job.get('selected') or job['id'] not in ids:continue
        if job.get('kind')!='care':raise ValueError(f'{path}: expected care')
        for outfit in job['outfits']:
            key=(job['id'],outfit)
            if key in seen:raise ValueError(f'duplicate selected care: {key}')
            seen.add(key)
        count=len(job.get('indices',[])) or job['columns']*job['rows']
        # First pose of each actual four-pose action row is the neutral height.
        # Combing and wiping often occupy different raster row scales; this
        # compensates source layout, without resizing individual gesture frames.
        job.setdefault('referenceGroups',[i//4*4 for i in range(count)])
        jobs.append(job)
    return jobs

def definitions(job):
    if job.get('clips'):return job['clips']
    result={}
    for o in range(len(job['outfits'])):
        result[f'{o}:comb']=([o*8+i for i in [0,1,2,1,2,3]],[450,750,900,750,1100,850],False)
        result[f'{o}:wipe']=([o*8+i for i in [4,5,6,5,6,7]],[400,600,850,600,1050,700],False)
    return result

def measure(job):
    return art.measure(job['source'],job['columns'],job['rows'],definitions(job),reference_index=job.get('referenceIndex',0),reference_groups=job['referenceGroups'],row_counts=job.get('rowCounts'),indices=job.get('indices'),reference_pixels=job.get('referenceHeightPixels',0))

def verify(ids):
    rows=[];failures=[]
    for ident in ids:
        folder=art.ASSETS/ident;pet=json.loads((folder/'pet.json').read_text(encoding='utf-8-sig'))
        for outfit in OUTFITS:
            appearance=pet if outfit=='original' else pet['outfits'][outfit]
            row={'id':ident,'outfit':outfit,'actions':{}}
            for key in ['comb','wipe']:
                try:
                    sprite=appearance['motions'].get(key)
                    assert sprite, 'no wardrobe-owned motion'
                    assert sprite['file'].startswith('motions/cloud-care-'),'wrong dedicated care file'
                    assert sprite.get('bakedProps') is True,'double-prop prevention missing'
                    assert sprite.get('isolateCells') is True,'isolated cell mask missing'
                    assert len(set(sprite['frames']))>=3,'fewer than three actual gesture poses'
                    assert len(sprite['frameMs'])==len(sprite['frames']) and all(t>0 for t in sprite['frameMs']),'invalid dwell times'
                    sheet=folder/sprite['file'];meta=json.loads(sheet.with_suffix('.json').read_text())
                    assert meta['version']==2 and meta['sha256']==hashlib.sha256(sheet.read_bytes()).hexdigest(),'sidecar/source mismatch'
                    assert len(meta['poses'])==len(sprite['cells']),'crop/pose count mismatch'
                    for index in sprite['frames']:assert 0<=index<len(meta['poses']),'out of bounds frame'
                    for a,b in zip(sprite['frames'],sprite['frames'][1:]):
                        assert a==b or f'{a}:{b}' in meta['flow'],'missing continuous pose transition'
                    heights=[meta['poses'][i]['referenceHeight'] for i in sprite['frames']]
                    assert max(heights)==min(heights),'reference plane changes within gesture'
                    row['actions'][key]={'file':sprite['file'],'frames':sprite['frames'],'uniquePoses':len(set(sprite['frames'])),'referenceHeight':heights[0],'sha256':meta['sha256']}
                except Exception as e:failures.append(f'{ident}/{outfit}/{key}: {e}')
            if len(row['actions'])==2:rows.append(row)
    report={'expectedAppearances':len(ids)*3,'completeAppearances':len(rows),'fullMatrix':len(ids)==16,'appearances':rows,'failures':failures}
    (ART/'coverage.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f"Care coverage: {len(rows)}/{len(ids)*3} appearances; fullMatrix={len(ids)==16}")
    for error in failures:print('FAIL',error)
    if failures:raise SystemExit(1)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--measure',action='store_true');parser.add_argument('--install',action='store_true');parser.add_argument('--verify',action='store_true');parser.add_argument('--ids',nargs='+');args=parser.parse_args();ids=args.ids or IDS
    if args.measure or args.install:
        report=[]
        for job in selected(ids):
            if args.install:result=art.install(job)
            else:
                data,cells,threshold=measure(job);(ART/'staged').mkdir(exist_ok=True)
                (ART/'staged'/(job['key']+'.json')).write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
                result={'key':job['key'],'cells':len(cells),'threshold':threshold,'transitions':len(data['flow'])}
            report.append(result);print('PASS',job['key'],flush=True)
        (ART/('installed.json' if args.install else 'measured.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    if args.verify:verify(ids)
