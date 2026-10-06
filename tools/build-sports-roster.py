"""Index the actual installed sports idle frames for the cloud wardrobe gallery.

Only JSON is written. The browser displays original sprite regions; no bitmap
derivatives or duplicate art packs are created.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/DesktopPet.App/Assets/Characters'
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / 'docs/demo/cloud-club/sports-roster.js'
families = [('whale', 'DeepSeek'), ('gpt', 'GPT'), ('claude', 'Claude'), ('gemini', 'Gemini'),
            ('grok', 'Grok'), ('qwen', 'Qwen'), ('zhipu', 'GLM'), ('kimi', 'Kimi')]
result = []
for family, name in families:
    looks = {}
    for style in ['chibi', 'realistic']:
        cid = family if style == 'chibi' else ('deepseek' if family == 'whale' else family) + '-adult'
        pet = json.loads((ASSETS / cid / 'pet.json').read_text(encoding='utf-8-sig'))
        outfit = pet.get('outfits', {}).get('sports')
        if not outfit or not outfit.get('idle'):
            raise ValueError(f'Missing reviewed sports outfit: {cid}')
        sprite = outfit['idle']
        assert sprite['file'].startswith('outfits/sports/'), (cid, 'wrong outfit file')
        source = ASSETS / cid / sprite['file']
        assert source.is_file(), source
        frame = sprite.get('frames', [0])[0]
        looks[style] = {'id': cid, 'file': f'../../Assets/Characters/{cid}/{sprite["file"]}',
                        'columns': sprite['columns'], 'rows': sprite['rows'], 'frame': frame,
                        'cell': sprite.get('cells', [])[frame] if sprite.get('cells') else None}
    result.append({'family': family, 'name': name, 'looks': looks})
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text('globalThis.SPORTS_ROSTER=' + json.dumps(result, ensure_ascii=False, separators=(',', ':')) + ';\n', encoding='utf-8')
print(f'Sports wardrobe: {len(result)} families, {len(result) * 2} reviewed appearances; no image copies.')
