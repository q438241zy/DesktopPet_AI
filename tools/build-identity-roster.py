"""Copy untouched identity edits and their source images into the comparison Demo."""
import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/DesktopPet.App/Assets/Characters'
DEMO = ROOT / 'docs/demo/wardrobe-identity'
FAMILIES = [('whale', 'DeepSeek', 'deepseek-adult')] + [(s, n, s+'-adult') for s, n in [('gpt','GPT'),('claude','Claude'),('gemini','Gemini'),('grok','Grok'),('qwen','Qwen'),('zhipu','GLM'),('kimi','Kimi')]]
roster = {}
provenance = []

def copy(source, name, sprite=None):
    target = DEMO / 'art/roster' / (name + source.suffix)
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists() or source.read_bytes() != target.read_bytes():
        shutil.copyfile(source, target)
    provenance.append(dict(source=source.relative_to(ROOT).as_posix(), file=target.relative_to(DEMO).as_posix(), sha256=hashlib.sha256(source.read_bytes()).hexdigest()))
    result = dict(file=target.relative_to(DEMO/'art').as_posix(), columns=1, rows=1)
    if sprite:
        result.update({key:sprite[key] for key in ['columns','rows','cells'] if key in sprite})
    return result

for family, name, adult in FAMILIES:
    entry = dict(name=name)
    for style, cid in [('chibi', family), ('realistic', adult)]:
        folder = ASSETS / cid
        pet = json.loads((folder/'pet.json').read_text(encoding='utf-8-sig'))
        original = copy(folder/pet['atlas']['file'], cid+'/original', pet['atlas'])
        gallery = dict(original=original)
        for outfit in ['swim','wedding','sports']:
            sprite = pet['outfits'][outfit]['idle']
            gallery[outfit] = copy(folder/sprite['file'], cid+'/'+outfit+'-before', sprite)
        if family == 'whale':
            selected = DEMO/'art'/('deepseek-chibi-sports-v1.png' if style == 'chibi' else 'deepseek-realistic-sports-v1.png')
        else:
            selected = ROOT/'artwork/sports-identity'/(cid+('-basic-v1.png' if style == 'chibi' else '-portrait-v1.png'))
        value = dict(original=original, candidate=copy(selected,cid+'/sports-identity',dict(columns=3,rows=2) if style=='chibi' else None), gallery=gallery)
        if style == 'chibi':
            sprite = pet['motions']['walk']
            value['walkOriginal'] = copy(folder/sprite['file'],cid+'/walk-original',sprite)
            walk = DEMO/'art/deepseek-chibi-sports-walk-v2.png' if family=='whale' else ROOT/'artwork/sports-identity'/(cid+'-walk-v1.png')
            value['walkCandidate'] = copy(walk,cid+'/walk-identity',dict(columns=3,rows=4))
        entry[style] = value
    roster[family] = entry
(DEMO/'roster.js').write_text('window.identityRoster='+json.dumps(roster,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
(DEMO/'art/roster/provenance.json').write_text(json.dumps(dict(scope='8 families, original-source clothing edits; action expansion in progress', sources=provenance),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'{len(roster)} families, {len(provenance)} source/candidate references copied without pixel changes')
