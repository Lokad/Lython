from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b595';q.mkdir(exist_ok=True);old=r.parent/'native-iteration-storage-r249/retirement/b594'
sha=lambda b:hashlib.sha256(b).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n).read_bytes())==h,n
name='word-materialization-b595-20261111';unit='lython-b595-word-materialization-20261111'
jobs=[['scripts.text-summary-helpers.nonlocal-counter.large','warm'],['scripts.text-summary-helpers.nonlocal-counter.large','warm-async'],['scripts.text-summary-helpers.large','warm'],['scripts.text-summary-helpers.large','warm-async'],['scripts.file-search.large','warm'],['scripts.file-search.large','warm-async'],['scripts.csv-validated-records.large','warm'],['scripts.csv-validated-records.large','warm-async'],['scripts.json-summary-helpers.large','warm'],['scripts.report-render.large','warm-async']]
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'))
identity.update(sourceRevision='R250 private native mapped-word probing versus fully qualified8f8628a4. Ordinary object call/return ABI and cased iterator retained. Duplicate short words probe UTF8 bytes; insertions/final bindings materialize prepaid immutable values. One shared span probe/counter update, no cache/ownership/return-transport subsystem. 1642internal/3807public per actual11/10 plus69corrected fixtures per target,5independentPython references. Initial local text allocation reduction20–22%; noisy desktop clocks not CPU evidence. Current conservative eager funding unchanged. No final delivery or CPU claim before bounded comparison.',coreSHA256={k:meta['images']['before'if k.startswith('reference')else'candidate']['Lokad.Lython.dll']for k in ['reference','reference-control','candidate','candidate-control']},catalogSHA256=sha((r/'catalog.json').read_bytes()))
(q/'identity.json').write_text(json.dumps(identity,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps(dict(beforeRevision='8f8628a4',baselineQualification='458be840/43087005596Windows/Ubuntu actual11/10stress/packageGREENcached',sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],candidateQualification='11036focused check invocations actual11/10,5Python references. No full prototype delivery.',prototypeUncommitted=True),indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'images/candidate/Lokad.Lython.dll',q/'current-core.dll')
for filename in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=(old/filename).read_text(encoding='utf-8')
 t=t.replace('native-iteration-retirement-b594-20261111',name).replace('lython-b594-native-iteration-retirement-20261111',unit).replace('native-iteration-retirement-b594-launch.py','word-materialization-b595-launch.py').replace('lython-b594-fixtures','lython-b595-fixtures').replace('B594','B595')
 t=t.replace('ownerSeconds=180','ownerSeconds=330').replace('seconds=240','seconds=390').replace('ownerSeconds=180','ownerSeconds=330').replace('RuntimeMaxSec=180','RuntimeMaxSec=330').replace("<180","<330")
 t=t.replace("len(raw['jobs'])==5","len(raw['jobs'])==10").replace("['jobs']==5","['jobs']==10").replace('jobs=5','jobs=10')
 if filename=='collect.py':
  a=t.index('jobs=');b=t.index('\nenv = ',a);t=t[:a]+'jobs='+repr(jobs)+'\nassert len(jobs)==10 and all(case in cases for case,lane in jobs)\n'+t[b:]
  a=t.index("purpose='");b=t.index("',\n                  warmupSeconds",a)
  t=t[:a]+"purpose='R250 private word materialization/probing. Both complete text scripts through both APIs; search/CSV/JSON Run and reportAsync controls against exact qualified8f862 baseline. Fixed common worker/adapter, mediated RAM I/O/checks/prior-result cleanup included; no CPython comparison or general allocation-to-CPU attribution"+t[b:]
  t=t.replace("< 170, 'Reserve40seconds","< 295, 'Reserve35seconds")
 (q/filename).write_text(t,encoding='utf-8',newline='\n')
 if filename.endswith('.py')and filename!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as archive:
 for filename in ['current-core.dll','representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:archive.add(q/filename,arcname=filename)
with tarfile.open(q/'payload.tgz')as archive:expanded=sum(((m.size+4095)//4096)*4096 for m in archive.getmembers())
retire=[]
def canonical(path):
 p=Path(path);b=p.read_bytes();assert b==subprocess.check_output(['git','show','HEAD:'+p.as_posix()]);return p,b
p,b=canonical('benchmarks/results/2026-11-11-native-iteration-storage/retirement/quiet/observations.tgz')
with tarfile.open(p)as archive:core=archive.extractfile('current-core.dll').read()
retire.append(dict(kind='all-aliases',knownPath='native-iteration-retirement-b594-20261111/candidate/Lokad.Lython.dll',bytes=len(core),sha256=sha(core),canonicalStorage=p.as_posix(),canonicalArchiveSHA256=sha(b),canonicalEntry='current-core.dll',reason='Withdrawn prototype image. Exact adverse observations/core/source committed ina2abcac1; fresh idle/path/UID/inode/all-alias proof required. Selected8f862 reference protected.'))
retire.append(dict(kind='exact',knownPath='native-iteration-retirement-b594-20261111/observations.tgz',bytes=len(b),sha256=sha(b),canonicalStorage=p.as_posix(),reason='Unused terminal duplicate archive; exact committed Git bytes verified locally.'))
p,b=canonical('benchmarks/results/2026-11-11-native-iteration-storage/retirement/quiet/representative.json')
retire.append(dict(kind='exact',knownPath='native-iteration-retirement-b594-20261111/representative.json',bytes=len(b),sha256=sha(b),canonicalStorage=p.as_posix(),reason='Unused terminal duplicate catalog. New collection supplies its own catalog.'))
p,b=canonical('benchmarks/results/2026-11-11-dictionary-cpython-checkpoint/observations.tgz')
with tarfile.open(p)as archive:catalog=archive.extractfile('representative.json').read()
retire.append(dict(kind='exact',knownPath='dictionary-checkpoint-b590-20261111/representative.json',bytes=len(catalog),sha256=sha(catalog),canonicalStorage=p.as_posix(),canonicalArchiveSHA256=sha(b),canonicalEntry='representative.json',reason='Unused terminal duplicate catalog; exact committed archive entry. Reference image and auxiliary source remain protected.'))
config=dict(root='/home/lokad/lython-bench-checks/'+name,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha((q/'payload.tgz').read_bytes()),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
assert "len(raw['jobs'])==10"in(q/'archive-owned.py').read_text(encoding='utf-8')
assert 'word-materialization-b595-launch.py'in(q/'launch.py').read_text(encoding='utf-8')
assert 'RuntimeMaxSec=330'in(q/'remote-launch.py').read_text(encoding='utf-8')
print('Ten existing short jobs prepared,330sowner/390sobservation bound. Includes adverse async search/CSV controls. No VM mutation; fresh inventory required. Canonical committed duplicate evidence verified; selected reference protected.')
