from pathlib import Path
import hashlib, json, shutil, subprocess, tarfile
root=Path('D:/code/Lython'); home=Path(__file__).parent; src=root/'src/Lokad.Lython'
baseline=json.loads((home/'baseline.json').read_text(encoding='utf-8'))
changed=subprocess.check_output(['git','-c','core.autocrlf=false','diff','--name-only','--','src/Lokad.Lython','tests/Lokad.Lython.Tests'],text=True).splitlines()
assert all(p.startswith(('src/Lokad.Lython/','tests/Lokad.Lython.Tests/')) for p in changed)
assert all((home/'before-source'/Path(p).relative_to('src/Lokad.Lython')).is_file() if p.startswith('src/')
           else (home/'before-tests'/Path(p).name).is_file() for p in changed), 'unexpected change; do not restore'
snapshot=home/'refined-source'
for p in src.rglob('*.cs'):
    if 'obj' in p.parts or 'bin' in p.parts: continue
    dst=snapshot/p.relative_to(src); dst.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(p,dst)
(home/'refined/source.patch').write_bytes(subprocess.check_output(['git','-c','core.autocrlf=false','diff','--','src/Lokad.Lython','tests']))

evidence=root/'benchmarks/results/2026-11-09-private-small-integers'; evidence.mkdir()
for name in ['baseline.json','identity.json','desktop.json','initial.patch','first-qualified.patch']:
    shutil.copyfile(home/name,evidence/name)
for name in ['identity.json','desktop.json','source.patch']:
    dst=evidence/'refined'/name; dst.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(home/'refined'/name,dst)
for stage in [home,home/'refined']:
    for label in ['warm-sync','warm-async','compile-sync']:
        for name in ['measurements.json','stderr.txt','hash-witness.json']:
            p=stage/label/name; dst=evidence/p.relative_to(home); dst.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(p,dst)
checks=evidence/'qualification'; checks.mkdir()
for p in home.glob('*.log'): shutil.copyfile(p,checks/p.name)
for p in (home/'refined').glob('*.log'): shutil.copyfile(p,checks/('refined-'+p.name))

production_names=[Path(p).relative_to('src/Lokad.Lython') for p in changed if p.startswith('src/')]+[Path('Runtime/PySmallInteger.cs')]
with tarfile.open(evidence/'source-checks.tar.xz','w:xz') as tar:
    for stage in ['initial-source','first-qualified-source','refined-source']:
        for rel in production_names:
            p=home/stage/rel
            if p.is_file(): tar.add(p,arcname=f'{stage}/{rel.as_posix()}')
    for name in ['SmallIntegerProjectionTests.cs']:
        tar.add(root/'tests/Lokad.Lython.Tests'/name,arcname='prototype-tests/'+name)
    for p in (home/'before-tests').glob('*.cs'):
        tar.add(p,arcname='before-tests/'+p.name)
        tar.add(root/'tests/Lokad.Lython.Tests'/p.name,arcname='refined-tests/'+p.name)
    for stage in [home,home/'refined']:
        tar.add(stage/'images/candidate/Lokad.Lython.dll',arcname=('wrapper' if stage==home else 'refined')+'/Lokad.Lython.dll')
    for name in ['prepare.py','consumers.py','extraction.py','test-extraction.py','prepare-measurement.py','qualify-measure.py','refine.py','measure.py']:
        tar.add(home/name,arcname='commands/'+name)
    tar.add(home/'catalog.json',arcname='catalog.json')

# Only validated task-owned files. Fresh writes, never restored timestamps.
for p in changed:
    path=root/p
    original=home/'before-source'/Path(p).relative_to('src/Lokad.Lython') if p.startswith('src/') else home/'before-tests'/Path(p).name
    path.write_bytes(original.read_bytes())
for p in [src/'Runtime/PySmallInteger.cs',root/'tests/Lokad.Lython.Tests/SmallIntegerProjectionTests.cs']:
    p.unlink()
for name,expected in baseline['productionSources'].items():
    assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==expected,name
(evidence/'withdrawal.json').write_text(json.dumps(dict(productionMatchesQualifiedRevision='cac4acf8',
    changedProductionFiles=len(production_names), changedWhiteboxFixtures=sum(p.startswith('tests/') for p in changed),
    preservedPublicCases=5, decision='Withdraw both representations; allocation benefit established, CPU uncertain and source complexity not justified for this milestone. No permanent representation veto.',
    fullCrossPlatformQualification='Not run for discarded prototypes; qualified production restored. Fresh builds and retained checks follow.'),indent=2)+'\n',encoding='utf-8')
print('Preserved raw/adverse evidence and both measured cores; restored all production and white-box fixtures exactly. New five public semantic cases retained.')
