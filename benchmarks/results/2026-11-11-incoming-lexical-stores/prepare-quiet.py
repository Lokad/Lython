from pathlib import Path
import ast
import hashlib
import json
import shutil
import subprocess
import tarfile

repo = Path('D:/code/Lython')
root = Path(__file__).parent
old = root.parent / 'json-string-decoder-r217/b565'
out = root / 'b567'
out.mkdir()
write = lambda p, s: p.write_text(s, encoding='utf-8', newline='\n')
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
ids = ['scripts.csv-validated-records.nonlocal-counter.large',
       'scripts.text-summary-helpers.nonlocal-counter.large',
       'scripts.report-render.nonlocal-counter.large', 'scripts.text-summary-helpers.large']
catalog = json.loads((root / 'catalog.json').read_text(encoding='utf-8'))
catalog['cases'] = [next(c for c in catalog['cases'] if c['id'] == name) for name in ids]
write(out / 'representative.json', json.dumps(catalog, ensure_ascii=False, indent=2) + '\n')
before = root / 'images/before/Lokad.Lython.dll'
candidate = root / 'images/candidate/Lokad.Lython.dll'
aux = json.loads((root.parent / 'json-cpython-r218/b566/identity.json').read_text(encoding='utf-8'))['auxiliaryFiles']
identity = dict(sourceRevision='R221 current1208/0b8 freshly forced committed reference versus exact owner-aware incoming lexical store prototype. Both sources share current R219 regex storage. 672internal/2587public focused checks per actual11/10 pass; three counter variants independently preserve CPython/full file goldens. Reference R219 full delivery pending; latest fully qualified e122 preserved separately. Original desktop adversity retained.',
    coreSHA256={name: sha(before if name.startswith('reference') else candidate)
                for name in ['reference', 'reference-control', 'candidate', 'candidate-control']},
    auxiliaryFiles=aux, catalogSHA256=sha(out / 'representative.json'), performanceQualified=False)
write(out / 'identity.json', json.dumps(identity, indent=2) + '\n')
for name in ['collect.py', 'archive-owned.py', 'run.sh', 'retrieve.py', 'summarize.py']:
    s = (old / name).read_text(encoding='utf-8').replace('json-string-b565-20261111', 'lexical-stores-b567-20261111').replace('lython-b565-json-string-20261111', 'lython-b567-lexical-stores-20261111')
    s = s.replace('len(jobs)==3', 'len(jobs)==4').replace("len(raw['jobs'])==3", "len(raw['jobs'])==4").replace('jobs=3', 'jobs=4').replace("['jobs']==3", "['jobs']==4").replace('B565 three-job', 'B567 four-job').replace("['elapsedSeconds']<150", "['elapsedSeconds']<180")
    if name == 'collect.py':
        lines = s.splitlines()
        i = next(i for i, line in enumerate(lines) if line.startswith('jobs='))
        lines[i] = 'jobs=' + repr([[name, 'warm'] for name in ids])
        s = '\n'.join(lines) + '\n'
        s = s.replace('time.monotonic()-started < 110', 'time.monotonic()-started < 140')
        s = s.replace('R214 exact bound native operand borrowing', 'R221 owner-aware incoming lexical stores')
        a = s.index("purpose='")
        b = s.index("',\n", a)
        s = s[:a] + "purpose='R221 complete CSV/text/report helpers with observable nonlocal counters, identical source/fixtures/output checks across both engines; ordinary text control. Existing owner-aware cell writes remove per-call context/name reconstruction. Allocation and original desktop adversity remain separate" + s[b:]
    if name.endswith('.py'):
        ast.parse(s)
    write(out / name, s)
write(out / 'contract.md', '''# R221 — bounded whole-script confirmation

Three existing complete captured CSV/text/report variants add one nonlocal progress
counter with a caller assertion; fixtures/full output files unchanged and independently
verified under CPython. Identical source across the two Lython images. Ordinary text
controls prefix/code-placement effects. No CPython lane or single-kernel substitute.
Four independent roles,5s warmup,7rotated400msrounds, normalGC/tiering, fixed fec79
worker/host/optional0.4 adapter. Warm sync only; desktop async/compile/allocation
and every adverse result remain separate. No fixed gain veto or universal CPU claim.
180s owner/30stop/30margin. Full mediated RAM I/O, per-invocation validation and
prior-result cleanup included; compile/reset/separate verification/protocol/startup/
final caches excluded. Wider CPU includes surrounding work and coarse ticks.
''')
inventory = json.loads((root / 'vm-idle-inventory.json').read_text(encoding='utf-8'))
mapping = [('direct-operands-b457-20261108/observations.tgz', '2026-11-08-direct-operand-transport', 'b457/observations.tgz'),
           ('shared-cased-split-b453-20261108/observations.tgz', '2026-11-08-shared-cased-split', 'b453/observations.tgz'),
           ('direct-native-fusion-b451-20261108/observations.tgz', '2026-11-08-native-method-facts', 'b451/observations.tgz')]
