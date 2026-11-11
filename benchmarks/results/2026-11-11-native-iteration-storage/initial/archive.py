from pathlib import Path
import difflib,gzip,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).parent;out=Path('benchmarks/results/2026-11-11-native-iteration-storage');assert not out.exists()
q=r/'refined/b593';receipt=json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert receipt['complete']and receipt['allWorkersJoined']and receipt['guestFixturesRemoved']and receipt['archivedAndVerifiedUnderLease']and receipt['jobs']==7
assert sha(q/'observations.tgz')==receipt['archiveSHA256']
out.mkdir()
for label,base in [('initial',r),('refined',r/'refined')]:
 dest=out/label;dest.mkdir();meta=json.loads((base/'diagnostic-identity.json').read_text(encoding='utf-8'))
 for name in ['before.json','diagnostic-identity.json','catalog.json','sources-before.tar.xz','sources-candidate.tar.xz','focused-source-identity.json']:
  shutil.copyfile(base/name,dest/name)
 for phase in ['warm-sync','warm-async','compile']:shutil.copytree(base/phase,dest/phase)
 for p in base.glob('*.log'):shutil.copyfile(p,dest/p.name)
 for p in base.glob('*.py'):shutil.copyfile(p,dest/p.name)
 assert sha(base/'candidate-net10.dll')==meta['net10SHA256']
 (dest/'candidate-net10.0.dll.gz').write_bytes(gzip.compress((base/'candidate-net10.dll').read_bytes(),mtime=0))
 # Initial net11 image has no VM archive; refined image lives in its raw archive.
 if label=='initial':(dest/'candidate-net11.0.dll.gz').write_bytes(gzip.compress((base/'images/candidate/Lokad.Lython.dll').read_bytes(),mtime=0))
 with tarfile.open(dest/'focused-checks.tar.xz','w:xz')as archive:
  for p in sorted((base/'tests').glob('*.trx')):archive.add(p,arcname=p.name)
 with tarfile.open(base/'sources-before.tar.xz')as b,tarfile.open(base/'sources-candidate.tar.xz')as c:
  before={m.name:m for m in b.getmembers()if m.isfile()};candidate={m.name:m for m in c.getmembers()if m.isfile()};patch=[]
  for name,h in meta['sourceHashes'].items():assert hashlib.sha256(c.extractfile(candidate[name]).read()).hexdigest()==h,name
  for name in meta['changedProductionFiles']:
   old=b.extractfile(before[name]).read().decode('utf-8').splitlines()if name in before else[]
   new=c.extractfile(candidate[name]).read().decode('utf-8').splitlines()
   patch.extend(difflib.unified_diff(old,new,fromfile='a/'+name if name in before else'/dev/null',tofile='b/'+name,lineterm=''))
 (dest/'prototype.patch').write_text('\n'.join(patch)+'\n',encoding='utf-8')
quiet=out/'refined/quiet';quiet.mkdir()
for p in q.iterdir():
 if p.is_file()and p.name not in ['payload.tgz','current-core.dll']:shutil.copyfile(p,quiet/p.name)
meta=json.loads((r/'refined/diagnostic-identity.json').read_text(encoding='utf-8'))
with tarfile.open(q/'observations.tgz')as archive:assert hashlib.sha256(archive.extractfile('current-core.dll').read()).hexdigest()==meta['images']['candidate']['Lokad.Lython.dll']
shutil.copyfile(r/'python-goldens.json',out/'python-goldens.json')
tests=out/'test-sources';tests.mkdir()
for name in ['ReturnedIterationTests.cs','SimpleFunctionExecutionBoundaryTests.cs']:
 source=Path('tests/Lokad.Lython.PublicApi.Tests'if name=='ReturnedIterationTests.cs'else'tests/Lokad.Lython.Tests')/name
 shutil.copyfile(source,tests/name)
baseline=json.loads((r/'before.json').read_text(encoding='utf-8'))
(out/'reference-images.json').write_text(json.dumps(dict(source='Fully qualified8f8628a4, same production in458be840/9f400b71,43087005596terminalGREENcached',net11=dict(path='benchmarks/results/2026-11-11-dictionary-integer-storage/refined/quiet/observations.tgz',entry='current-core.dll',sha256=baseline['images']['before']['Lokad.Lython.dll']),net10=dict(path='benchmarks/results/2026-11-11-dictionary-integer-storage/refined/candidate-net10.0.dll.gz',sha256=baseline['net10SHA256']),patches='Supplemental LF-normalized text diffs; exact source bytes are in each source archive.'),indent=2)+'\n',encoding='utf-8')
print('Both source/core forms, all local/adverse observations and focused checks, eight Python goldens, public regressions and verified seven-job quiet raw archive retained. No disposition inferred by archival.')
