"""Write prompts only; all drawing uses the built-in image generation tool."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
folder = root / 'artwork/chibi-continuity'
(folder / 'prompts').mkdir(parents=True, exist_ok=True)
(folder / 'results').mkdir(exist_ok=True)
common = '''Use case: identity-preserve.
Asset: crisp transparent animation sprite atlas for a friendly desktop companion. Reference image 1 is the character identity and EXACT OUTFIT guide. Preserve her chibi proportions, face, hair color, hairstyle, accessories, costume, shoes, line weight and colors. Keep any existing tail and never add a tail to characters without one. The requested motions below replace the reference poses. Neutral, wholesome character acting, same eye-level front three-quarter camera. No text, numbers, borders, checkerboard, backdrop, ground shadows or decorative symbols. REAL alpha transparency. Entire character and all accessories inside every cell with at least 24 transparent pixels between figures. Same face size and bodily proportions across ALL cells, including seated, curled and lying poses. Never zoom in to fill a shorter pose. Only the specified action changes. Do not draw external hands: the application supplies touch feedback. No extra limbs or disconnected fingers. Each pose is a genuinely different drawing, suitable for sequential playback; no costume changes or substitutions.'''
care = '''EXACTLY 24 sprites, four columns by six rows on a high resolution 2048x3072 RGBA canvas. Each row contains four consecutive frames of ONE action. Read left to right, then next row. Character stays seated at the same floor line in this sheet.
Row 1, speaking: 0 attentive with lips closed and hands resting; 1 mouth slightly open and one palm gently raised; 2 a different soft mouth shape with palm turning; 3 close lips and settle. Natural conversation, no speech bubbles.
Row 2, quietly daydreaming: 4 closed mouth and gaze slightly upward; 5 tilt head a little with a hand near chin; 6 slow blink, lips still closed; 7 quiet sideways gaze with relaxed hands. Never talking in this row.
Row 3, response to a head pat: 8 attentive; 9 lower head a little and half close eyes; 10 soft contented blink and tiny head tilt; 11 relaxed smile. No external hand in the artwork.
Row 4, cheeks being rubbed: 12 relaxed face; 13 left cheek softly compressed and head gently leaning right; 14 right cheek softly compressed and head gently leaning left; 15 cheeks return to normal with a small closed-mouth smile. No external hands in the artwork.
Row 5, tickled: 16 shoulders begin to rise; 17 eyes close in laughter, torso leans slightly left; 18 laughs and leans slightly right with bent elbows; 19 eases back into a smile. Feet stay in place, no jumping or random limb shaking.
Row 6, a gentle toy mallet tap: 20 the small BLUE CYLINDER toy mallet with WOODEN HANDLE with a tiny white whale marking hovers just above head; 21 it lightly touches the top of head once, eyes close; 22 it rebounds as she raises her own hands to her head; 23 mallet is gone and she lowers hands, recovered. Exactly one mallet in frames 20-22, no mallets in any other row. Keep bare wrists and the SAME clothing as image 1, even when hands touch her head. Never add maid cuffs or gloves. No sparks or text.'''
body = '''EXACTLY 24 sprites, four columns by six rows on a high resolution 2048x3072 transparent RGBA canvas. Six rows, four frames per row. Keep identical head size, limb proportions, camera and floor line; a lying character must look naturally shorter, NOT enlarged.
Row 1, jump: 0 squat with bent knees preparing; 1 push off with toes, arms lifting; 2 airborne with knees clearly tucked and elbows lifted; 3 land in a gentle bent-knee crouch. The application moves the sprite upward: keep the bottom of each drawn pose at the same cell baseline. No floating dust, rings or motion lines.
Row 2, curl up: 4 sit down with legs forward; 5 draw both knees toward chest; 6 wrap both arms around knees; 7 head lowered onto tightly hugged knees. Full sitting curl, not a half squat.
Row 3, rest: 8 seated and sleepy; 9 support body with one arm and lean sideways; 10 lie down on side with knees loosely folded; 11 fully relaxed side-lying sleep, cheek on joined hands and eyes closed. No pillow, bed or Z text.
Row 4, lifted: 12 slight surprised expression and legs beginning to leave the floor; 13 body gently extends vertically, arms and feet dangling; 14 settles with relaxed hanging limbs; 15 stable suspended pose. No swinging, pendulum, dizzy face, huge body rotation or external hand. Dress or veil hangs naturally downward.
Row 5, waiting for a ball: 16 brings forearms forward with open palms; 17 ready with both palms visible; 18 eyes follow a ball slightly to her left; 19 eyes gently return to center, hands still ready. Do not draw a ball: the application owns the one moving ball.
Row 6: 20 surprised at being hit by a soft ball, shoulders recoil and palms protect chest; 21 recovered surprised face with arms relaxing; 22 the ball missed, looks down toward empty floor on her right with open hands; 23 small rueful smile, returns to relaxed posture. No ball, bat, shield or other object in any cell.'''
food = '''EXACTLY 16 sprites, four columns by four rows on a high resolution 2048x2048 transparent RGBA canvas. Read left to right then next row. The same seated character at the same scale and baseline in all cells. Every food item must be in visible contact with an actual hand or spoon. The face remains fully recognizable.
Frames 0-7 form ONE consecutive bread snack: 0 hold a small bread roll in both hands near lap; 1 lift with bent elbows; 2 bring roll closer to mouth; 3 bread held AT the lips for a small bite; 4 pull the roll slightly away with one new bite visibly missing; 5 chew with closed lips and small rounded cheeks; 6 swallow softly while lowering bread; 7 contented closed-mouth smile, bread still held near lap. One roll only. Never put bread at an elbow or float food toward the face.
Frames 8-15 form ONE consecutive rice meal: 8 left hand holds a small rice bowl near body and right hand holds spoon; 9 dip spoon into bowl; 10 lift spoon with rice, bowl stays level in left hand; 11 spoon tip with rice AT the lips; 12 spoon comes away EMPTY, lips close; 13 chew with closed lips, bowl still supported; 14 lower the empty spoon back toward bowl; 15 swallow and settle with bowl and spoon still in the same hands. One bowl and one spoon, no floating rice, no separate giant food icons, no forks, no extra bowls. Maintain smooth small changes in wrists and elbows across each sequence.'''
original = '''EXACTLY 12 sprites, four columns by three rows on a high resolution 2048x1536 transparent RGBA canvas. The same seated character, identical face scale and baseline. Each row is four consecutive frames.
Row 1, cheek rub: 0 relaxed closed-mouth face; 1 left cheek softly compressed and tiny head tilt to right; 2 right cheek softly compressed and tiny head tilt to left; 3 cheeks restored with gentle smile. Do not draw external hands; the app supplies them.
Row 2, tickled: 4 shoulders begin to rise; 5 eyes close in laughter and torso leans slightly left; 6 laughs and leans slightly right with bent elbows; 7 eases back into a smile. Feet planted, no jumping or wobbling legs; no external hands.
Row 3, waiting for a ball: 8 arms start to lift; 9 both open palms extend forward; 10 gently track an incoming ball to left with eyes and palms; 11 return gaze to center and remain ready. No drawn ball: the app renders one physical ball separately.'''
jobs = []
for manifest in sorted((root/'src/DesktopPet.App/Assets/Characters').glob('*/pet.json')):
    c = json.loads(manifest.read_text(encoding='utf-8'))
    if c.get('category','chibi') != 'chibi': continue
    for outfit in ['original','swim','wedding']:
        reference = manifest.parent / (c['atlas'] if outfit=='original' else c['outfits'][outfit]['idle'])['file']
        for action in (['touch'] if outfit=='original' else ['care','body','food']):
            key = f"{c['id']}-{outfit}-{action}"
            prompt = common+'\n'+{'touch':original,'care':care,'body':body,'food':food}[action]
            prompt += '\nAppearance lock: '+c['name']+'. Preserve the EXACT '+{'original':'original costume','swim':'sporty swim outfit','wedding':'wedding dress and veil'}[outfit]+' from image 1 in every frame; no outfit changes.'
            refs=[str(reference)]
            job=dict(key=key,id=c['id'],outfit=outfit,action=action,references=refs,prompt=prompt,columns=4,rows={'touch':3,'care':6,'body':6,'food':4}[action])
            jobs.append(job)
            (folder/'prompts'/f'{key}.txt').write_text(prompt,encoding='utf-8')
(folder/'jobs.json').write_text(json.dumps(jobs,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(jobs)} chibi continuity prompts prepared')