exact = []
for remote, folder, member in mapping:
    relative = 'benchmarks/results/' + folder + '/observations.tar.xz'
    wrapper = repo / relative
    assert wrapper.read_bytes() == subprocess.check_output(['git', 'show', 'HEAD:' + relative], cwd=repo)
    with tarfile.open(wrapper) as archive:
        data = archive.extractfile(member).read()
    expected = next(x for x in inventory['archives'] if x['path'] == remote)
    assert len(data) == expected['bytes'] and hashlib.sha256(data).hexdigest() == expected['sha256']
    exact.append(dict(path=remote, canonical=relative, canonicalMember=member, bytes=len(data), sha256=expected['sha256']))
write(out / 'exact-copies.json', json.dumps(exact, indent=2) + '\n')
write(out / 'superseded-cores.json', '[]\n')
with tarfile.open(out / 'payload.tgz', 'w:gz') as archive:
    for name in ['representative.json', 'identity.json', 'collect.py', 'archive-owned.py', 'run.sh', 'contract.md']:
        archive.add(out / name, arcname=name)
    archive.add(before, arcname='baseline-core.dll')
    archive.add(candidate, arcname='current-core.dll')
with tarfile.open(out / 'payload.tgz') as archive:
    footprint = sum(((m.size + 4095) // 4096) * 4096 for m in archive.getmembers() if m.isfile()) + 128 * 1024
size = (out / 'payload.tgz').stat().st_size
write(out / 'footprint.json', json.dumps(dict(payloadBytes=size, footprintBytes=footprint,
    requiredFreeBytes=(240 + 12) * 1024 * 1024 + max(footprint, 3 * size)), indent=2) + '\n')
s = (old / 'launch.py').read_text(encoding='utf-8').replace('json-string-b565-20261111', 'lexical-stores-b567-20261111').replace('lython-b565-json-string-20261111', 'lython-b567-lexical-stores-20261111').replace('json-string-b565-launch.py', 'lexical-stores-b567-launch.py').replace('lython-b565-fixtures', 'lython-b567-fixtures')
s = s.replace('construction-cpython-b561-20261110/candidate', 'json-cpython-b566-20261111/candidate')
s = s.replace('0e2bc55bac5ebfafd9d30014e34e2afb3e9776bbe6b5d83a53cc710c0d99c492', '9dadd59bd75528186c66a9a95e2efcf411e611e0ccaa718ff38c4facdb191e33')
s = s.replace(" protocol=base/'prepared-call-layout", " protected=base/'json-string-b565-20261111/reference/Lokad.Lython.dll'\n assert hashlib.sha256(protected.read_bytes()).hexdigest()=='c27160d53d66489976b181fb425b2f46c927fa85156433b80dc6f5477a3e5506'\n protocol=base/'prepared-call-layout")
s = s.replace(" assert shutil.disk_usage(base).free>config['required']", " assert hashlib.sha256(protected.read_bytes()).hexdigest()=='c27160d53d66489976b181fb425b2f46c927fa85156433b80dc6f5477a3e5506'\n assert shutil.disk_usage(base).free>config['required']")
s = s.replace('fullyQualified0ePreserved=True', 'fullyQualifiedE122AndB8Preserved=True')
s = s.replace('jobs=3,ownerSeconds=150', 'jobs=4,ownerSeconds=180').replace('RuntimeMaxSec=150', 'RuntimeMaxSec=180').replace('ownerSeconds=150', 'ownerSeconds=180').replace('seconds=210', 'seconds=240')
s = s.replace('[c]omplete-jobs.dll', '[c]omplete-jobs.dll|[p]ython3.*cpython-worker.py')
assert 'construction-cpython-b561' not in s and '0e2bc55b' not in s
ast.parse(s)
write(out / 'launch.py', s)
print('Four-job confirmation prepared without VM mutation; exact nested canonical proofs, no core retirement. Required free', json.loads((out / 'footprint.json').read_text())['requiredFreeBytes'])
