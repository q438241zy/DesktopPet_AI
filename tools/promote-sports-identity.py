"""Pin staged artwork before verification, then promote that exact set to source.

The daily runtime and user data are never written by this tool. Visual review
remains a separate step; --apply requires its explicit evidence file.
"""
import argparse
import copy
import hashlib
import json
import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.artifacts/sports-identity-stage'
SOURCE = ROOT / 'src/DesktopPet.App/Assets/Characters'
FAMILIES = ['whale', 'gpt', 'claude', 'gemini', 'grok', 'qwen', 'zhipu', 'kimi']
IDS = FAMILIES + ['deepseek-adult' if x == 'whale' else x + '-adult' for x in FAMILIES]

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def inventory():
    files = {}
    records = []
    for cid in IDS:
        folder = STAGE / 'Assets/Characters' / cid
        report = read(folder / 'sports-identity-stage.json')
        assert report['readyForNativeVerification'] and not report['missing'] and not report['missingFive'], cid
        before, after = read(SOURCE / cid / 'pet.json'), read(folder / 'pet.json')
        before_without, after_without = copy.deepcopy(before), copy.deepcopy(after)
        before_without['outfits'].pop('sports')
        after_without['outfits'].pop('sports')
        assert before_without == after_without, f'{cid}: unrelated manifest changed'
        outfit = after['outfits']['sports']
        sprites = [outfit['idle'], *outfit['motions'].values()]
        relative_files = {'pet.json'}
        for sprite in sprites:
            name = sprite['file']
            assert name.startswith('outfits/sports/identity-'), name
            relative_files.update([name, str(Path(name).with_suffix('.json')).replace('\\', '/')])
        for sprite in outfit['interactionFive']['atlases'].values():
            name = sprite['file']
            assert name.startswith('interactions/sports/identity-'), name
            relative_files.add(name)
        for item in report['artwork'] + report['interactionFive']:
            job = read(ROOT / 'artwork/sports-identity/results' / (item['key'] + '.json'))
            review = job['visualReview']
            assert job['selected'] and review['passed'], item['key']
            assert sha(ROOT / job['source']) == review['candidateSha256'] == item['sha256'] == sha(folder / item['file']), item['key']
            assert sha(ROOT / job['references'][0]) == review['originalSha256'], item['key']
            records.append(item['key'])
        for name in sorted(relative_files):
            path = (folder / name).resolve()
            assert path.is_relative_to(folder.resolve()) and path.is_file(), path
            files[f'{cid}/{name}'] = sha(path)
    assert len(records) == len(set(records)), 'Duplicate selected source record'
    return dict(characters=IDS, records=sorted(records), files=files, executableSha256=sha(STAGE / 'DesktopPet.exe'))

parser = argparse.ArgumentParser()
parser.add_argument('--checks', default='.artifacts/sports-final-20261009')
parser.add_argument('--apply', action='store_true')
args = parser.parse_args()
checks = (ROOT / args.checks).resolve()
assert checks.is_relative_to((ROOT / '.artifacts').resolve()), 'Checks must be isolated'
current = inventory()
pin = checks / 'stage-pins.json'
if not args.apply:
    if pin.exists():
        assert read(pin) == current, 'Pinned stage changed; use a new verification directory'
    else:
        write(pin, current)
    print(f"Pinned {len(IDS)} appearances, {len(current['records'])} reviewed sources and {len(current['files'])} staged files.")
else:
    assert read(pin) == current, 'Staged files changed after verification'
    visual = read(checks / 'visual-review.json')
    assert visual['reviewer'] == 'assistant' and visual['passed'] and set(visual['characters']) == set(IDS), 'Full visual review required'
    reports = {}
    for name in ['sports-check.txt', 'scale-check.txt', 'polish-check.txt', 'walk-check.txt', 'floor-walk-check.txt', 'five-check.txt', 'walk-interrupt-check.txt']:
        path = checks / name
        lines = path.read_text(encoding='utf-8-sig').splitlines()
        assert lines and all(line.startswith('PASS ') for line in lines[:-1]), name
        assert 'check' in lines[-1].lower() or 'pass' in lines[-1].lower(), name
        reports[name] = dict(sha256=sha(path), summary=lines[-1])
    assert '16 sports appearances (full roster)' in reports['sports-check.txt']['summary']
    assert '64 appearances' in reports['five-check.txt']['summary']
    asset_check = checks / 'asset-check.txt'
    assert asset_check.read_text(encoding='utf-8-sig').startswith('PASS: 16 characters,'), 'Asset decoding verification required'
    reports['asset-check.txt'] = dict(sha256=sha(asset_check), summary=asset_check.read_text(encoding='utf-8-sig').strip())
    backup = ROOT / '.artifacts' / ('source-before-sports-identity-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
    backup.mkdir()
    for name in current['files']:
        target = SOURCE / name
        if target.exists():
            saved = backup / name
            saved.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(target, saved)
    # All preflight checks and backup finish before the first source write.
    for name, digest in current['files'].items():
        target = SOURCE / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(STAGE / 'Assets/Characters' / name, target)
        assert sha(target) == digest
    receipt = dict(date=datetime.now().isoformat(), sourceOnly=True, backup=str(backup.relative_to(ROOT)), checks=str(checks.relative_to(ROOT)), reports=reports, visualReview=visual, **current)
    write(ROOT / 'artwork/sports-identity/installation.json', receipt)
    print(f"Promoted {len(IDS)} sports appearances to source; backup: {backup}. Daily runtime unchanged.")
