from pathlib import Path
import ast,gzip,hashlib,json,shutil,subprocess,tarfile
parent=Path(__file__).parent;r=parent/'refined';q=r/'b587';q.mkdir(exist_ok=False);base=parent.parent
old=base/'native-counter-loops-r237/b576'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for name,h in meta['sourceHashes'].items():assert sha(Path(name))==h,name
root='/home/lokad/lython-bench-checks/known-call-binding-b587-20261111';unit='lython-b587-known-call-binding-20261111'
def replace(t):return t.replace('/native-counter-loops-b576-20261111','/known-call-binding-b587-20261111').replace('lython-b576-native-counter-loops-20261111',unit).replace('lython-b576-native-counter-loops',unit.removesuffix('-20261111')).replace('native-counter-loops-b576','known-call-binding-b587').replace('lython-b576-fixtures','lython-b587-fixtures')
reference=meta['images']['before']['Lokad.Lython.dll'];candidate=meta['images']['candidate']['Lokad.Lython.dll']
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'))
identity.update(sourceRevision='R244 refined prepared known-call binding, omitting unused post-completion presence writes and duplicate small-loop entry check versus fully qualified6904093e, exact frozenccdd reference on desktop/VM. 5936focused actual11/10 checks and15isolated CPython correctness goldens. Runtime allocationflat, compilation+0.38..0.54%; desktop target clocks conflict, all adverse data preserved. No CPU or neutrality claim before bounded quiet pair.',coreSHA256={role:reference if role.startswith('reference')else candidate for role in ['reference','candidate','reference-control','candidate-control']},catalogSHA256=sha(r/'catalog.json'))
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
for canonicalName,entry,known,expected in [
 ('2026-11-11-constructor-binding/preparation.tar.xz','candidate-core.dll','constructor-binding-b574-20261111/candidate/Lokad.Lython.dll','f5ae2355139a2588a3e6193c4c44332da7a2b0cf98bdcd6b1614878eebc479d2'),
 ('2026-11-11-ordinary-return-consumption/quiet/observations.tgz','current-core.dll','iteration-returns-b570-20261111/candidate/Lokad.Lython.dll','9ee7e700580399ff80d803f0a7535d2de5ff0f10d35a736370f457383be1b67f')]:
 canonical=Path('benchmarks/results')/canonicalName;data=canonical.read_bytes()
 assert data==subprocess.run(['git','show','HEAD:'+canonical.as_posix()],capture_output=True,check=True).stdout
 with tarfile.open(canonical)as archive:core=archive.extractfile(entry).read()
 assert hashlib.sha256(core).hexdigest()==expected
 retire.append(dict(kind='all-aliases',knownPath=known,bytes=len(core),sha256=expected,canonicalStorage=canonical.as_posix(),canonicalEntry=entry,canonicalArchiveSHA256=sha(canonical),reason='Obsolete15f/4aa historical image, no longer source or selected reference. Current690/reference and firstR244 prototype stay protected. Exact committed canonical archive/core verified; fresh path/UID/inode/all-alias proof before retirement.'))
config=dict(root=root,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha(q/'payload.tgz'),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
t=replace((old/'remote-launch.py').read_text(encoding='utf-8'))
referencePath="base/'captured-sequence-b583-20261111/candidate/Lokad.Lython.dll'"
t=t.replace("source=base/'constructor-binding-b574-20261111/candidate'","source=base/'captured-sequence-b583-20261111/candidate'")
start=t.index('protected={');end=t.index("with(base/'comparison-vm.lock')",start)
protected={"captured-sequence-b583-20261111/candidate/Lokad.Lython.dll":reference,"known-call-binding-b586-20261111/candidate/Lokad.Lython.dll":'00e08cfa247846f713d88f4e07c095bc7822d5e7234eb94be18a11c1db7628df'}
t=t[:start]+"protected={base/name:h for name,h in "+repr(protected)+".items()}\n"+t[end:]
t=t.replace("os.link(root/name,root/role/'Lokad.Lython.dll')","os.link("+referencePath+" if role=='reference' else root/name,root/role/'Lokad.Lython.dll')")
t=t.replace("canonicalStorage='Pre-existing independently verified persistent local source.tar; exact path/bytes/SHA recorded in files'","canonicalStorage='Exact committed canonical benchmark archive entries verified locally; current selected roles and initial adverse prototype protected; each retirement has fresh path/UID/inode/all-alias proof'")
ast.parse(t.replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
(q/'remote-launch.py').write_text(t,encoding='utf-8',newline='\n')
print('Prepared5short source pairs/210sowner. Canonical old5f core and duplicate archives verified in committed Git; fresh idle/disk/UID/all-alias proof required before mutation.')
