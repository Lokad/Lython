from pathlib import Path
import hashlib,json,shutil,subprocess,tarfile,xml.etree.ElementTree as E

root=Path(__file__).resolve().parent;repo=Path('D:/code/Lython')
target=repo/'benchmarks/results/2026-11-11-json-string-decoder';target.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
checks={}
for path in (root/'tests').glob('*.trx'):
    doc=E.parse(path);c=next(x.attrib for x in doc.iter()if x.tag.endswith('Counters'))
    checks[path.name]={k:int(v)for k,v in c.items()}
for suite in ['Lokad.Lython.Tests','Lokad.Lython.PublicApi.Tests']:
    for framework in ['net11.0','net10.0']:
        check=checks[f'delivery-{suite}-{framework}.trx'];assert check['failed']==0
paths=subprocess.check_output(['git','ls-files','src/Lokad.Lython'],cwd=repo,text=True).splitlines()
identity=dict(deliveryRevision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip(),base=json.loads((root/'before.json').read_text(encoding='utf-8')),measured=json.loads((root/'diagnostic-identity.json').read_text(encoding='utf-8')),
    finalSource={p:hashlib.sha256((repo/p).read_bytes().replace(b'\r\n',b'\n')).hexdigest()for p in paths if p.endswith('.cs')},
    finalCoreSHA256={f:sha(repo/f'src/Lokad.Lython/bin/Release/{f}/Lokad.Lython.dll')for f in ['net11.0','net10.0']},
    measurementLimit='Desktop/quiet source-pair measurements precede final cold error-plumbing cleanup: unused argument/usings removal and explicit cancellation in error-coordinate scans. Exact measured patch/images retained. Final source separately passes193internal/811public JSON checks per target; no final-source general CPU gain is claimed.',publication='Postponed; normal adapter0.4 unchanged; full platform delivery42928757271 pending.')
(target/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
(target/'focused-checks.json').write_text(json.dumps(checks,indent=2)+'\n',encoding='utf-8')
for name in ['freeze.py','prepare-measure.py','prepare-quiet.py','generate-goldens.py','record.py','measured-source.patch','measured-source.json','baseline-probes.json','candidate-probes.json','cpython-string-goldens.json']:
    shutil.copyfile(root/name,target/name)
for phase in ['warm','warm-async','compile']:
    (target/phase).mkdir(exist_ok=True)
    for name in ['summary.json','measurements.json']:shutil.copyfile(root/phase/name,target/phase/name)
(target/'quiet').mkdir(exist_ok=True)
for name in ['summary.json','identity.json','archive-receipt.json','terminal-status.json','observe-not-before.json','observations.tgz','footprint.json','exact-copies.json','superseded-cores.json']:
    shutil.copyfile(root/'b565'/name,target/'quiet'/name)
shutil.copyfile(root/'b565/retrieved/space-recovery.json',target/'quiet/space-recovery.json')
patch=subprocess.check_output(['git','diff',identity['base']['revision'],'--','src','tests','docs/RUNTIME.md','CHANGELOG.md'],cwd=repo)
(target/'final-source.patch').write_bytes(patch)
with tarfile.open(target/'preparation.tar.xz','w:xz')as archive:
    seen={}
    def add(path,name):
        if path.suffix=='.dll':
            digest=sha(path)
            if digest in seen:
                member=tarfile.TarInfo(name);member.type=tarfile.LNKTYPE;member.linkname=seen[digest];archive.addfile(member);return
            seen[digest]=name
        archive.add(path,arcname=name)
    for framework in ['net11.0','net10.0']:
        add(root/f'baseline/{framework}/Lokad.Lython.dll',f'baseline/{framework}/Lokad.Lython.dll')
        add(repo/f'src/Lokad.Lython/bin/Release/{framework}/Lokad.Lython.dll',f'final/{framework}/Lokad.Lython.dll')
    add(root/'images/candidate/Lokad.Lython.dll','measured/net11.0/Lokad.Lython.dll')
    for name in ['Lokad.Lython.Benchmarks.dll','Lokad.Lython.Utf8Regex.dll','Lokad.Utf8Regex.dll','Lokad.Utf8Regex.PythonRe.dll']:
        add(root/'baseline/net11.0'/name,'desktop-auxiliary/'+name)
    for name in ['tests/Lokad.Lython.PublicApi.Tests/JsonStringLexingTests.cs','tests/Lokad.Lython.PublicApi.Tests/JsonPrefixValidationTests.cs','tests/Lokad.Lython.Tests/EscapedJsonStorageTests.cs','tests/Lokad.Lython.Tests/JsonStringTokenStorageTests.cs']:
        add(repo/name,'delivery-source/'+name)
    for path in root.iterdir():
        if path.is_file()and path.suffix in {'.json','.log','.py','.txt','.patch'}:add(path,path.name)
    for folder in ['source-before','tests','tool','b565']:
        for path in (root/folder).rglob('*'):
            parts=path.relative_to(root).parts
            if any(p in {'retrieved','obj'}for p in parts):continue
            if path.is_file()and path.suffix in {'.json','.py','.cs','.csproj','.props','.log','.txt','.sh','.trx'}:
                add(path,path.relative_to(root).as_posix())
for name in ['preparation.tar.xz','quiet/observations.tgz']:
    with tarfile.open(target/name)as archive:
        for member in archive.getmembers():
            if member.isfile():assert len(archive.extractfile(member).read())==member.size
(target/'archive-identity.json').write_text(json.dumps({name:dict(bytes=(target/name).stat().st_size,sha256=sha(target/name))for name in ['preparation.tar.xz','quiet/observations.tgz']},indent=2)+'\n',encoding='utf-8')
print('Recorded source, original failures, supported Python goldens, all focused outcomes and both timing boundaries.',flush=True)
