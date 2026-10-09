"""Add a fresh Unicode index-cache diagnostic; retain every canonical case.

The golden is calculated independently from fixture code points. No benchmark
execution supplies an expected result. This catalog is not milestone qualified.
"""
import hashlib
import json
import pathlib
import sys

catalog = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8'))
scan = next(c for c in catalog['cases'] if c['id'] == 'strings.scan-supplementary.medium')
fixture = json.loads(scan['fixtureJson'])
text = fixture['text']
assert scan['expectedOutput'] == f"{len(text)} {sum(map(ord, text))}\n"
source = 'TEXT = ' + repr(text) + "\nowned = TEXT + 'a'\ntotal = 0\nfor index in range(len(owned)):\n    total += ord(owned[index])\nprint(len(owned), total)\n"
expected = f"{len(text) + 1} {sum(map(ord, text)) + ord('a')}\n"
row = dict(id='diagnostic.unicode.index-cache.medium', family='diagnostic', category='strings',
           scale='medium', size=scan['size'], source=source, fixtureJson=scan['fixtureJson'], expectedOutput=expected,
           sourceUtf8Bytes=len(source.encode('utf-8')), expectedOutputUtf8Bytes=len(expected.encode('utf-8')),
           fixtureTextUtf8Bytes=len(text.encode('utf-8')), fixtureTextCodePoints=len(text))
for key in ('source', 'fixture', 'expectedOutput'):
    value = row['fixtureJson' if key == 'fixture' else key]
    row[key + 'Sha256'] = hashlib.sha256(value.encode('utf-8')).hexdigest()
assert not any(c['id'] == row['id'] for c in catalog['cases'])
catalog['cases'].append(row)
catalog['performanceQualified'] = False
with pathlib.Path(sys.argv[2]).open('x', encoding='utf-8', newline='\n') as output:
    output.write(json.dumps(catalog, separators=(',', ':')) + '\n')
print('CATALOG_CREATED', row['id'], row['expectedOutput'].strip())
