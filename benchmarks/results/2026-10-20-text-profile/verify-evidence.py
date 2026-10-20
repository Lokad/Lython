import base64
import collections
import gzip
import hashlib
import json
import pathlib
import sys

root = pathlib.Path.cwd()
directory = root / 'benchmarks/results/2026-10-20-text-profile'
manifest = json.loads((directory / 'manifest.json').read_text(encoding='utf-8'))
for name, facts in manifest['files'].items():
    raw = (directory / name).read_bytes()
    assert len(raw) == facts['bytes'] and hashlib.sha256(raw).hexdigest() == facts['sha256'], name
receipt = json.loads((directory / 'profile-receipt.json').read_text(encoding='utf-8'))
for name in ('text.nettrace', 'text.speedscope.json'):
    raw = gzip.decompress((directory / (name + '.gz')).read_bytes())
    assert hashlib.sha256(raw).hexdigest() == receipt['files'][name]['sha256'], name
catalog = gzip.decompress((directory / 'catalog.json.gz').read_bytes())
assert hashlib.sha256(catalog).hexdigest() == receipt['catalogSha256']
case = json.loads((directory / 'case.json').read_text(encoding='utf-8'))
assert hashlib.sha256(case['source'].encode('utf-8')).hexdigest() == case['sourceSha256']
source = (root / 'benchmarks/scripts/text-summary-helpers.py').read_text(encoding='utf-8-sig').replace('\r\n', '\n')
assert source == case['source']
fixture = json.loads(case['fixtureJson'])
text = base64.b64decode(fixture['inputFiles']['input.txt']).decode('utf-8')
counts = collections.Counter(word for line in text.splitlines() for word in line.lower().split())
report = ''.join(f'{word}={counts[word]}\n' for word in sorted(counts)).encode('utf-8')
assert report == base64.b64decode(fixture['outputFiles']['report.txt'])
assert case['expectedOutput'] == str(sum(counts.values())) + '\n'
verification = {'reference': sys.version, 'sourceSha256': case['sourceSha256'],
    'inputBytes': len(text.encode('utf-8')), 'lines': len(text.splitlines()), 'words': sum(counts.values()),
    'reportBytes': len(report), 'reportSha256': hashlib.sha256(report).hexdigest(),
    'status': 'Complete artifact checks, digests and independent Python aggregation pass'}
(directory / 'verification.json').write_text(json.dumps(verification, indent=2) + '\n', encoding='utf-8')
print(verification['status'])
