"""Prepare original-pose clothing edits. Does not call any generation service.

Read and view every target before passing these jobs to built-in image_gen.
"""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'src/DesktopPet.App/Assets/Characters'
PROFILES={
 'whale':('DeepSeek','navy','cyan','small blue whale','white-blue','blue/cyan hair, fin ears, cyan bow, white frilly headband and whale tail'),
 'gpt':('GPT','deep emerald green','green','small green knot','white-green','silver/lavender hair, violet eyes, horns, pointed ears, knot hair ornament and ahoge'),
 'claude':('Claude','chestnut brown','warm orange','small orange sun','white-orange','copper-orange hair, eyeglasses, warm brown eyes, sunflower hair clip and black ribbon'),
 'gemini':('Gemini','navy','violet','small violet four-point star','white-blue-violet','blue-purple gradient hair, cat ears, pink and gold heterochromatic eyes, star hair clip and purple cat tail'),
 'grok':('Grok','charcoal black','gold','small gray ringed planet','white-gold','golden blonde flowing twin tails, blue eyes, black/gold ribbons and ringed-planet hair clip'),
 'qwen':('Qwen','navy','light blue','small blue flower','white-blue','blue-lavender hair, twin braids, purple eyes, tilted dark-blue hat with flower and tassels, bow earrings'),
 'zhipu':('GLM','navy','pale blue','small gray emblem','white-blue','black/blue-gray hair, blue eyes, black cat ears and tail, white frilly headband, black eyelash sleep-mask ornament on head, Z tag and blue tassels'),
 'kimi':('Kimi','navy','silver lavender','small crescent moon','white-lavender-blue','silver lavender hair, purple eyes, white frilly headband, silver crescent ornament, navy ribbon and pearl dangles')
}

