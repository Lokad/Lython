from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).parent;q=r/'b593';q.mkdir();old=r.parent.parent/'native-loop-admission-r247/b592'
sha=lambda b:hashlib.sha256(b).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n).read_bytes())==h,n
name='native-iteration-storage-b593-20261111';unit='lython-b593-native-iteration-storage-20261111'
jobs=[['scripts.text-summary-helpers.nonlocal-counter.large','warm'],['scripts.text-summary-helpers.nonlocal-counter.large','warm-async'],['scripts.text-summary-helpers.large','warm'],['scripts.text-summary-helpers.large','warm-async'],['scripts.file-search.large','warm'],['scripts.csv-validated-records.large','warm'],['scripts.report-render.large','warm-async']]
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'))
identity.update(sourceRevision='R249 refined unified native iteration storage/private return handoff versus fully qualified8f8628a4/458be840. Per-line carrier/reservation and old positions removed; private metadata stays at return roots, ordinary value recursion unchanged. Larger fixed48B/slot estimate explicit. Initial9178focused+8Python goldens, refined9178focused pass. Initial desktop allocation−26..−29%text, flat compile; sync search/CSV/report adversity and API conflicts preserved. No CPU claim before bounded quiet comparison.',coreSHA256={k:meta['images']['before'if k.startswith('reference')else'candidate']['Lokad.Lython.dll']for k in ['reference','reference-control','candidate','candidate-control']},catalogSHA256=sha((r/'catalog.json').read_bytes()))
(q/'identity.json').write_text(json.dumps(identity,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps(dict(beforeRevision='8f8628a4',baselineQualification='458be840/43087005596fullbothplatformsactual11/10stress/packageGREENcached',sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],candidateQualification='9178focused actual11/10,8unchangedPython goldens; no full prototype delivery',prototypeUncommitted=True),indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'images/candidate/Lokad.Lython.dll',q/'current-core.dll')
for filename in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=(old/filename).read_text(encoding='utf-8')
 t=t.replace('native-loop-scope-b592-20261111',name).replace('lython-b592-native-loop-scope-20261111',unit).replace('native-loop-scope-b592-launch.py','native-iteration-storage-b593-launch.py').replace('lython-b592-fixtures','lython-b593-fixtures').replace('B592','B593')
 t=t.replace('len(jobs)==5','len(jobs)==7').replace("len(raw['jobs'])==5","len(raw['jobs'])==7").replace("['jobs']==5","['jobs']==7").replace('jobs=5','jobs=7').replace("'jobs':5","'jobs':7")
 t=t.replace('RuntimeMaxSec=180','RuntimeMaxSec=240').replace('ownerSeconds=180','ownerSeconds=240').replace('seconds=240','seconds=300').replace("['elapsedSeconds']<180","['elapsedSeconds']<240")
 if filename=='collect.py':
  a=t.index('jobs=');b=t.index('\nenv = ',a);t=t[:a]+'jobs='+repr(jobs)+'\nassert len(jobs)==7 and all(case in cases for case,lane in jobs)\n'+t[b:]
  a=t.index("purpose='");b=t.index("',\n                  warmupSeconds",a)
  t=t[:a]+"purpose='R249 refined unified native iteration storage. Captured/ordinary text both APIs plus search/CSV and reportAsync controls. Initial allocation reduction and adverse local elapsed results retained. Existing warm boundary with mediated RAM I/O/checks/prior-result cleanup; no CPython ratio"+t[b:]
 (q/filename).write_text(t,encoding='utf-8',newline='\n')
 if filename.endswith('.py')and filename!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as archive:
 for filename in ['current-core.dll','representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:archive.add(q/filename,arcname=filename)
with tarfile.open(q/'payload.tgz')as archive:expanded=sum(((m.size+4095)//4096)*4096 for m in archive.getmembers())
retire=[]
def canonical(path):
 p=Path(path);b=p.read_bytes();assert b==subprocess.check_output(['git','show','HEAD:'+p.as_posix()]);return p,b
p,b=canonical('benchmarks/results/2026-11-11-native-loop-admission/quiet/observations.tgz')
with tarfile.open(p)as archive:core=archive.extractfile('current-core.dll').read()
retire.append(dict(kind='all-aliases',knownPath='native-loop-scope-b592-20261111/candidate/Lokad.Lython.dll',bytes=len(core),sha256=sha(core),canonicalStorage=p.as_posix(),canonicalArchiveSHA256=sha(b),canonicalEntry='current-core.dll',reason='Withdrawn R247 image, exact committed raw core/source/evidence preserved in9f400b71. Fresh idle/path/UID/inode/all-alias proof; current8f862 reference protected.'))
retire.append(dict(kind='exact',knownPath='native-loop-scope-b592-20261111/observations.tgz',bytes=len(b),sha256=sha(b),canonicalStorage=p.as_posix(),reason='Terminal duplicate raw archive, exact committed Git bytes verified; retain canonical all adverse evidence.'))
p,b=canonical('benchmarks/results/2026-11-11-native-loop-admission/quiet/representative.json')
retire.append(dict(kind='exact',knownPath='native-loop-scope-b592-20261111/representative.json',bytes=len(b),sha256=sha(b),canonicalStorage=p.as_posix(),reason='Unused terminal duplicate catalog; exact committed bytes, fresh single-alias proof. New collection uploads its own catalog.'))
p,b=canonical('benchmarks/results/2026-11-11-current-storage-costs/observations.tgz')
with tarfile.open(p)as archive:catalog=archive.extractfile('catalog.json').read()
retire.append(dict(kind='exact',knownPath='current-value-profile-b591-20261111/catalog.json',bytes=len(catalog),sha256=sha(catalog),canonicalStorage=p.as_posix(),canonicalArchiveSHA256=sha(b),canonicalEntry='catalog.json',reason='Unused terminal duplicate profile catalog, canonical exact committed archive entry verified. Fresh idle/path/UID/inode/single-alias proof.'))
config=dict(root='/home/lokad/lython-bench-checks/'+name,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha((q/'payload.tgz').read_bytes()),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
assert "len(raw['jobs'])==7"in(q/'archive-owned.py').read_text(encoding='utf-8')
assert 'native-iteration-storage-b593-launch.py'in(q/'launch.py').read_text(encoding='utf-8')
print('Seven short jobs prepared,240sowner/300sobservation bound. No VM mutation; fresh inventory required. Committed duplicate/core evidence verified, current8f862 reference protected.')
