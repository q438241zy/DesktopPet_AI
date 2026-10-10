"""Losslessly compress exported binary ownership metadata; no image files touched."""
import base64
import json
from pathlib import Path
import numpy as np

def compact(data):
    cache={}
    for family in data.values():
        for style in family.values():
            for outfit in style.values():
                for desc in outfit.values():
                    for c in desc['cells']:
                        if not c or not isinstance(c.get('ownership'),str):continue
                        raw=c['ownership'];n=c['width']*c['height'];key=(raw,n)
                        if key not in cache:
                            bits=np.unpackbits(np.frombuffer(base64.b64decode(raw),dtype=np.uint8))[:n]
                            switches=np.flatnonzero(bits[1:]!=bits[:-1])+1
                            counts=np.diff(np.concatenate(([0],switches,[n])))
                            if bits[0]:counts=np.concatenate(([0],counts))
                            body=bytearray()
                            for count in counts:
                                value=int(count)
                                while value>=128:body.append((value&127)|128);value>>=7
                                body.append(value)
                            # Every encoded mask must reconstruct exactly, including padding.
                            decoded=np.repeat(np.arange(len(counts))%2,counts).astype(np.uint8)
                            assert np.array_equal(decoded,bits)
                            cache[key]={'pixels':n,'runs':base64.b64encode(body).decode('ascii')}
                        c['ownership']=cache[key]
    return len(cache)

if __name__=='__main__':
    file=Path(__file__).resolve().parents[1]/'docs/demo/companion-v01/play-art.js'
    prefix='globalThis.CLOUD_PLAY_ART='
    data=json.loads(file.read_text(encoding='utf-8')[len(prefix):].rstrip(';\n'));before=file.stat().st_size
    count=compact(data)
    file.write_text(prefix+json.dumps(data,ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
    print(json.dumps({'masksVerified':count,'before':before,'after':file.stat().st_size}))
