from pathlib import Path
import ast,gzip,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).parent;q=r/'b586';q.mkdir(exist_ok=False);base=r.parent
old=base/'native-counter-loops-r237/b576'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for name,h in meta['sourceHashes'].items():assert sha(Path(name))==h,name
root='/home/lokad/lython-bench-checks/known-call-binding-b586-20261111';unit='lython-b586-known-call-binding-20261111'
def replace(t):return t.replace('/native-counter-loops-b576-20261111','/known-call-binding-b586-20261111').replace('lython-b576-native-counter-loops-20261111',unit).replace('lython-b576-native-counter-loops',unit.removesuffix('-20261111')).replace('native-counter-loops-b576','known-call-binding-b586').replace('lython-b576-fixtures','lython-b586-fixtures')
reference=meta['images']['before']['Lokad.Lython.dll'];candidate=meta['images']['candidate']['Lokad.Lython.dll']
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'))
identity.update(sourceRevision='R244 uncommitted prepared known-call binding versus fully qualified6904093e, exact frozenccdd reference on desktop/VM. 5920broad focused actual11/10 checks plus final4internal/15public per target with overlap,14isolated CPython correctness goldens. Runtime allocationflat, compilation+0.38..0.54%; desktop target clocks conflict, all adverse data preserved. No CPU or neutrality claim before bounded quiet pair.',coreSHA256={role:reference if role.startswith('reference')else candidate for role in ['reference','candidate','reference-control','candidate-control']},catalogSHA256=sha(r/'catalog.json'))
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps(dict(beforeRevision='6904093e',prototypeUncommitted=True,sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],beforeCore=reference,candidateCore=candidate,qualification='Qualified69043058223560GREEN cached; R244focused only, no full prototype qualification yet.'),indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'images/candidate/Lokad.Lython.dll',q/'current-core.dll')
jobs=[['scripts.file-search.large','warm'],['scripts.csv-validated-records.large','warm'],['scripts.file-search.large','warm-async'],['scripts.csv-validated-records.large','warm-async'],['scripts.text-summary-helpers.large','warm']]
t=replace((old/'collect.py').read_text(encoding='utf-8'))
start=t.index('jobs=');end=t.index('\nenv = ',start);t=t[:start]+'jobs='+repr(jobs)+'\nassert len(jobs)==5 and all(case in cases for case,lane in jobs)\n'+t[end:]
start=t.index("purpose='R237");end=t.index("',\n                  warmupSeconds",start)
t=t[:start]+"purpose='R244 bounded known-call binding versus qualified690, CSV/search both APIs and unchanged textsync control. Desktop conflict/adversity retained. Warm includes mediated RAM I/O/full checks/prior-result cleanup, excludes compile/startup/reset/protocol/final caches; no CPython ratio"+t[end:]
(q/'collect.py').write_text(t,encoding='utf-8',newline='\n');ast.parse(t)
for n in ['archive-owned.py','run.sh','retrieve.py','summarize.py','launch.py']:
 t=replace((old/n).read_text(encoding='utf-8')).replace('B576','B586')
 (q/n).write_text(t,encoding='utf-8',newline='\n')
 if n.endswith('.py'):ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as t:
 for n in ['current-core.dll','representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:t.add(q/n,arcname=n)
with tarfile.open(q/'payload.tgz')as t:expanded=sum(((m.size+4095)//4096)*4096 for m in t.getmembers())
retire=[]
canonical=Path('benchmarks/results/2026-11-11-mirrored-function-storage/candidate-net11.0.dll.gz')
data=canonical.read_bytes();assert data==subprocess.run(['git','show','HEAD:'+canonical.as_posix()],capture_output=True,check=True).stdout
core=gzip.decompress(data)
assert hashlib.sha256(core).hexdigest()=='f7025485f03de431e3cd61a52b12bc55baa9df329617737555e06617b452205e'
retire.append(dict(kind='all-aliases',knownPath='mirrored-storage-b581-20261111/candidate/Lokad.Lython.dll',bytes=len(core),sha256=hashlib.sha256(core).hexdigest(),canonicalStorage=canonical.as_posix(),canonicalArchiveSHA256=sha(canonical),reason='Obsolete5f comparison baseline; next selected reference is qualified690/ccdd. Exact decompressed committed Git core verified, all aliases must be proven idle/owned and outside current roles before retirement.'))
for canonicalName,known in [('2026-11-11-current-native-call-costs/observations.tgz','current-phases-profile-b585-20261111/observations.tgz'),('2026-11-11-captured-helper-cpython-checkpoint/observations.tgz','captured-checkpoint-b584-20261111/observations.tgz')]:
 canonical=Path('benchmarks/results')/canonicalName;data=canonical.read_bytes()
 assert data==subprocess.run(['git','show','HEAD:'+canonical.as_posix()],capture_output=True,check=True).stdout
 retire.append(dict(kind='exact',knownPath=known,bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),canonicalStorage=canonical.as_posix(),reason='Terminal historical raw archive duplicate; exact canonical committed Git bytes verified. Fresh absolute path/UID/inode/all-alias proof; preserve every original result and selected core.'))
config=dict(root=root,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha(q/'payload.tgz'),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
t=replace((old/'remote-launch.py').read_text(encoding='utf-8'))
referencePath="base/'captured-sequence-b583-20261111/candidate/Lokad.Lython.dll'"
t=t.replace("with(base/'comparison-vm.lock')","protected["+referencePath+"]="+repr(reference)+"\nwith(base/'comparison-vm.lock')")
t=t.replace("os.link(root/name,root/role/'Lokad.Lython.dll')","os.link("+referencePath+" if role=='reference' else root/name,root/role/'Lokad.Lython.dll')")
t=t.replace("canonicalStorage='Pre-existing independently verified persistent local source.tar; exact path/bytes/SHA recorded in files'","canonicalStorage='Exact committed canonical benchmark bytes independently verified locally, including decompressed core; path/bytes/SHA in each proof'")
ast.parse(t.replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
(q/'remote-launch.py').write_text(t,encoding='utf-8',newline='\n')
print('Prepared5short source pairs/210sowner. Canonical old5f core and duplicate archives verified in committed Git; fresh idle/disk/UID/all-alias proof required before mutation.')
