from pathlib import Path
import ast,gzip,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).parent;q=r/'b592';q.mkdir(exist_ok=False)
old=r.parent/'dictionary-value-storage-r245/refined/b589'
name='native-loop-scope-b592-20261111';unit='lython-b592-native-loop-scope-20261111'
def replace(t):return t.replace('dictionary-values-b589-20261111',name).replace('lython-b589-dictionary-values-20261111',unit).replace('dictionary-values-b589-launch.py','native-loop-scope-b592-launch.py').replace('lython-b589-fixtures','lython-b592-fixtures').replace('B589','B592')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n))==h,n
reference=meta['images']['before']['Lokad.Lython.dll'];candidate=meta['images']['candidate']['Lokad.Lython.dll']
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'))
identity.update(sourceRevision='R247 one managed admission scope for native dictionary loop versus fully qualified8f8628a4/458be840. First binding/guards precede initial admission; cancellation/refusal checks stay inside. Scope unwinds before fallback/error/return; conservative extra logical nesting across binding callbacks explicit. 3046focused actual11/10 checks pass. Runtime/compile allocationflat; desktop capturedAsync+7.48..+8.35% with0.81%control and other conflicts/adversity preserved. No CPU/neutrality claim before bounded quiet confirmation.',coreSHA256={label:reference if label.startswith('reference')else candidate for label in ['reference','reference-control','candidate','candidate-control']},catalogSHA256=sha(r/'catalog.json'))
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps(dict(beforeRevision='8f8628a4',baselineDelivery='458be840/43087005596fullyGREENcached',prototypeUncommitted=True,sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],beforeCore=reference,candidateCore=candidate,qualification='Prototype3046focused actual11/10; baseline full both platforms/targets/stress/package. No full prototype qualification.'),indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'images/candidate/Lokad.Lython.dll',q/'current-core.dll')
jobs=[['scripts.text-summary-helpers.nonlocal-counter.large','warm'],['scripts.text-summary-helpers.nonlocal-counter.large','warm-async'],['scripts.text-summary-helpers.large','warm'],['scripts.text-summary-helpers.large','warm-async'],['scripts.csv-validated-records.large','warm']]
for filename in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=replace((old/filename).read_text(encoding='utf-8'))
 if filename=='collect.py':
  a=t.index('jobs=');b=t.index('\nenv = ',a);t=t[:a]+'jobs='+repr(jobs)+'\nassert len(jobs)==5 and all(case in cases for case,lane in jobs)\n'+t[b:]
  a=t.index("purpose='");b=t.index("',\n                  warmupSeconds",a)
  t=t[:a]+"purpose='R247 native-loop admission scope versus fully qualified8f862 source, captured/ordinary text both APIs plus CSV control. Original tight-control capturedAsync desktop adversity retained. Existing warm boundary includes mediated RAM I/O/checks/prior-result cleanup; no CPython ratio"+t[b:]
 if filename=='remote-launch.py':
  t=t.replace('captured-sequence-b583-20261111/candidate','dictionary-checkpoint-b590-20261111/candidate')
  a=t.index('protected=');b=t.index("with(base/'comparison-vm.lock')",a)
  t=t[:a]+"protected={source/'Lokad.Lython.dll':config['identity']['coreSHA256']['reference']}\n"+t[b:]
 (q/filename).write_text(t,encoding='utf-8',newline='\n')
 if filename.endswith('.py')and filename!='remote-launch.py':ast.parse(t)
assert 'native-loop-scope-b592-launch.py'in(q/'launch.py').read_text(encoding='utf-8')
with tarfile.open(q/'payload.tgz','w:gz')as archive:
 for filename in ['current-core.dll','representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:archive.add(q/filename,arcname=filename)
with tarfile.open(q/'payload.tgz')as archive:expanded=sum(((m.size+4095)//4096)*4096 for m in archive.getmembers())
retire=[]
canonical=Path('benchmarks/results/2026-11-11-known-call-binding/initial/before-net11.0.dll.gz');data=canonical.read_bytes()
assert data==subprocess.check_output(['git','show','HEAD:'+canonical.as_posix()]);core=gzip.decompress(data)
retire.append(dict(kind='all-aliases',knownPath='captured-sequence-b583-20261111/candidate/Lokad.Lython.dll',bytes=len(core),sha256=hashlib.sha256(core).hexdigest(),canonicalStorage=canonical.as_posix(),canonicalArchiveSHA256=sha(canonical),reason='Fully qualified690 image superseded by fully qualified8f862 current reference. Exact canonical core/source/evidence preserved in committed Git. Fresh UID/inode/all-alias proof; protect current selected8f862 reference.'))
for known,relative in [
 ('dictionary-values-b589-20261111/observations.tgz','2026-11-11-dictionary-integer-storage/refined/quiet/observations.tgz'),
 ('dictionary-checkpoint-b590-20261111/observations.tgz','2026-11-11-dictionary-cpython-checkpoint/observations.tgz'),
 ('current-value-profile-b591-20261111/observations.tgz','2026-11-11-current-storage-costs/observations.tgz')]:
 canonical=Path('benchmarks/results')/relative;data=canonical.read_bytes();assert data==subprocess.check_output(['git','show','HEAD:'+canonical.as_posix()])
 retire.append(dict(kind='exact',knownPath=known,bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),canonicalStorage=canonical.as_posix(),reason='Terminal raw archive duplicate, exact canonical committed Git bytes verified. Preserve every original observation and current core; fresh path/UID/inode/all-alias proof before retirement.'))
config=dict(root='/home/lokad/lython-bench-checks/'+name,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha(q/'payload.tgz'),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
assert "len(raw['jobs'])==5"in(q/'archive-owned.py').read_text(encoding='utf-8')
print('Five short jobs, existing180sowner/240sobserve; current8f862reference protected. Canonical old690core and duplicate raw archives verified; fresh inventory required before any VM mutation.')
