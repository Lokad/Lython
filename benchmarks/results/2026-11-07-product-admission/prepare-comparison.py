from pathlib import Path
import base64, hashlib, json, os, shutil, struct, subprocess, tempfile

r = Path(__file__).parent
home = r.parent
catalog = json.loads((home / 'module-bindings-r49/catalog.json').read_text(encoding='utf-8'))
source = '''import csv, math
totals = {}
with open('matrix.csv', newline='') as source:
    for row in csv.DictReader(source):
        size = math.prod([int(row['a']), int(row['b']), int(row['c']), int(row['d'])])
        kind = row['kind']
        totals[kind] = totals.get(kind, 0) + size
with open('report.txt', 'w') as target:
    for kind in sorted(totals):
        target.write(f'{kind}: {totals[kind]}\\n')
print(sum(totals.values()))
'''
data = ('kind,a,b,c,d\n' + ''.join(f'g{i % 8},{i % 7 + 1},{i % 5 + 1},{i % 3 + 1},{i % 11 + 1}\n' for i in range(1024))).encode('utf-8')
with tempfile.TemporaryDirectory(prefix='lython-product-golden-') as folder:
    p = Path(folder)
    (p / 'matrix.csv').write_bytes(data)
    (p / 'script.py').write_text(source, encoding='utf-8')
    result = subprocess.run(['C:/Python313/python.exe', '-I', '-S', '-X', 'utf8', 'script.py'], cwd=p, capture_output=True, check=True)
    stdout = result.stdout.decode('utf-8').replace('\r\n', '\n')
    outputs = {'report.txt': (p / 'report.txt').read_bytes().replace(b'\r\n', b'\n')}
    (r / 'cpython-product-version.txt').write_text(subprocess.check_output(['C:/Python313/python.exe', '--version'], text=True), encoding='utf-8')
fixture = json.dumps(dict(size=1024, text='', inputFiles={'matrix.csv': base64.b64encode(data).decode()},
    outputFiles={k:base64.b64encode(v).decode() for k,v in outputs.items()}), separators=(',', ':'))
h = hashlib.sha256(struct.pack('>i', len(outputs)))
for name, data in sorted(outputs.items()):
    name = name.encode('utf-8')
    h.update(struct.pack('>i', len(name))); h.update(name)
    h.update(struct.pack('>i', len(data))); h.update(data)
row = dict(id='scripts.matrix-products.1024', family='scripts', category='library', scale='diagnostic', size=1024,
    source=source, fixtureJson=fixture, expectedOutput=stdout,
    sourceUtf8Bytes=len(source.encode()), expectedOutputUtf8Bytes=len(stdout.encode()), fixtureTextUtf8Bytes=0, fixtureTextCodePoints=0,
    sourceSha256=hashlib.sha256(source.encode()).hexdigest(), fixtureSha256=hashlib.sha256(fixture.encode()).hexdigest(),
    expectedOutputSha256=hashlib.sha256(stdout.encode()).hexdigest(), expectedFilesSha256=h.hexdigest())
catalog['cases'] = [row] + [c for c in catalog['cases'] if 'file-search' not in c['id']]
assert len(catalog['cases']) == 5
(r / 'catalog.json').write_text(json.dumps(catalog, indent=2)+'\n', encoding='utf-8')
for role in ['before', 'candidate']:
    target = r / 'images' / role
    shutil.copytree(home / 'admission-reentry-r53/images/candidate', target, copy_function=os.link)
    (target / 'Lokad.Lython.dll').unlink()
    source_core = r / 'baseline-core.dll' if role == 'before' else Path('src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll')
    shutil.copyfile(source_core, target / 'Lokad.Lython.dll')
identity = dict(baseline='525172bc, qualified R55 runtime, native regression 2 pass/4 fail',
    candidate='525172bc plus R57 product lifetime/start correction',
    coreSHA256={role:hashlib.sha256((r/'images'/role/'Lokad.Lython.dll').read_bytes()).hexdigest() for role in ['before','candidate']},
    productionSources={str(p).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in Path('src/Lokad.Lython').rglob('*.cs') if not any(s in ['bin','obj'] for s in p.parts)},
    comparison='Existing R45 driver unchanged; five complete jobs, shared ALC/CoreLib/hash seed/process GC.32 warmups/image,5 rotated rounds*4 jobs; normal GC/tiering; diagnostic desktop elapsed only.',
    affected='1024-row CSV matrix-size report uses four-factor products; CPython frozen stdout/file goldens.',
    controls='Original ordinary CSV/text/JSON/report sources, fixtures and goldens unchanged.',
    boundary='Warm Run/awaited RunAsync, mediated file I/O, full validation and result cleanup; compilation/startup/final cached teardown excluded.')
(r / 'comparison-identity.json').write_text(json.dumps(identity, indent=2)+'\n', encoding='utf-8')
print(json.dumps({'frozenCores':identity['coreSHA256'],'fiveCompleteJobs':True}))
