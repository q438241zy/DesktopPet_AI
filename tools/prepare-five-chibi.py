"""Prepare outfit-specific Q jobs from reviewed native identities, never edit pixels."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1];art=root/'artwork/interaction-five'
template=json.loads((art/'gpt-prompts.json').read_text())[0]['prompt']
poses=template[template.index(' Row-major poses:'):]
identities={
 'whale':'DeepSeek, blue hair, vivid blue eyes, whale hair clips and blue bow',
 'claude':'Claude, long copper orange wavy hair, amber eyes, fine round glasses and orange flower with dark ribbon',
 'gemini':'Gemini, violet-blue hair with pink/cyan tips, pink and gold heterochromia, cat ears and tail, star hair clip',
 'grok':'Grok, golden twin-tails, blue eyes, black/gold ribbons and silver orbit clip',
 'gpt':'GPT, silver lavender wavy hair, lilac eyes, cream horns, pointed ears and knot ornament',
 'kimi':'Kimi, silvery lavender hair with pale blue ends, violet eyes, navy ribbon and moon ornament',
 'qwen':'Qwen, periwinkle blue hair with two front braids, violet eyes, small navy/gold hat and Chinese knot ornaments',
 'zhipu':'GLM, charcoal hair with blue ends, blue eyes, black cat ears and tail, navy tassels and Z ornaments'}
jobs=[]
for char,identity in identities.items():
 for outfit in ['original','swim','wedding','sports']:
  if char=='whale' and outfit=='original' or char=='gpt' and outfit in ['original','sports']:continue
  folder=root/'src/DesktopPet.App/Assets/Characters'/char
  ref=folder/({'original':'atlas.png','swim':'outfits/swim.webp','wedding':'outfits/wedding.webp','sports':'outfits/sports/core.png'}[outfit])
  clothes='The exact '+outfit+' outfit and shoes in reference 1 must stay identical in every cell.'
  if outfit=='sports':clothes+=' Short sleeves above elbows, athletic shorts ending ABOVE knees, sneakers; preserve reference standing head/body ratio and natural visible thighs/shins, never draw trousers or crop/shrink legs.'
  prompt=f'Create a transparent production interaction sprite atlas of the exact existing Q-version mascot {identity}. Reference 1 is the identity and SINGLE outfit reference, not the sheet layout. '+clothes+' Preserve the existing delicate anime illustration, face, hairstyle and original cute standing head/body proportions. For seated source poses, show the SAME character standing with natural connected knees/ankles. No doll-stump legs or adult body. Across all cells copy identical planted feet, head scale, waist, knees and ankles; ONLY arms and face change for these friendly everyday gestures. Exactly FOUR COLUMNS and FIVE ROWS, 20 complete separate full-body figures in row-major order. High-resolution portrait canvas approximately 2048x2560. Each sprite occupies at most 64% of its cell width and 82% height, centered; wide absolutely empty transparent gutters separate ALL hair, ears, tails and bridal veils. Bottom row retains identical scale and entire shoes. No scene, background, text, shadow, labels, grid or floating decoration. True alpha transparency. All hands remain connected and physically hold the indicated object.'+poses
  jobs.append(dict(key='QFive-'+char+'-'+outfit,character=char,outfit=outfit,filename=char+'-'+outfit+'-v2.png',references=[ref.as_posix()],rows=5,poses=['neutral','high-prep','high-ready','high-contact','high-recoil','fist-up','fist-mid','fist-down','reveal-rock','reveal-scissors','reveal-paper','receive','gift','gift-empty','read','page','read-finish','photo','lift','happy'],prompt=prompt))
(art/'chibi-expansion-prompts.json').write_text(json.dumps(jobs,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Saved '+str(len(jobs))+' Q appearance jobs')
