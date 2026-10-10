"""Delete the duplicated PC Ledger rows left over by verification.

The 26-row import ended up applied twice (53 rows: 1..26 imported, 27..53 duplicated plus test rows).
The imported set is numbered 1..26, so anything above that is a duplicate and is removed here.
"""
import json
import urllib.request

API = 'http://localhost:5099'
cfg = json.load(open('api/appsettings.json', encoding='utf-8-sig'))


def call(method, path, body=None, token=None):
    r = urllib.request.Request(API + path, data=json.dumps(body).encode() if body is not None else None, method=method)
    r.add_header('Content-Type', 'application/json')
    if token:
        r.add_header('Authorization', 'Bearer ' + token)
    try:
        with urllib.request.urlopen(r, timeout=90) as x:
            raw = x.read().decode()
            return x.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:150]


_, tp = call('POST', '/api/auth/login', {'username': cfg['Seed']['AdminUsername'], 'password': cfg['Seed']['AdminPassword']})
token = tp['token']
_, page = call('GET', '/api/records/10?page=1&pageSize=500', None, token)
items = page.get('items') or []


def num_of(rec):
    try:
        return int(str((rec.get('values') or {}).get('nomor', '0')).strip())
    except Exception:
        return -1


extra = [r for r in items if num_of(r) > 26]
print('total sebelum:', len(items), '| nomor > 26:', len(extra))
removed = 0
for rec in extra:
    status, _ = call('DELETE', '/api/records/10/%d' % rec['id'], None, token)
    if status in (200, 204):
        removed += 1
print('dihapus:', removed)
_, after = call('GET', '/api/records/10?page=1&pageSize=500', None, token)
nums = sorted(num_of(r) for r in (after.get('items') or []))
print('total sekarang:', after.get('total'), '| nomor:', nums[:3], '...', nums[-3:])
