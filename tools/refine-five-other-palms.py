"""Visually reviewed Q palm centers, in original 4x5 atlas cell coordinates."""
import json
from pathlib import Path
p=Path(__file__).resolve().parents[1]/'artwork/interaction-five/chibi-contact-review-all.json'
rows=json.loads(p.read_text(encoding='utf-8-sig'))
points={
'claude/original':[[.365,.521],[.246,.435],[.205,.517],[.378,.444]],
'claude/swim':[[.315,.549],[.171,.446],[.205,.528],[.478,.526]],
'claude/wedding':[[.301,.485],[.196,.385],[.241,.474],[.374,.509]],
'gpt/original':[[.369,.603],[.228,.464],[.212,.517],[.378,.576]],
'gpt/swim':[[.344,.553],[.210,.467],[.209,.539],[.414,.590]],
'gpt/wedding':[[.305,.503],[.217,.399],[.244,.492],[.385,.538]],
'gemini/original':[[.315,.581],[.182,.364],[.212,.517],[.378,.548]],
'gemini/swim':[[.291,.524],[.210,.389],[.194,.496],[.307,.537]],
'gemini/wedding':[[.333,.535],[.278,.485],[.283,.549],[.353,.522]],
'grok/original':[[.255,.517],[.182,.439],[.198,.346],[.324,.494]],
'grok/swim':[[.319,.485],[.210,.389],[.219,.517],[.414,.452]],
'grok/wedding':[[.305,.492],[.193,.360],[.248,.471],[.357,.469]],
'kimi/original':[[.312,.549],[.210,.460],[.212,.464],[.328,.498]],
'kimi/swim':[[.280,.556],[.210,.407],[.209,.481],[.374,.551]],
'kimi/wedding':[[.312,.549],[.146,.385],[.180,.507],[.374,.512]],
'qwen/original':[[.319,.503],[.285,.499],[.276,.542],[.431,.533]],
'qwen/swim':[[.358,.542],[.225,.439],[.230,.510],[.435,.523]],
'qwen/wedding':[[.358,.478],[.314,.442],[.294,.449],[.364,.448]],
'zhipu/original':[[.383,.546],[.299,.442],[.219,.471],[.474,.551]],
'zhipu/swim':[[.373,.546],[.210,.414],[.219,.492],[.453,.501]],
'zhipu/wedding':[[.376,.517],[.274,.428],[.308,.496],[.431,.484]],
}
for row in rows:
    key=row['character']+'/'+row['outfit']
    if key in points:row['points']['high']=points[key]
p.write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
