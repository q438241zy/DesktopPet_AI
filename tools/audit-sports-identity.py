"""Inventory original-pose clothing edits without modifying any image or manifest.

The geometry checks are review aids, not an assertion of visual acceptance.
Only explicit visualReview entries may promote a generated candidate.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image
import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/DesktopPet.App/Assets/Characters'
ART = ROOT / 'artwork/sports-identity'
FAMILIES = ['whale', 'gpt', 'claude', 'gemini', 'grok', 'qwen', 'zhipu', 'kimi']


def rel(path):
    return path.relative_to(ROOT).as_posix()


def canvas_resolution(original_size, candidate_size):
    # The image service can round a requested canvas edge by one pixel. Keep
    # ONE uniform pixel scale, never stretch x/y independently to hide that.
    ratio = candidate_size[0] / original_size[0]
    return ratio if abs(candidate_size[1] - original_size[1] * ratio) <= 2 else None


def geometry(path, columns, rows):
    with Image.open(path) as image:
        alpha = image.convert('RGBA').getchannel('A')
        threshold = alpha.point(lambda a: 255 if a >= 80 else 0)
        cells = []
        for i in range(columns * rows):
            x, y = i % columns * image.width // columns, i // columns * image.height // rows
            right, bottom = (i % columns + 1) * image.width // columns, (i // columns + 1) * image.height // rows
            box = threshold.crop((x, y, right, bottom)).getbbox()
            cells.append(list(box) if box else None)
        return dict(size=list(image.size), alphaZeroFraction=round(alpha.histogram()[0] / (image.width * image.height), 4), cells=cells)


def comparison_windows(original, candidate, columns, rows, source_cells=None):
    """Shared source-coordinate windows, never independent figure normalization.

    Original atlases may intentionally cross a nominal row boundary. Component
    bounds recover complete figures, including shoes; both versions then use
    the union window and one common scale for the entire sheet.
    """
    count = len(source_cells) if source_cells else columns * rows
    ordered = []
    source_size = Image.open(original).size
    candidate_size=Image.open(candidate).size
    resolution=canvas_resolution(source_size,candidate_size)
    if resolution is None:
        return None
    for path in [original, candidate]:
        rgba = np.asarray(Image.open(path).convert('RGBA'))
        height, width = rgba.shape[:2]
        for threshold in [80,120,180,220,240,248,252,253,254,255]:
            _, _, stats, _ = cv2.connectedComponentsWithStats((rgba[:, :, 3] >= threshold).astype('uint8'), 8)
            parts = sorted([s for s in stats[1:] if s[4] > width * height * .0003], key=lambda s: int(s[4]), reverse=True)
            if len(parts)>=count and parts[count-1][4]>width*height*.0015:
                break
        else:
            return None
        coord_scale=resolution if path==candidate else 1
        # Higher output resolution is allowed only as a single uniform canvas
        # factor. Never correct anatomy or each figure's scale independently.
        parts=[np.array([s[0]/coord_scale,s[1]/coord_scale,s[2]/coord_scale,s[3]/coord_scale,s[4]/coord_scale**2]) for s in parts]
        width,height=source_size
        main = parts[:count]
        boxes = []
        if source_cells:
            # A manifest may describe 8x3 physical positions as 4x6 logical cells.
            # Match each measured silhouette to its actual source coordinates.
            choices=[]
            for i,cell in enumerate(source_cells):
                cx,cy=cell['x']+cell['width']/2,cell['y']+cell['height']/2
                for j,s in enumerate(main):
                    choices.append((((s[0]+s[2]/2-cx)/cell['width'])**2+((s[1]+s[3]/2-cy)/cell['height'])**2,i,j))
            boxes=[None]*count
            used=set()
            for _,i,j in sorted(choices):
                if boxes[i] is None and j not in used:
                    boxes[i]=main[j]
                    used.add(j)
        else:
            main = sorted(main, key=lambda s: int(s[1] + s[3]))
            for row in range(rows):
                boxes.extend(sorted(main[row * columns:(row + 1) * columns], key=lambda s: int(s[0] + s[2] / 2)))
        boxes = [[int(x), int(y), int(x + w), int(y + h)] for x, y, w, h, _ in boxes]
        for x, y, w, h, _ in parts[count:]:
            cx, cy = x + w / 2, y + h / 2
            i = min(range(count), key=lambda i: ((cx-(boxes[i][0]+boxes[i][2])/2)/(width/columns))**2+((cy-(boxes[i][1]+boxes[i][3])/2)/(height/rows))**2)
            a = boxes[i]
            if abs(cx-(a[0]+a[2])/2) < width/columns*.65 and a[1]-h < cy < a[3]+h:
                boxes[i] = [min(a[0],int(x)),min(a[1],int(y)),max(a[2],int(x+w)),max(a[3],int(y+h))]
        ordered.append(boxes)
    windows=[]
    for a,b in zip(*ordered):
        left,top=max(0,min(a[0],b[0])-4),max(0,min(a[1],b[1])-4)
        right,bottom=min(width,max(a[2],b[2])+4),min(height,max(a[3],b[3])+4)
        windows.append(dict(x=left,y=top,w=right-left,h=bottom-top))
    return windows


def audit():
    drafts = []
    for record in sorted((ART / 'results').glob('*.json')):
        job = json.loads(record.read_text(encoding='utf-8-sig'))
        source = ROOT / job['source']
        references = job.get('references', [])
        if not references or not source.is_file():
            continue
        original = ROOT / references[0]
        manifest = json.loads((ASSETS / job['id'] / 'pet.json').read_text(encoding='utf-8-sig'))
        sprites = [manifest['atlas'], *manifest.get('motions', {}).values(), *manifest.get('interactionFive', {}).get('atlases', {}).values()]
        if manifest.get('dizzy'):
            sprites.append(manifest['dizzy'])
        original_sprite = next((s for s in sprites if ASSETS / job['id'] / s['file'] == original), {})
        columns, rows = job.get('columns', original_sprite.get('columns', 1)), job.get('rows', original_sprite.get('rows', 1))
        count=columns*rows
        source_cells=original_sprite.get('cells')
        if source_cells and len(source_cells)!=count:
            source_cells=None
        clips=[]
        for action,sprite in manifest.get('motions',{}).items():
            if ASSETS/job['id']/sprite['file'] != original:
                continue
            frames=sprite.get('frames') or list(range(count))
            if max(frames)>=count:
                raise ValueError(f'{job["key"]}: invalid source sequence {action}')
            clips.append(dict(key=action,frames=frames,frameMs=sprite.get('frameMs') or [500]*len(frames),loop=sprite.get('loop',True)))
        if job.get('clips'):
            clips=[dict(key=key,**value) for key,value in job['clips'].items()]
        if not clips:
            clips=[dict(key='poses',frames=list(range(count)),frameMs=[800]*count,loop=False)]
        candidate_sha=hashlib.sha256(source.read_bytes()).hexdigest()
        original_sha=hashlib.sha256(original.read_bytes()).hexdigest()
        review=job.get('visualReview')
        review_current=bool(review and review.get('candidateSha256')==candidate_sha and review.get('originalSha256')==original_sha)
        candidate_geometry, original_geometry = geometry(source, columns, rows), geometry(original, columns, rows)
        ca,oa=candidate_geometry['size'],original_geometry['size']
        resolution=canvas_resolution(oa,ca)
        diffs = []
        if candidate_geometry['size'] == original_geometry['size']:
            for i, (a, b) in enumerate(zip(original_geometry['cells'], candidate_geometry['cells'])):
                if a and b:
                    diffs.append(dict(frame=i, heightRatio=round((b[3]-b[1])/(a[3]-a[1]),4), widthRatio=round((b[2]-b[0])/(a[2]-a[0]),4), footDelta=b[3]-a[3], topDelta=b[1]-a[1]))
        drafts.append(dict(key=job['key'], character=job['id'], kind=job['kind'], columns=columns, rows=rows,
                           candidate=rel(source), original=rel(original), status=job.get('status'), selected=bool(job.get('selected') and review_current and review.get('passed')),
                           visualReview=review if review_current else None, reviewStale=bool(review and not review_current), candidateSha256=candidate_sha, originalSha256=original_sha,
                           clips=clips, sourceCells=source_cells, candidateResolution=resolution, transparent=candidate_geometry['alphaZeroFraction']>=.2,
                           comparisonRole=job.get('comparisonRole','original-clothing-edit'),
                           comparisonWindows=comparison_windows(original,source,columns,rows,source_cells),
                           originalGeometry=original_geometry, candidateGeometry=candidate_geometry, differences=diffs))
    coverage=[]
    for family in FAMILIES:
        for style, cid in [('chibi',family),('realistic','deepseek-adult' if family=='whale' else family+'-adult')]:
            folder=ASSETS/cid
            pet=json.loads((folder/'pet.json').read_text(encoding='utf-8-sig'))
            files={}
            for name, sprite in [('base',pet['atlas']),*pet['motions'].items(),*([('dizzy',pet['dizzy'])] if pet.get('dizzy') else [])]:
                files.setdefault(sprite['file'],dict(columns=sprite['columns'],rows=sprite['rows'],actions=[]))['actions'].append(name)
            for name, sprite in pet.get('interactionFive',{}).get('atlases',{}).items():
                files.setdefault(sprite['file'],dict(columns=sprite['columns'],rows=sprite['rows'],actions=[]))['actions'].append('interactionFive:'+name)
            for file, info in files.items():
                matches=[d for d in drafts if d['character']==cid and d['original']==rel(folder/file)]
                coverage.append(dict(character=cid,style=style,original=rel(folder/file),**info,
                    candidates=[d['key'] for d in matches], reviewed=[d['key'] for d in matches if d['selected'] and d['visualReview']],
                    installed=False))
    report=dict(schema=1, note='Geometry is diagnostic only; visual review and native installation are separate.',drafts=drafts,coverage=coverage)
    return report


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',default='.artifacts/sports-identity-review/audit.json')
    args=parser.parse_args()
    output=ROOT/args.output
    output.parent.mkdir(parents=True,exist_ok=True)
    report=audit()
    output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    output.with_suffix('.js').write_text('window.sportsAudit='+json.dumps(report,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
    print(json.dumps(dict(report=rel(output),drafts=len(report['drafts']),originalSheets=len(report['coverage']),visuallyReviewed=sum(bool(d['selected'] and d['visualReview']) for d in report['drafts'])),ensure_ascii=False))
