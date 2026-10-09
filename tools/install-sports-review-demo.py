"""Deploy diagnostic comparisons; never promote artwork to native characters."""
import importlib.util
import hashlib
import json
import shutil
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('audit',ROOT/'tools/audit-sports-identity.py')
audit=importlib.util.module_from_spec(spec);spec.loader.exec_module(audit)
report=audit.audit()
dest=ROOT/'Release/win-x64/Demo/WardrobeIdentity'
dest.mkdir(parents=True,exist_ok=True)
for name in ['motions.html','motions.js','style.css']:
    shutil.copyfile(ROOT/'docs/demo/wardrobe-identity'/name,dest/name)
sheets=[]
runtime_files={}
for draft in report['drafts']:
    row={k:draft[k] for k in ['key','character','kind','columns','rows','visualReview','selected','comparisonWindows','clips','sourceCells','reviewStale','candidateResolution','transparent','candidateSha256','originalSha256','comparisonRole']}
    cid=draft['character']
    native=ROOT/'Release/win-x64/Assets/Characters'/cid
    if cid not in runtime_files:
        pet=json.loads((native/'pet.json').read_text(encoding='utf-8-sig'))
        outfit=pet.get('outfits',{}).get('sports',{})
        sprites=[outfit.get('idle',{}),*outfit.get('motions',{}).values(),*outfit.get('interactionFive',{}).get('atlases',{}).values()]
        runtime_files[cid]={s['file'] for s in sprites if s.get('file')}
    relative=('interactions/sports/' if draft['kind'].startswith('five-') else 'outfits/sports/')+'identity-'+draft['kind']+'.png'
    row['installed']=bool(row['selected'] and relative in runtime_files[cid] and (native/relative).is_file() and hashlib.sha256((native/relative).read_bytes()).hexdigest()==draft['candidateSha256'])
    for kind in ['original','candidate']:
        source=ROOT/draft[kind]
        if kind=='original' and source.is_relative_to(audit.ASSETS):
            # Runtime original images already exist; no second full set of originals.
            target=ROOT/'Release/win-x64/Assets/Characters'/source.relative_to(audit.ASSETS)
            if target.is_file():
                row[kind+'Url']='../../Assets/Characters/'+source.relative_to(audit.ASSETS).as_posix()
            else:
                # Superseded sports guides may be trimmed from the runtime. Keep
                # their unmodified source bytes in the historical comparison.
                relative=Path('art/references')/source.relative_to(audit.ASSETS)
                target=dest/relative;target.parent.mkdir(parents=True,exist_ok=True)
                if not target.exists() or source.read_bytes()!=target.read_bytes():shutil.copyfile(source,target)
                row[kind+'Url']=relative.as_posix()
        else:
            target=dest/'art/motions'/source.name
            target.parent.mkdir(parents=True,exist_ok=True)
            if not target.exists() or source.read_bytes()!=target.read_bytes():shutil.copyfile(source,target)
            row[kind+'Url']='art/motions/'+source.name
    sheets.append(row)
payload='window.sportsMotionReview='+json.dumps(dict(sheets=sheets),ensure_ascii=False,separators=(',',':'))+';\n'
(dest/'motion-data.js').write_text(payload,encoding='utf-8')
print(f'Deployed {len(sheets)} comparison sheets to {dest / "motions.html"}')