def prepare(cid,kinds):
 family='whale' if cid=='deepseek-adult' else cid.removesuffix('-adult')
 name,color,piping,emblem,shoe,identity=PROFILES[family]
 adult=cid.endswith('-adult'); folder=ASSETS/cid
 pet=json.loads((folder/'pet.json').read_text(encoding='utf-8-sig'))
 records=[json.loads(p.read_text(encoding='utf-8-sig')) for p in (ROOT/'artwork/sports-identity/results').glob('*.json')]
 lookup={'body':'listen','contact':'meal','club':'stars','care':'comb','touch':'poke'}
 jobs=[]
 for kind in kinds:
  if kind.startswith('emotion-'):
   if not adult:raise ValueError('Emotion strips are for adult characters')
   key=f'{cid}-{kind}-v1'
   if (ROOT/'artwork/sports-identity/results'/f'{key}.json').exists():continue
   reference=next(j for j in records if j['id']==cid and j['kind']=='five-photo' and j.get('selected'))
   original=ROOT/reference['source']
   if hashlib.sha256(original.read_bytes()).hexdigest()!=reference['visualReview']['candidateSha256']:raise ValueError('Stale photo reference')
   width,height=Image.open(original).size
   gestures={
    'emotion-touch':'1 gently close eyes and smile while being petted, relaxed hands; 2 right hand softly cups right cheek; 3 left hand softly cups left cheek; 4 laugh with shoulders slightly raised and both hands open near chest as if tickled',
    'emotion-mood':'1 warm happy smile with relaxed hands; 2 a slightly disappointed pout with relaxed hands; 3 a gentle self-hug with hands around opposite upper arms; 4 dizzy expression with eyes shut and one hand resting against the temple, without changing the legs',
    'emotion-bonk':f'1 relaxed neutral smile; 2 raise a small soft {piping} toy hammer with a proper hand grip just beside the head; 3 gently tap the top of the head with the SAME toy hammer, eyes squeezed shut in playful surprise; 4 lower the SAME hammer and smile; keep the hammer fully inside each source cell'
   }
   clips={
    'emotion-touch':{'headpat':dict(frames=[0],frameMs=[5000],loop=False),'poke':dict(frames=[1,2,1],frameMs=[500,650,500],loop=False),'tickle':dict(frames=[3],frameMs=[5000],loop=False)},
    'emotion-mood':{'happy':dict(frames=[0],frameMs=[5000],loop=False),'sad':dict(frames=[1],frameMs=[5000],loop=False),'faint':dict(frames=[2],frameMs=[5000],loop=False),'dizzy':dict(frames=[3],frameMs=[5000],loop=False)},
    'emotion-bonk':{'bonk':dict(frames=[0,1,2,3,0],frameMs=[400,650,400,600,450],loop=False)}
   }
   prompt=f'''Precise upper-body pose EDIT of this exact {width}x{height}, 4-column 1-row sprite sheet. Keep the SAME adult {name} woman and the SAME approved short-sleeve white/{color} sports shirt and above-knee {color} shorts. Keep the original face, mature small head size, {identity}, clean 3D rendering, and all clothing details. Preserve the complete hips, long thighs, knees, shins, ankles, sneakers and ground position at EXACT original scale and coordinates in ALL four poses. Do not redesign the person or shorten/thicken legs, enlarge heads, resize individual figures or crop feet. Modify ONLY expressions and arms/hands, left to right: {gestures[kind]}. Preserve the original canvas, original figure positions, margins, camera and order of 4 full figures. True transparent alpha outside figures and held props. No background, gradient, glow, text, frame or extra people.'''
   jobs.append(dict(key=key,id=cid,style='realistic',kind=kind,columns=4,rows=1,source='artwork/sports-identity/'+key+'.png',references=[reference['source'],reference['references'][0]],generationReferences=[reference['source']],prompt=prompt,tool='built-in image_gen',status='generated-awaiting-visual-review',selected=False,comparisonRole='approved-sports-pose-edit',sourceAtlas='realistic-photo' if family=='whale' else 'photo',physicalOrder=True,clips=clips[kind]))
   continue
  if kind=='dizzy':sprite=pet['dizzy']
  elif kind.startswith('five-'):
   key=('realistic-' if family=='whale' else '')+kind.removeprefix('five-') if adult else ('chibi' if kind=='five-main' else 'chibi-interaction') if family=='whale' else 'five'
   sprite=pet['interactionFive']['atlases'][key]
  else:sprite=pet['motions'][lookup.get(kind,kind)]
  original=folder/sprite['file'];relative=original.relative_to(ROOT).as_posix()
  if any(j['references'][0]==relative for j in jobs):continue
  if any(j['id']==cid and j['references'][0]==relative and j.get('selected') for j in records):
   continue
  key=f'{cid}-{kind}-v1'
  if (ROOT/'artwork/sports-identity/results'/f'{key}.json').exists():
   continue
  columns,rows=sprite['columns'],sprite['rows']
  if family=='whale' and not adult and kind=='five-main':columns,rows=3,4
  width,height=Image.open(original).size
  style='photorealistic 3D ADULT woman with long adult legs' if adult else 'Q/chibi original character'
  prompt=f'''Precise clothing-only EDIT of this original {name} {style} action atlas. Keep EXACT source {width}x{height} canvas, all original figure positions, pose order, cell spacing and camera. Never repack or rescale figures to fill margins. Preserve original face and head SIZE, head-body ratio, shoulders, torso, hips, knees, ankles, feet and hand/wrist/finger positions in EVERY pose, including sitting, lying, leaning, bent knees or raised hands. This is clothing on the SAME person, no redesign, no doll anatomy, no larger head, no shorter or thicker legs. Preserve {identity}, all expressions and hair/ear/tail silhouettes and directions.
Replace only old dress/apron, long sleeves, wrist cuffs, tights and boots with a WHITE zip-front school sports shirt with {color} upper-arm SHORT SLEEVES/shoulder panels, {piping} piping and {emblem} chest motif; {color} athletic SHORTS ending well ABOVE knees with narrow white side stripes; white ankle socks and small {shoe} low-top sneakers at the original ankle/sole positions. Keep neckline modest and zipped. Bare forearms, knees and lower legs. NO long sleeves, trousers, gloves or leftover wrist frills. Keep headband and hair accessories even if they are frilly. Keep original hand contacts and ALL existing props, fingers and gesture shapes; do not invent an extra ball, hammer, sparkle or particle.
IMPORTANT: preserve the source's exact head and limb coordinates, original folded seated legs, original small margins at canvas edge, and full figure heights. No longer dangling seated legs and no shortening adult legs to fit a raised arm. Genuine transparent alpha outside characters and held objects. No painted backdrop, gradient, glow, grid, label or extra shadow.'''
  if kind=='care' and not adult:
   prompt+=' Edit ONLY the first TWO rows (8 care poses) to sportswear. The lower FOUR rows show other outfits and must remain unchanged; these lower rows will not be used for sports. Keep cream face towels held in BOTH the opening and closing wipe poses, as well as against the cheek; do not mistake the towel for a sleeve. Keep the hairbrush.'
  elif kind=='care':prompt+=' Preserve every cream face towel, brush and exact hand/cheek contact. Cloth held in the hands is a PROP, not a sleeve.'
  elif kind=='club':prompt+=' Preserve the original physical layout even if uneven. Keep all 8 counting-star gestures, 8 bubble bottle/wand poses, and 8 stretching poses. Preserve mouth-to-ring and hand-to-bottle contacts.'
  elif kind in ('eat','meal','contact','build'):prompt+=' Keep every food, spoon, bowl, cookie, block and actual fingertip-to-object or mouth contact unchanged. Do not replace eating with waving or add a second utensil.'
  elif kind=='bonk':prompt+=' Keep the SAME original pre-painted toy hammer and its existing path above the head. Preserve each original hammer position, angle and head contact; do not invent a hand grip or a second hammer.'
  jobs.append(dict(key=key,id=cid,style='realistic' if adult else 'chibi',kind=kind,columns=columns,rows=rows,source='artwork/sports-identity/'+key+'.png',references=[relative],generationReferences=[relative],prompt=prompt,tool='built-in image_gen',status='generated-awaiting-visual-review',selected=False))
 return jobs

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('character');p.add_argument('kinds',nargs='+');p.add_argument('--output',default='.artifacts/sports-identity-review/next-jobs.json');a=p.parse_args()
 jobs=prepare(a.character,a.kinds);target=ROOT/a.output;target.parent.mkdir(parents=True,exist_ok=True);target.write_text(json.dumps(jobs,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print(json.dumps(dict(jobs=[j['key'] for j in jobs],file=target.as_posix()),ensure_ascii=False))
