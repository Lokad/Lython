from pathlib import Path
import ast, hashlib, io, json, subprocess, tarfile

repo = Path.cwd()
root = Path(__file__).parent
out = root / 'b568'
out.mkdir(exist_ok=True)
old = root.parent / 'json-cpython-r218/b566'
write = lambda p, s: p.write_text(s, encoding='utf-8', newline='\n')
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
catalog = json.loads((root / 'representative.json').read_text(encoding='utf-8'))
write(out / 'representative.json', json.dumps(catalog, ensure_ascii=False, indent=2) + '\n')
source = json.loads((repo / 'benchmarks/results/2026-11-11-incoming-lexical-stores/source-identity.json').read_text(encoding='utf-8'))
core = root.parent / 'incoming-lexical-stores-r221/images/candidate/Lokad.Lython.dll'
assert sha(core) == source['coreSHA256']['candidate']
revision = '4625d83e00ba99fab6c01cef93b56755f09c284d'
blob = subprocess.check_output(['git', 'archive', revision, 'src/Lokad.Lython'])
with tarfile.open(fileobj=io.BytesIO(blob)) as a:
    sources = {m.name: hashlib.sha256(a.extractfile(m).read().replace(b'\r\n', b'\n')).hexdigest()
               for m in a.getmembers() if m.isfile() and m.name.endswith('.cs')}
assert len(sources) == 568 and sources == source['measuredProductionSource']
identity = dict(sourceRevision='R223 frozenR221 cb03fd47, all568 production hashes equal committed4625d83e/0.11.0. Combined delivery42942735331 still active; local672internal/2587public eachactualtarget pass. Later R222 frontend source is separate. Ordinary and captured-helper/counter text versus stockCPython3.13.16; compilation/warm/compile-to-result separate, no single-change attribution.',
    coreSHA256={n: sha(core) for n in ['candidate', 'candidate-control']},
    auxiliaryFiles=json.loads((old / 'identity.json').read_text(encoding='utf-8'))['auxiliaryFiles'],
    catalogSHA256=sha(out / 'representative.json'), performanceQualified=False)
write(out / 'identity.json', json.dumps(identity, indent=2) + '\n')
write(out / 'source-identity.json', json.dumps(dict(runtimeRevision=revision, normalizedSource=sources, coreSHA256=sha(core)), indent=2) + '\n')

prior = repo / 'benchmarks/results/2026-11-10-construction-cpython-checkpoint/observations.tgz'
assert prior.read_bytes() == subprocess.check_output(['git', 'show', 'HEAD:' + prior.relative_to(repo).as_posix()])
with tarfile.open(prior) as a:
    previous = next(c for c in json.loads(a.extractfile('representative.json').read())['cases'] if c['id'] == 'scripts.text-summary-helpers.large')
for key in ['sourceSha256', 'fixtureSha256', 'expectedOutputSha256', 'expectedFilesSha256']:
    assert catalog['cases'][0][key] == previous[key], key
write(out / 'prior-case-proof.json', json.dumps(dict(keys={k: previous[k] for k in ['sourceSha256', 'fixtureSha256', 'expectedOutputSha256', 'expectedFilesSha256']}, canonical=prior.relative_to(repo).as_posix()), indent=2) + '\n')
for name in ['collect.py', 'archive-owned.py', 'run.sh', 'retrieve.py', 'summarize.py']:
    s = (old / name).read_text(encoding='utf-8').replace('json-cpython-b566-20261111', 'text-cpython-b568-20261111').replace('lython-b566-json-cpython-20261111', 'lython-b568-text-cpython-20261111').replace('B566', 'B568')
    if name == 'collect.py':
        lines = s.splitlines()
        i = next(i for i, line in enumerate(lines) if line.startswith('jobs='))
        lines[i] = 'jobs=' + repr([[c['id'], lane] for c in catalog['cases'] for lane in ['compile', 'warm', 'compile-run']])
        s = '\n'.join(lines) + '\n'
        a = s.index("purpose='"); b = s.index("',\n", a)
        s = s[:a] + "purpose='R223 exact4625/cb03 complete ordinary text and captured-helper-counter CPython checkpoint. All568 source hashes verified; delivery42942735331 active separately. Compilation/warm/compile-to-result separate, mediated I/O/full validation/prior-result cleanup included; reset/protocol/startup/final caches excluded. No single-change attribution or later HEAD claim" + s[b:]
    if name.endswith('.py'): ast.parse(s)
    write(out / name, s)
