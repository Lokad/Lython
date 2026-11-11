from pathlib import Path
import base64
import copy
import csv
import hashlib
import io
import json
import shutil
import subprocess
import tempfile

repo = Path('D:/code/Lython')
root = Path(__file__).parent
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
old = root.parent / 'lexical-operands-r139/catalog.json'
catalog = json.loads(old.read_text(encoding='utf-8'))
cases = []
for case in catalog['cases']:
    if case['id'] not in {'scripts.csv-validated-records.large', 'scripts.text-summary-helpers.large', 'scripts.report-render.large'}:
        continue
    item = copy.deepcopy(case)
    source = case['source']
    fixture = json.loads(case['fixtureJson'])
    if 'csv-' in case['id']:
        records = len(list(csv.DictReader(io.StringIO(base64.b64decode(fixture['inputFiles']['input.csv']).decode('utf-8')))))
        source = source.replace('    parse_amount=int', '    processed=0\n    parse_amount=int')
        source = source.replace('        amount = parse_amount', '        nonlocal processed\n        processed += 1\n        amount = parse_amount')
        source = source.replace('    print(accepted)', f'    assert processed == {records}\n    print(accepted)')
    elif 'text-' in case['id']:
        lines = len(base64.b64decode(fixture['inputFiles']['input.txt']).decode('utf-8').splitlines())
        source = source.replace('    separator=None', '    processed=0')
        source = source.replace('        return line.lower().split(separator)', '        nonlocal processed\n        processed += 1\n        return line.lower().split()')
        source = source.replace('    print(total)', f'    assert processed == {lines}\n    print(total)')
    else:
        source = source.replace('    newline=', '    processed=0\n    newline=')
        source = source.replace('        return f"', '        nonlocal processed\n        processed += 1\n        return f"')
        source = source.replace('    print(written)', '    assert processed == len(records)\n    print(written)')
    assert source != case['source'] and 'nonlocal processed' in source
    item['id'] = case['id'].replace('.large', '.nonlocal-counter.large')
    item['source'] = source
    item['sourceUtf8Bytes'] = len(source.encode('utf-8'))
    item['sourceSha256'] = hashlib.sha256(source.encode('utf-8')).hexdigest()
    cases.append(item)
controls = json.loads((root.parent / 'regex-projection-storage-r219/catalog.json').read_text(encoding='utf-8'))['cases']
cases.append(next(c for c in controls if c['id'] == 'scripts.text-summary-helpers.large'))
cases.append(next(c for c in catalog['cases'] if c['id'] == 'scripts.csv-validated-records.large'))
json_control = json.loads((root.parent / 'json-cpython-r218/b566/representative.json').read_text(encoding='utf-8'))['cases']
cases.append(next(c for c in json_control if c['id'] == 'scripts.json-summary-helpers.large'))
assert len(cases) == 6
catalog['cases'] = cases
(root / 'catalog.json').write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

goldens = []
for item in cases[:3]:
    fixture = json.loads(item['fixtureJson'])
    with tempfile.TemporaryDirectory(prefix='cpython-', dir=root) as temporary:
        workspace = Path(temporary).resolve()
        assert workspace.parent == root.resolve()
        for name, data in fixture['inputFiles'].items():
            path = (workspace / name).resolve()
            assert path.is_relative_to(workspace)
            path.write_bytes(base64.b64decode(data))
        result = subprocess.run(['C:/Python313/python.exe', '-I', '-S', '-X', 'utf8', '-c', item['source']],
                                cwd=workspace, capture_output=True, text=True, encoding='utf-8', check=True)
        assert result.stdout == item['expectedOutput'], item['id']
        outputs = {}
        for name, data in fixture['outputFiles'].items():
            path = (workspace / name).resolve()
            assert path.is_relative_to(workspace)
            actual = path.read_bytes()
            assert actual == base64.b64decode(data), (item['id'], name)
            outputs[name] = hashlib.sha256(actual).hexdigest()
        goldens.append(dict(case=item['id'], stdout=result.stdout, outputFilesSHA256=outputs))
(root / 'cpython-goldens.json').write_text(json.dumps(dict(reference='CPython3.13.2 isolated local', cases=goldens,
    sourceChange='Observable nonlocal progress counter and caller assertion; text uses ordinary default-whitespace split instead of R139 explicit separator. Same source on both Lython images; original full fixtures/output files unchanged.'), indent=2) + '\n', encoding='utf-8')
source = subprocess.check_output(['git', 'ls-files', 'src/Lokad.Lython'], cwd=repo, text=True).splitlines()
identity = dict(revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repo, text=True).strip(),
    runtime='1208a6812ea51a34de9c2c3370ed56356c6e2efa; focused-qualified, full delivery will batch with retained R221',
    normalizedSource={p: hashlib.sha256((repo / p).read_bytes().replace(b'\r\n', b'\n')).hexdigest()
                      for p in source if p.endswith('.cs')}, baselineCoreSHA256={})
for framework in ['net11.0', 'net10.0']:
    destination = root / 'baseline' / framework
    destination.mkdir(parents=True)
    path = repo / f'src/Lokad.Lython/bin/Release/{framework}/Lokad.Lython.dll'
    shutil.copyfile(path, destination / path.name)
    identity['baselineCoreSHA256'][framework] = sha(path)
auxiliary = repo / 'benchmarks/Lokad.Lython.Benchmarks/bin/Release/net11.0'
destination = root / 'images/before'
destination.mkdir(parents=True)
for path in auxiliary.iterdir():
    if path.is_file() and path.suffix != '.pdb':
        shutil.copyfile(path, destination / path.name)
assert sha(destination / 'Lokad.Lython.dll') == identity['baselineCoreSHA256']['net11.0']
(root / 'before.json').write_text(json.dumps(identity, indent=2) + '\n', encoding='utf-8')
print('Fresh committed baseline frozen for both targets; three complete counter variants independently pass CPython/full file goldens.')
