"""Prepare reproducible prompts; generation is performed only by the built-in image tool."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
folder = root / 'artwork/motion-polish'
jobs = []
walk = (folder / 'prompts/whale-original-walk.txt').read_text(encoding='utf-8')
walk = walk.replace('the SAME DeepSeek chibi whale maid as reference', 'the SAME character in the EXACT outfit and rendering style of the reference')
walk = walk.replace('and chibi illustration style', 'and reference rendering style')
walk = walk.replace('Whale tail trails behind to LEFT in all frames.', 'If this character has a tail, it trails behind to LEFT in all frames.')
walk = walk.replace('Preserve a recognizable tiny blue whale tail BEHIND the hips;', 'Preserve any existing tail BEHIND the hips; do not add a tail if absent;')
walk = walk.replace('same head height, scale and baseline.', 'same head height, scale and baseline, 14px transparent margin inside every cell.')
build = '''Use case: identity-preserve. Make a production animation atlas of the SAME CHIBI character and EXACT clothing in the reference. Preserve face, hairstyle, hair color, accessories, tail if present, proportions, footwear, costume and crisp outlined art style. Reference is identity and outfit guidance, not the requested action.
EXACTLY 12 separate full-body sprites arranged in a regular 3 columns by 4 rows grid on a portrait 1024x1536 transparent RGBA canvas. No background, no checkerboard, no labels, no grid lines. Leave 14px empty alpha margin around every sprite. All figures have identical scale, head registration, floor baseline and front three-quarter camera angle. Keep the small character seated throughout.
Chronological building-block action with actual contact, read left to right then next row:
0 seated looking down at three pastel wooden cubes on the floor;1 reach toward pink cube;2 fingers grasp pink cube;3 lift pink cube just off floor;4 carry it toward front;5 lower pink cube onto the tower base;6 release and reach to blue cube;7 grasp and lift blue cube;8 carry blue cube above pink cube;9 lower and release blue cube onto pink;10 place yellow cube on top with fingertips;11 let go and smile at completed three-cube tower.
Only the cube carried in the character's hand moves. Previously placed cubes stay on the floor in the same location; three cubes in total at all times. No floating cubes or disconnected hands, no mid-sequence costume changes, no exaggerated body bouncing. Hands must visibly reach, grasp, carry, and release. Keep sharp edges, clear expressive face, consistent lighting, actual alpha transparency. No speech or symbols.'''
for path in sorted((root / 'src/DesktopPet.App/Assets/Characters').glob('*/pet.json')):
    c = json.loads(path.read_text(encoding='utf-8-sig'))
    category = c.get('category','chibi')
    if category not in ('chibi','3d','adult'): continue
    for outfit in ['original','swim','wedding']:
        motions = c['motions'] if outfit=='original' else c['outfits'][outfit]['motions']
        reference = str(path.parent / motions['walk']['file'])
        lock = ('Chibi anime proportions, same outlined illustration style.' if category=='chibi' else
                'Adult-proportioned 3D animated woman, never chibi; keep the same adult height and 3D materials.' if category=='3d' else
                'Realistic adult woman, lifelike anatomy and realistic materials; never chibi or cartoon.')
        lock += ' Keep precisely the same '+{'original':'original costume','swim':'modest sporty swimwear','wedding':'wedding dress and veil'}[outfit]+' and footwear as reference in every frame. No added or removed accessories.'
        for action in (['walk','build'] if category=='chibi' else ['walk']):
            key=c['id']+'-'+outfit+'-'+action
            prompt=(walk if action=='walk' else build)+'\nAppearance lock: '+c['name']+'. '+lock
            job=dict(key=key,id=c['id'],category=category,outfit=outfit,action=action,reference=reference,prompt=prompt)
            jobs.append(job)
            file=folder/'prompts'/f'{key}.txt'
            if not file.exists(): file.write_text(prompt,encoding='utf-8')
(folder/'jobs.json').write_text(json.dumps(jobs,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(jobs),'prompts prepared')