write(out / 'contract.md', (root / 'contract.md').read_text(encoding='utf-8'))
exact = []
for remote, canonical in [
    ('json-cpython-b566-20261111/observations.tgz', 'benchmarks/results/2026-11-11-json-cpython-checkpoint/observations.tgz'),
    ('lexical-stores-b567-20261111/observations.tgz', 'benchmarks/results/2026-11-11-incoming-lexical-stores/quiet/observations.tgz')]:
    data = (repo / canonical).read_bytes()
    assert data == subprocess.check_output(['git', 'show', 'HEAD:' + canonical])
    exact.append(dict(path=remote, canonical=canonical, bytes=len(data), sha256=hashlib.sha256(data).hexdigest()))
write(out / 'exact-copies.json', json.dumps(exact, indent=2) + '\n')
canonical = 'benchmarks/results/2026-11-08-native-json-construction/observations.tar.xz'
data = (repo / canonical).read_bytes()
assert data == subprocess.check_output(['git', 'show', 'HEAD:' + canonical])
with tarfile.open(fileobj=io.BytesIO(data)) as a:
    nested = a.extractfile('b463/observations.tgz').read()
exact.append(dict(path='native-json-b463-20261108/observations.tgz', canonical=canonical,
    canonicalMember='b463/observations.tgz', bytes=len(nested), sha256=hashlib.sha256(nested).hexdigest()))
write(out / 'exact-copies.json', json.dumps(exact, indent=2) + '\n')
write(out / 'superseded-cores.json', '[]\n')
with tarfile.open(out / 'payload.tgz', 'w:gz') as a:
    for name in ['representative.json', 'identity.json', 'collect.py', 'archive-owned.py', 'run.sh', 'contract.md']:
        a.add(out / name, arcname=name)
    a.add(core, arcname='current-core.dll')
with tarfile.open(out / 'payload.tgz') as a:
    footprint = sum(((m.size + 4095) // 4096) * 4096 for m in a.getmembers() if m.isfile()) + 128 * 1024
size = (out / 'payload.tgz').stat().st_size
write(out / 'footprint.json', json.dumps(dict(payloadBytes=size, footprintBytes=footprint,
    requiredFreeBytes=252 * 1024 * 1024 + max(footprint, 3 * size)), indent=2) + '\n')
s = (old / 'launch.py').read_text(encoding='utf-8').replace('json-cpython-b566-20261111', 'text-cpython-b568-20261111').replace('lython-b566-json-cpython-20261111', 'lython-b568-text-cpython-20261111').replace('json-cpython-b566-launch.py', 'text-cpython-b568-launch.py').replace('lython-b566-fixtures', 'lython-b568-fixtures')
s = s.replace("source=base/'json-string-b565-20261111/reference'", "source=base/'json-cpython-b566-20261111/candidate'")
s = s.replace('c27160d53d66489976b181fb425b2f46c927fa85156433b80dc6f5477a3e5506', '9dadd59bd75528186c66a9a95e2efcf411e611e0ccaa718ff38c4facdb191e33')
s = s.replace(" protocol=base/'prepared-call-layout", " protected=base/'json-string-b565-20261111/reference/Lokad.Lython.dll'\n assert hashlib.sha256(protected.read_bytes()).hexdigest()=='c27160d53d66489976b181fb425b2f46c927fa85156433b80dc6f5477a3e5506'\n protocol=base/'prepared-call-layout")
s = s.replace(" assert shutil.disk_usage(base).free>config['required']", " assert hashlib.sha256(protected.read_bytes()).hexdigest()=='c27160d53d66489976b181fb425b2f46c927fa85156433b80dc6f5477a3e5506'\n assert shutil.disk_usage(base).free>config['required']")
s = s.replace('qualifiedB8BaselinePreserved=True', 'qualifiedB8AndE122Preserved=True')
assert 'construction-cpython-b561' not in s and '0e2bc55b' not in s
ast.parse(s)
write(out / 'launch.py', s)
print(json.dumps(dict(payloadBytes=size, requiredFreeBytes=252 * 1024 * 1024 + max(footprint, 3 * size), exactDuplicatesBytes=sum(x['bytes'] for x in exact))))
