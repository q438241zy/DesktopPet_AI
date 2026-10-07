"""Deploy diagnostic comparisons; never promote artwork to native characters."""
import importlib.util
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
for draft in report['drafts']:
    row={k:draft[k] for k in ['key','character','kind','columns','rows','visualReview','selected','comparisonWindows','clips','sourceCells','reviewStale','candidateResolution','transparent','candidateSha256','originalSha256','comparisonRole']}
    row['installed']=False
    for kind in ['original','candidate']:
        source=ROOT/draft[kind]
        if kind=='original' and source.is_relative_to(audit.ASSETS):
            # Runtime original images already exist; no second full set of originals.
            target=ROOT/'Release/win-x64/Assets/Characters'/source.relative_to(audit.ASSETS)
            if not target.is_file():raise ValueError(f'Missing runtime reference: {target}')
            row[kind+'Url']='../../Assets/Characters/'+source.relative_to(audit.ASSETS).as_posix()
        else:
            target=dest/'art/motions'/source.name
            target.parent.mkdir(parents=True,exist_ok=True)
            if not target.exists() or source.read_bytes()!=target.read_bytes():shutil.copyfile(source,target)
            row[kind+'Url']='art/motions/'+source.name
    sheets.append(row)
payload='window.sportsMotionReview='+json.dumps(dict(sheets=sheets),ensure_ascii=False,separators=(',',':'))+';\n'
(dest/'motion-data.js').write_text(payload,encoding='utf-8')
print(f'Deployed {len(sheets)} comparison sheets to {dest / "motions.html"}')
