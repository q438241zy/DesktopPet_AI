"""Record explicit human/model visual decisions; never infer approval from geometry."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
folder = ROOT / 'artwork/sports-identity'
review = json.loads((folder / 'review-decisions.json').read_text(encoding='utf-8'))
pins_path = ROOT / '.artifacts/sports-identity-review/comparison-evidence.json'
pins = json.loads(pins_path.read_text(encoding='utf-8')) if pins_path.is_file() else {}
for decision in review['decisions']:
    path = folder / 'results' / (decision['key'] + '.json')
    job = json.loads(path.read_text(encoding='utf-8-sig'))
    evidence = '.artifacts/sports-identity-review/' + decision['key'] + '-pairs.png'
    if not (ROOT / evidence).is_file():
        raise ValueError('Missing comparison evidence: ' + evidence)
    candidate_hash=hashlib.sha256((ROOT/job['source']).read_bytes()).hexdigest()
    original_hash=hashlib.sha256((ROOT/job['references'][0]).read_bytes()).hexdigest()
    previous=job.get('visualReview') or {}
    if previous and (previous.get('candidateSha256')!=candidate_hash or previous.get('originalSha256')!=original_hash):
        raise ValueError('Source changed after review; create a new version and review it: '+job['key'])
    pin=pins.get(job['key'])
    if not pin or pin.get('candidateSha256')!=candidate_hash or pin.get('originalSha256')!=original_hash:
        raise ValueError('Regenerate comparison evidence for current sources: '+job['key'])
    job['selected'] = decision['passed']
    job['status'] = 'visually-reviewed-pending-runtime' if decision['passed'] else 'not-selected-after-review'
    job['visualReview'] = dict(reviewer='assistant', date=review['date'], passed=decision['passed'],
        summary=decision['summary'], evidence=[evidence], scope=review['scope'], runtimeVerified=False,
        candidateSha256=candidate_hash,originalSha256=original_hash,
        comparisonFingerprint=pin['fingerprint'])
    path.write_text(json.dumps(job,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f"Saved {len(review['decisions'])} explicit review decisions; native assets unchanged.")
