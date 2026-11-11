from pathlib import Path
import gzip,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).parent;q=r/'b592';out=Path('benchmarks/results/2026-11-11-native-loop-admission')
assert not out.exists();out.mkdir()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n))==h,n
for n in ['before.json','diagnostic-identity.json','catalog.json','comparison.log','focused.log','sources-before.tar.xz','sources-candidate.tar.xz']:
 shutil.copyfile(r/n,out/n)
for phase in ['warm-sync','warm-async','compile']:shutil.copytree(r/phase,out/phase)
with tarfile.open(out/'focused-checks.tar.xz','w:xz')as archive:
 for p in sorted(r.glob('stable-*.log')):archive.add(p,arcname=p.name)
 for p in sorted((r/'tests').glob('*.trx')):archive.add(p,arcname='tests/'+p.name)
assert sha(r/'candidate-net10.dll')==meta['net10SHA256']
(out/'candidate-net10.0.dll.gz').write_bytes(gzip.compress((r/'candidate-net10.dll').read_bytes(),mtime=0))
receipt=json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'))
assert receipt['complete']and receipt['allWorkersJoined']and receipt['guestFixturesRemoved']and receipt['archivedAndVerifiedUnderLease']
assert sha(q/'observations.tgz')==receipt['archiveSHA256']
quiet=out/'quiet';quiet.mkdir()
for p in q.iterdir():
 if p.is_file()and p.name not in ['payload.tgz','current-core.dll']:shutil.copyfile(p,quiet/p.name)
with tarfile.open(q/'observations.tgz')as archive:core=archive.extractfile('current-core.dll').read()
assert hashlib.sha256(core).hexdigest()==meta['images']['candidate']['Lokad.Lython.dll']
for p in r.glob('*.py'):shutil.copyfile(p,out/p.name)
with(out/'prototype.patch').open('wb')as f:subprocess.run(['git','diff','--binary','--','src/Lokad.Lython'],stdout=f,check=True)
decision=dict(status='Withdrawn after bounded confirmation; uncertainty remains, no permanent design veto.',baseline='Fully qualified8f8628a4 production;458be840 exact same production source;43087005596terminalGREENcached.',changedProductionFiles=meta['changedProductionFiles'],focusedActualTargets=dict(net11=1523,net10=1523,total=3046),runtimeAndCompilationAllocation='Flat in desktop three-image source pair.',desktopAdversity='Captured text RunAsync elapsed+7.48..+8.35%, unchanged control0.81%; cause remains unexplained. Other desktop observations mixed and preserved.',quietSummary='141.216808seconds, five jobs, four independent processes each. Captured Run elapsed−1.17..+1.78%; RunAsync−3.02..+2.80%; moving reference controls2.60/5.38%. Ordinary Run−1.78..−0.44%, widerCPU−7.80..+0.85%. OrdinaryAsync−5.05..+1.28%. UnaffectedCSV control also−2.00..−0.58%. No useful general CPU gain or neutrality established.',reason='Small mechanical per-word admission reduction adds persistent cleanup state and extends logical nesting across namespace reentry. Complete-script evidence does not justify this contract/lifetime change. Restore original managed stack-admission boundary. No fixed percentage veto, no rejection of broader call/operand redesign.',ownershipAndCleanup='All output/files/balances/quiet/identity/worker joins/RAM cleanup/persistent archive checks pass. Initial disk refusal stopped before mutation, reserve unchanged; verified committed duplicates retired under fresh idle/UID/inode/all-alias proofs.',fullPrototypeQualification=False,publication=False)
(out/'decision.json').write_text(json.dumps(decision,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
references=dict(net11Before=dict(path='benchmarks/results/2026-11-11-dictionary-integer-storage/refined/quiet/observations.tgz',entry='current-core.dll',sha256=meta['images']['before']['Lokad.Lython.dll']),net10Before=dict(path='benchmarks/results/2026-11-11-dictionary-integer-storage/refined/candidate-net10.0.dll.gz',sha256=json.loads((r/'before.json').read_text(encoding='utf-8'))['net10SHA256']),net11Candidate=dict(path='quiet/observations.tgz',entry='current-core.dll',sha256=meta['images']['candidate']['Lokad.Lython.dll']))
(out/'reference-images.json').write_text(json.dumps(references,indent=2)+'\n',encoding='utf-8')
target=Path(meta['changedProductionFiles'][0]);assert len(meta['changedProductionFiles'])==1
original=r/'originals'/target;before=json.loads((r/'before.json').read_text(encoding='utf-8'))
assert sha(original)==before['sourceHashes'][target.as_posix()]
target.write_bytes(original.read_bytes())
assert not subprocess.check_output(['git','diff','--','src/Lokad.Lython'])
print('Exact sources/cores, all local and quiet adversity/checks retained. Prototype withdrawn; production source restored byte-for-byte to qualified baseline. Nonincremental rebuild required before next no-build check/freeze.')
