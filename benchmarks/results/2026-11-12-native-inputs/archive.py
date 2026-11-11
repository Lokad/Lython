from pathlib import Path
import difflib,gzip,hashlib,json,shutil,subprocess,tarfile,xml.etree.ElementTree as ET
r=Path(__file__).resolve().parent;q=r/'b597';out=Path('benchmarks/results/2026-11-12-native-inputs');assert not out.exists()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
receipt=json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'))
assert receipt['complete']and receipt['jobs']==10 and receipt['allWorkersJoined']and receipt['archivedAndVerifiedUnderLease']and receipt['guestFixturesRemoved']
assert sha(q/'observations.tgz')==receipt['archiveSHA256']
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n))==h,n
out.mkdir()
for name in ['before.json','diagnostic-identity.json','catalog.json','sources-before.tar.xz','sources-candidate.tar.xz','focused-source-identity.json','intermediate-r250-source.json']:
 shutil.copyfile(r/name,out/name)
for phase in ['warm-sync','warm-async','compile']:shutil.copytree(r/phase,out/phase)
for p in r.glob('*.log'):shutil.copyfile(p,out/p.name)
for p in r.glob('*.py'):shutil.copyfile(p,out/p.name)
with tarfile.open(out/'focused-checks.tar.xz','w:xz')as archive:
 for p in sorted((r/'tests').glob('*.trx')):archive.add(p,arcname=p.name)
assert sha(r/'candidate-net10.dll')==meta['net10SHA256']
(out/'candidate-net10.0.dll.gz').write_bytes(gzip.compress((r/'candidate-net10.dll').read_bytes(),mtime=0))
quiet=out/'quiet';quiet.mkdir()
for p in q.iterdir():
 if p.is_file()and p.name not in ['payload.tgz','current-core.dll']:shutil.copyfile(p,quiet/p.name)
with tarfile.open(q/'observations.tgz')as archive:assert hashlib.sha256(archive.extractfile('current-core.dll').read()).hexdigest()==meta['images']['candidate']['Lokad.Lython.dll']
with tarfile.open(r/'sources-before.tar.xz')as b,tarfile.open(r/'sources-candidate.tar.xz')as c:
 before={m.name:m for m in b.getmembers()if m.isfile()};candidate={m.name:m for m in c.getmembers()if m.isfile()};patch=[]
 for name,h in meta['sourceHashes'].items():assert hashlib.sha256(c.extractfile(candidate[name]).read()).hexdigest()==h,name
 for name in meta['changedProductionFiles']:
  old=b.extractfile(before[name]).read().decode('utf-8').splitlines()if name in before else[]
  new=c.extractfile(candidate[name]).read().decode('utf-8').splitlines()
  patch.extend(difflib.unified_diff(old,new,fromfile='a/'+name if name in before else'/dev/null',tofile='b/'+name,lineterm=''))
(out/'prototype.patch').write_text('\n'.join(patch)+'\n',encoding='utf-8')
with tarfile.open(out/'test-sources.tar.xz','w:xz')as archive:
 for n in ['tests/Lokad.Lython.PublicApi.Tests/NativeWordBindingTests.cs','tests/Lokad.Lython.PublicApi.Tests/DictionaryCountingLoopTests.cs','tests/Lokad.Lython.PublicApi.Tests/CooperativeCancellationTests.cs','tests/Lokad.Lython.Tests/DictionaryCountingLoopAdmissionTests.cs','tests/Lokad.Lython.Tests/NativeClosureSequenceTests.cs']:archive.add(n,arcname=n)
baseline=json.loads((r/'before.json').read_text(encoding='utf-8'))
(out/'reference-images.json').write_text(json.dumps(dict(source='Fully qualified8f8628a4, unchanged production througha2abcac1;43087005596terminalGREENcached',net11=dict(path='benchmarks/results/2026-11-11-dictionary-integer-storage/refined/quiet/observations.tgz',entry='current-core.dll',sha256=baseline['images']['before']['Lokad.Lython.dll']),net10=dict(path='benchmarks/results/2026-11-11-dictionary-integer-storage/refined/candidate-net10.0.dll.gz',sha256=baseline['net10SHA256']),patches='Supplemental LF-normalized text diff; exact source bytes are in each source archive.'),indent=2)+'\n',encoding='utf-8')
print('Exact source/core pair, both-target checks, all local/adverse phases, original failed fixtures/inventory and verified ten-job quiet raw archive retained. Disposition remains separate.')
