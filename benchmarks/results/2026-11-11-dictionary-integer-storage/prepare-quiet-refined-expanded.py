from pathlib import Path
import ast,gzip,hashlib,json,shutil,subprocess,tarfile
parent=Path(__file__).parent;r=parent/'refined';q=r/'b589';q.mkdir(exist_ok=False);base=parent.parent
old=base/'native-counter-loops-r237/b576'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for name,h in meta['sourceHashes'].items():assert sha(Path(name))==h,name
root='/home/lokad/lython-bench-checks/dictionary-values-b589-20261111';unit='lython-b589-dictionary-values-20261111'
def replace(t):return t.replace('/native-counter-loops-b576-20261111','/dictionary-values-b589-20261111').replace('lython-b576-native-counter-loops-20261111',unit).replace('lython-b576-native-counter-loops',unit.removesuffix('-20261111')).replace('native-counter-loops-b576','dictionary-values-b589').replace('lython-b576-fixtures','lython-b589-fixtures')
reference=meta['images']['before']['Lokad.Lython.dll'];candidate=meta['images']['candidate']['Lokad.Lython.dll']
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'))
identity.update(sourceRevision='R245 refined dictionary value storage, direct shared-record array reads eliminate dead inlined integer-allocation branch versus fully qualified6904093e. Exact source/core pair, existing scripts/worker/adapter; Refined focused actual11/10 and4isolated CPython correctness goldens pass. Entry capacity grows8bytes, includes funding for eventual immutable boxes; copies/shared layouts preserve identity. Desktop allocation/elapsed evidence preserved separately; no CPU gain or neutrality claim before quiet confirmation',coreSHA256={role:reference if role.startswith('reference')else candidate for role in ['reference','candidate','reference-control','candidate-control']},catalogSHA256=sha(r/'catalog.json'))
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps(dict(beforeRevision='6904093e',prototypeUncommitted=True,sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],beforeCore=reference,candidateCore=candidate,qualification='Qualified69043058223560GREEN cached; R245focused only, no full prototype qualification yet.'),indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'images/candidate/Lokad.Lython.dll',q/'current-core.dll')
jobs=[['scripts.text-summary-helpers.nonlocal-counter.large', 'warm'], ['scripts.text-summary-helpers.large', 'warm'], ['scripts.csv-validated-records.large', 'warm'], ['scripts.report-render.large', 'warm-async'], ['scripts.json-summary-helpers.large', 'warm']]
t=replace((old/'collect.py').read_text(encoding='utf-8'))
start=t.index('jobs=');end=t.index('\nenv = ',start);t=t[:start]+'jobs='+repr(jobs)+'\nassert len(jobs)==5 and all(case in cases for case,lane in jobs)\n'+t[end:]
start=t.index("purpose='R237");end=t.index("',\n                  warmupSeconds",start)
t=t[:start]+"purpose='R245 native dictionary values versus qualified690, captured/ordinary text both APIs and CSV control. Both forms, all desktop adversity preserved. Warm includes mediated RAM I/O/full checks/prior-result cleanup, excludes compile/startup/reset/protocol/final caches; no CPython ratio"+t[end:]
(q/'collect.py').write_text(t,encoding='utf-8',newline='\n');ast.parse(t)
for n in ['archive-owned.py','run.sh','retrieve.py','summarize.py','launch.py']:
 t=replace((old/n).read_text(encoding='utf-8')).replace('B576','B589')
 t=t.replace('seconds=270','seconds=240').replace('ownerSeconds=210','ownerSeconds=180').replace("['elapsedSeconds']<210","['elapsedSeconds']<180")
 (q/n).write_text(t,encoding='utf-8',newline='\n')
 if n.endswith('.py'):ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as t:
 for n in ['current-core.dll','representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:t.add(q/n,arcname=n)
with tarfile.open(q/'payload.tgz')as t:expanded=sum(((m.size+4095)//4096)*4096 for m in t.getmembers())
retire=[]
for canonicalName,known in [
 ('2026-11-11-known-call-binding/initial/quiet/observations.tgz','known-call-binding-b586-20261111/observations.tgz'),
 ('2026-11-11-known-call-binding/refined/quiet/observations.tgz','known-call-binding-b587-20261111/observations.tgz'),
 ('2026-11-11-native-counting-loops/quiet/observations.tgz','native-counter-loops-b576-20261111/observations.tgz')]:
 canonical=Path('benchmarks/results')/canonicalName;data=canonical.read_bytes()
 assert data==subprocess.run(['git','show','HEAD:'+canonical.as_posix()],capture_output=True,check=True).stdout
 retire.append(dict(kind='exact',knownPath=known,bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),canonicalStorage=canonical.as_posix(),reason='Duplicate retired R244 evidence committed in e2240c23. Original raw bytes preserved in Git; current690 reference and initialR245 core protected. Fresh UID/inode/all-alias proof before deleting only these duplicate files.'))
config=dict(root=root,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha(q/'payload.tgz'),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
t=replace((old/'remote-launch.py').read_text(encoding='utf-8'))
referencePath="base/'captured-sequence-b583-20261111/candidate/Lokad.Lython.dll'"
t=t.replace("source=base/'constructor-binding-b574-20261111/candidate'","source=base/'captured-sequence-b583-20261111/candidate'")
start=t.index('protected={');end=t.index("with(base/'comparison-vm.lock')",start)
protected={"captured-sequence-b583-20261111/candidate/Lokad.Lython.dll":reference,"dictionary-values-b588-20261111/candidate/Lokad.Lython.dll":json.loads((parent/"diagnostic-identity.json").read_text(encoding="utf-8"))["images"]["candidate"]["Lokad.Lython.dll"]}
t=t[:start]+"protected={base/name:h for name,h in "+repr(protected)+".items()}\n"+t[end:]
t=t.replace("os.link(root/name,root/role/'Lokad.Lython.dll')","os.link("+referencePath+" if role=='reference' else root/name,root/role/'Lokad.Lython.dll')")
t=t.replace("canonicalStorage='Pre-existing independently verified persistent local source.tar; exact path/bytes/SHA recorded in files'","canonicalStorage='Exact committed canonical benchmark archive entries verified locally; current selected reference protected; withdrawn prototype originals preserved in committed canonical Git; each retirement has fresh path/UID/inode/all-alias proof'")
t=t.replace('jobs=5,ownerSeconds=210','jobs=6,ownerSeconds=210')
t=t.replace('jobs=6,ownerSeconds=210','jobs=5,ownerSeconds=180').replace('RuntimeMaxSec=210','RuntimeMaxSec=180')
ast.parse(t.replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
(q/'remote-launch.py').write_text(t,encoding='utf-8',newline='\n')
print('Prepared5short source pairs/210sowner. Canonical old5f core and duplicate archives verified in committed Git; fresh idle/disk/UID/all-alias proof required before mutation.')
