"""Vendor the reference shortlist from Fontsource; no runtime CDN requests.
Run with Python. Existing manifest versions are reused for reproducible downloads.
Only Latin/Latin-ext WOFF2 faces at UI weights 400..800 are retained.
"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib, io, json, re, tarfile, urllib.request
ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / 'app/assets/theme/reference.json'
OUT = ROOT / 'public/fonts/studio'

def fetch(url):
    with urllib.request.urlopen(url, timeout=90) as response:
        return response.read()

def unpack(archive, member):
    stream = archive.extractfile(member)
    assert stream is not None, member
    return stream.read()

def main():
    catalog = json.loads(CATALOG.read_text(encoding='utf-8'))
    OUT.mkdir(parents=True, exist_ok=True)
    old = json.loads((OUT/'manifest.json').read_text()) if (OUT/'manifest.json').exists() else {'fonts': []}
    versions = {x['id']: x['version'] for x in old['fonts']}
    def vendor(font):
        slug = font['id']
        metadata = json.loads(fetch(f'https://registry.npmjs.org/@fontsource/{slug}/{versions.get(slug, "latest")}'))
        raw = fetch(metadata['dist']['tarball'])
        archive = tarfile.open(fileobj=io.BytesIO(raw), mode='r:gz')
        faces, css = [], []
        for member in archive.getmembers():
            name = Path(member.name).name
            match = re.fullmatch(re.escape(slug)+r'-(latin(?:-ext)?)-(400|500|600|700|800)-(normal|italic)\.woff2', name)
            if not match:
                continue
            data = unpack(archive, member)
            assert data[:4] == b'wOF2', name
            (OUT/name).write_bytes(data)
            subset, weight, style = match.groups()
            # Fontsource's per-subset CSS contains the exact unicode ranges.
            source_css = unpack(archive, f"package/{weight}{'-italic' if style == 'italic' else ''}.css").decode()
            block = next(block for block in source_css.split('}') if name in block)
            ranges = re.findall(r'unicode-range:\s*([^;]+);', block)
            assert ranges, name
            css.append(f"@font-face{{font-family:'Studio {font['name']}';font-style:{style};font-weight:{weight};font-display:swap;src:url('./{name}') format('woff2');unicode-range:{ranges[0]};}}")
            faces.append({'file':name,'weight':int(weight),'style':style,'subset':subset,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
        assert any(x['weight']==400 and x['style']=='normal' and x['subset']=='latin' for x in faces), slug
        license_member = next(m for m in archive.getmembers() if Path(m.name).name.upper() in ('LICENSE','LICENSE.TXT','OFL.TXT'))
        (OUT/'licenses').mkdir(exist_ok=True)
        (OUT/'licenses'/f'{slug}.txt').write_bytes(unpack(archive, license_member))
        return {**font,'version':metadata['version'],'package':metadata['name'],'license':metadata.get('license'),'source':metadata['dist']['tarball'],'faces':faces}, css
    results = list(ThreadPoolExecutor(max_workers=6).map(vendor,catalog['fonts']))
    manifest = {'fonts':[r[0] for r in results]}
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    (OUT/'fonts.css').write_text('\n'.join(line for _,css in results for line in css)+'\n',encoding='utf-8')
    print(json.dumps({'families':len(results),'faces':sum(len(r[0]['faces']) for r in results),'bytes':sum(f['bytes'] for r,_ in results for f in r['faces'])}))
if __name__ == '__main__':
    main()
