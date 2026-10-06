"""Persist small, resumable ImageGen jobs; this script never generates or edits pixels."""
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'artwork/interaction-five'
def read(n):return json.loads((ART/n).read_text(encoding='utf-8-sig'))
def save(n,v):(ART/n).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
base=read('adult-deepseek-prompts.json')+read('adult-gpt-claude-prompts.json')
groups={j['group']:(j['poses'],j['prompt'].split('LEFT TO RIGHT:')[-1]) for j in base if j['character']=='claude-adult' and j['outfit']=='original'}
grid='''LAYOUT: FOUR complete FULL BODY figures, TWO COLUMNS and TWO ROWS, square 2048x2048 transparent atlas. The whole top row fits y=6% to 43%; the whole bottom row fits y=56% to 93%. Copy the SAME full-height base body at EXACTLY the same scale into every cell, then change ONLY arms/face for the gestures. Heads, waist, knees and ankles remain at identical cell-relative levels. Leave empty transparent space below all four pairs of shoes and between all hair and gowns. Keep full gown and veil inside each cell. No overlapping people. Do not crop or shorten anyone to fit the canvas. Order: top-left, top-right, bottom-left, bottom-right. '''
wide='''LAYOUT: FOUR full-body figures in ONE HORIZONTAL ROW, 4 columns x 1 row, panoramic 3:1 canvas, approximately 3072x1024 pixels. Each head-to-shoe complete figure occupies at most 18% of the canvas width and 84% of height. Broad EMPTY TRANSPARENT gutters separate ALL hair and skirts. Copy the SAME full-height base body and legs at exactly the same scale into all four columns; ONLY arms and face change. All heads, waists, knees and ankles stay at the same horizontal levels. Feet share baseline y=93%, leaving empty space below ALL shoes. Never crop or shorten legs. Order left to right. '''
anatomy=''' Preserve reference portrait mature adult 25+ face, natural small head, long thighs and shins, about 7.5 head lengths tall. Photorealistic 3D digital human rendering. Preserve identity, face, hairstyle, ornament and exact outfit/footwear. Body and legs cannot shrink in later cells. No chibi or doll anatomy. Accurate connected fingers/wrists. No shadows, floor, backdrop, text, labels or grid. True transparent alpha. '''
failed={r['key'] for r in read('expansion-measurements.json') if not r['ok']}
failed.discard('claude-adult-wedding-handoff')
failed.add('gpt-adult-swim-highfive')
repairs=[]
for old in base:
 if old['key'] not in failed:continue
 j=dict(old);rows=2 if j['outfit']=='wedding' else 1
 start=j['prompt'].split('FORMAT')[0]
 if start.startswith('Create ONE horizontal row'):start=start.replace('ONE horizontal row of','an atlas of')
 j.update(filename=re.sub(r'-v\d+\.png$','-v3.png',j['filename']),rows=rows,prompt=start+anatomy+(grid if rows==2 else wide)+' FOUR POSES: '+groups[j['group']][1])
 repairs.append(j)
save('adult-isolation-repairs-v3.json',repairs)
identities={
 'gemini-adult':'Gemini: long violet-blue hair with pink/cyan tips, gold and pink heterochromia, cat ears and tail, star hairclip',
 'grok-adult':'Grok: long golden twin-tails, blue eyes, black/gold ribbons, orbital ornaments',
 'kimi-adult':'Kimi: silvery lavender hair fading pale blue, violet eyes, navy ribbon and moon ornament',
 'qwen-adult':'Qwen: periwinkle blue hair with two front braids, violet eyes, Chinese knot earrings and small navy/gold hat',
 'zhipu-adult':'GLM: long charcoal hair fading blue, blue eyes, black cat ears and tail, navy tassels and Z ornaments'}
more=[]
for character,identity in identities.items():
 for outfit in ['original','swim','wedding','sports']:
  folder=ROOT/'src/DesktopPet.App/Assets/Characters'/character
  ref=folder/({'original':'portrait.png','swim':'outfits/swim.png','wedding':'outfits/wedding.png','sports':'outfits/sports/portrait.png'}[outfit])
  refs=[ref.as_posix()]+([(folder/'portrait.png').as_posix()] if outfit!='original' else [])
  extra=' Sports uniform MUST be short sleeves ending above elbows, athletic shorts ending ABOVE KNEES, white sneakers. Reference 2 supplies mature body proportions only, never its maid clothes.' if outfit=='sports' else ' Reference 2, if present, supplies mature body proportions and identity only, never its other clothes.'
  for group,(poses,description) in groups.items():
   key=f'{character}-{outfit}-{group}';rows=2 if outfit=='wedding' else 1
   prompt=f'Create production game interaction keyframes of our exact existing ADULT {identity}. Reference 1 defines the exact {outfit} outfit and colors to KEEP in every frame. '+extra+anatomy+(grid if rows==2 else wide)+' FOUR POSES: '+description
   more.append(dict(key=key,character=character,outfit=outfit,group=group,poses=poses,filename=key+'-v1.png',references=refs,rows=rows,prompt=prompt))
save('adult-remaining-prompts.json',more)
print(json.dumps({'repairs':len(repairs),'remaining':len(more)}))
