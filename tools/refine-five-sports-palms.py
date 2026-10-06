"""Apply visually reviewed palm centers; original PNG pixels remain unchanged."""
import json
from pathlib import Path
p=Path(__file__).resolve().parents[1]/'artwork/interaction-five/chibi-contact-review-all.json'
rows=json.loads(p.read_text(encoding='utf-8-sig'))
points={
'whale':[[.319,.539],[.257,.428],[.298,.524],[.371,.612]],
'gpt':[[.373,.621],[.217,.478],[.258,.578],[.463,.512]],
'claude':[[.369,.574],[.267,.417],[.255,.546],[.374,.512]],
'gemini':[[.316,.578],[.189,.467],[.298,.542],[.371,.587]],
'grok':[[.355,.613],[.207,.464],[.241,.492],[.435,.548]],
'qwen':[[.376,.528],[.307,.435],[.298,.546],[.488,.498]],
'zhipu':[[.312,.560],[.264,.474],[.298,.560],[.392,.540]],
'kimi':[[.340,.521],[.267,.510],[.333,.539],[.424,.548]],
}
for row in rows:
    if row['outfit']=='sports':row['points']['high']=points[row['character']]
p.write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
