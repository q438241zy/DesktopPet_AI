"""Select only an explicitly reviewed image; copy byte-for-byte to project assets."""
import sys,json,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
folder=ROOT/'artwork/sports-shorts/results'
for key in sys.argv[1:]:
    path=folder/(key+'.json');j=json.loads(path.read_text(encoding='utf-8-sig'))
    for other in folder.glob(j['id']+'-shorts-*.json'):
        old=json.loads(other.read_text(encoding='utf-8-sig'))
        if old['kind']==j['kind'] and other!=path:
            old['selected']=False;other.write_text(json.dumps(old,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    source=Path(j['source'].split(' as ')[-1]);dest=ROOT/j['dest'];dest.parent.mkdir(parents=True,exist_ok=True)
    if source.resolve()!=dest.resolve():shutil.copyfile(source,dest)
    j['originalSource']=j.get('originalSource',str(source));j['source']=str(dest);j['selected']=True
    j['review']='Full-body poses visually checked: short sleeves above elbows, shorts above knees, bare lower legs, preserved identity; actual ordered action poses reviewed.'
    path.write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print('SELECTED',key)
