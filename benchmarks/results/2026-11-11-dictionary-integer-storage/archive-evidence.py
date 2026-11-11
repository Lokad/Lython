from pathlib import Path
import gzip,hashlib,json,shutil,subprocess,tarfile
repo=Path.cwd();r=Path(__file__).parent;out=repo/'benchmarks/results/2026-11-11-dictionary-integer-storage'
assert not out.exists()
assert (r/'refined/b589/terminal-status.json').exists()
out.mkdir()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for label,base,quiet in [('initial',r,'b588'),('refined',r/'refined','b589')]:
 dest=out/label;dest.mkdir()
 meta=json.loads((base/'diagnostic-identity.json').read_text(encoding='utf-8'))
 for name in ['before.json','diagnostic-identity.json','catalog.json','comparison.log','focused.log','sources-before.tar.xz','sources-candidate.tar.xz','jit-inspection.log']:
  shutil.copyfile(base/name,dest/name)
 for phase in ['warm-sync','warm-async','compile']:shutil.copytree(base/phase,dest/phase)
 for p in base.glob('*.log'):shutil.copyfile(p,dest/p.name)
 shutil.copytree(base/'jit',dest/'jit')
 core=base/'candidate-net10.dll';assert sha(core)==meta['net10SHA256']
 (dest/'candidate-net10.0.dll.gz').write_bytes(gzip.compress(core.read_bytes(),mtime=0))
 q=base/quiet;qout=dest/'quiet';qout.mkdir()
 receipt=json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'))
 assert receipt['complete']and receipt['allWorkersJoined']and receipt['guestFixturesRemoved']and receipt['archivedAndVerifiedUnderLease']
 assert sha(q/'observations.tgz')==receipt['archiveSHA256']
 for p in q.iterdir():
  if p.is_file()and p.name not in ['current-core.dll','payload.tgz']:
   shutil.copyfile(p,qout/p.name)
 # The raw VM archive already preserves the exact net11 candidate. Avoid
 # another core copy; verify that entry against the source-pair identity.
 with tarfile.open(q/'observations.tgz')as archive:
  core=archive.extractfile('current-core.dll').read()
 assert hashlib.sha256(core).hexdigest()==meta['images']['candidate']['Lokad.Lython.dll']
for p in r.glob('*.py'):shutil.copyfile(p,out/p.name)
for name in ['initial-assessment.json','python-goldens.json']:shutil.copyfile(r/name,out/name)
failed=r/'refined/b589-four-job-inventory-refusal'
shutil.copytree(failed,out/'refined/preparation-refusal',ignore=shutil.ignore_patterns('payload.tgz','current-core.dll'))
with(out/'prototype.patch').open('wb')as f:subprocess.run(['git','diff','--binary','--','src/Lokad.Lython'],stdout=f,check=True)
(out/'reference-images.json').write_text(json.dumps(dict(runtime='Fully qualified6904093e, exact frozenccdd; no changed core source in e2240c23',net11=dict(path='benchmarks/results/2026-11-11-known-call-binding/initial/before-net11.0.dll.gz',sha256=json.loads((r/'before.json').read_text(encoding='utf-8'))['images']['before']['Lokad.Lython.dll']),net10=dict(path='benchmarks/results/2026-11-11-known-call-binding/initial/before-net10.0.dll.gz',sha256=json.loads((r/'before.json').read_text(encoding='utf-8'))['net10SHA256']),candidateNet11='Each form quiet/observations.tgz current-core.dll; independently verified against diagnostic-identity.json. Reuse exact raw image, no duplicate core bundle.'),indent=2)+'\n',encoding='utf-8')
print('Both exact source pairs, actual-target cores, all local observations/JIT and verified raw VM archives preserved; baseline images referenced canonically, duplicate bundles omitted.')
