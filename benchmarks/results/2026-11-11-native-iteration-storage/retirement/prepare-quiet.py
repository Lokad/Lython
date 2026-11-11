from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).parent;q=r/'b594';q.mkdir();old=r.parent.parent/'native-loop-admission-r247/b592'
sha=lambda b:hashlib.sha256(b).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n).read_bytes())==h,n
name='native-iteration-retirement-b594-20261111';unit='lython-b594-native-iteration-retirement-20261111'
jobs=[['scripts.text-summary-helpers.nonlocal-counter.large','warm'],['scripts.text-summary-helpers.large','warm'],['scripts.file-search.large','warm'],['scripts.csv-validated-records.large','warm'],['scripts.report-render.large','warm-async']]
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'))
identity.update(sourceRevision='R249 retirement correction versus fully qualified8f8628a4/458be840. Per-line carrier/reservation and old positions removed; private metadata stays at return roots. Only live reference descriptors clear on retirement; numeric list/tuple positions reset at acquisition. No new tracking. Larger fixed48B/slot estimate explicit. 9178focused actual11/10 pass,8unchangedPython goldens. Allocation−26..−29%text/flat compile. Earlier B593 ordinaryRun CPU improves4..5%, reportAsync CPU+4.26..+6.38% and all local adversity retained. No current-source CPU or neutrality claim before bounded confirmation.',coreSHA256={k:meta['images']['before'if k.startswith('reference')else'candidate']['Lokad.Lython.dll']for k in ['reference','reference-control','candidate','candidate-control']},catalogSHA256=sha((r/'catalog.json').read_bytes()))
(q/'identity.json').write_text(json.dumps(identity,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps(dict(beforeRevision='8f8628a4',baselineQualification='458be840/43087005596fullbothplatformsactual11/10stress/packageGREENcached',sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],candidateQualification='9178focused actual11/10,8unchangedPython goldens; no full prototype delivery',prototypeUncommitted=True),indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'images/candidate/Lokad.Lython.dll',q/'current-core.dll')
for filename in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=(old/filename).read_text(encoding='utf-8')
 t=t.replace('native-loop-scope-b592-20261111',name).replace('lython-b592-native-loop-scope-20261111',unit).replace('native-loop-scope-b592-launch.py','native-iteration-retirement-b594-launch.py').replace('lython-b592-fixtures','lython-b594-fixtures').replace('B592','B594')
 if filename=='collect.py':
  a=t.index('jobs=');b=t.index('\nenv = ',a);t=t[:a]+'jobs='+repr(jobs)+'\nassert len(jobs)==5 and all(case in cases for case,lane in jobs)\n'+t[b:]
  a=t.index("purpose='");b=t.index("',\n                  warmupSeconds",a)
  t=t[:a]+"purpose='R249 final retirement correction. Captured/ordinary Run plus search/CSV and reportAsync controls, against exact qualified8f862 baseline. B593 report adversity and all local forms retained. Existing warm boundary with mediated RAM I/O/checks/prior-result cleanup; no CPython ratio or async text CPU claim"+t[b:]
 (q/filename).write_text(t,encoding='utf-8',newline='\n')
 if filename.endswith('.py')and filename!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as archive:
 for filename in ['current-core.dll','representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:archive.add(q/filename,arcname=filename)
with tarfile.open(q/'payload.tgz')as archive:expanded=sum(((m.size+4095)//4096)*4096 for m in archive.getmembers())
retire=[]
def canonical(path):
 p=Path(path);b=p.read_bytes();assert b==subprocess.check_output(['git','show','HEAD:'+p.as_posix()]);return p,b
p,b=canonical('benchmarks/results/2026-11-11-native-iteration-storage/refined/quiet/observations.tgz')
with tarfile.open(p)as archive:core=archive.extractfile('current-core.dll').read()
retire.append(dict(kind='all-aliases',knownPath='native-iteration-storage-b593-20261111/candidate/Lokad.Lython.dll',bytes=len(core),sha256=sha(core),canonicalStorage=p.as_posix(),canonicalArchiveSHA256=sha(b),canonicalEntry='current-core.dll',reason='Superseded prototype image, exact committed raw core/source/evidence preserved inccf6cba9. Fresh idle/path/UID/inode/all-alias proof; current8f862 reference protected.'))
retire.append(dict(kind='exact',knownPath='native-iteration-storage-b593-20261111/observations.tgz',bytes=len(b),sha256=sha(b),canonicalStorage=p.as_posix(),reason='Terminal duplicate raw archive, exact committed Git bytes verified; retain all canonical adverse evidence.'))
p,b=canonical('benchmarks/results/2026-11-11-native-iteration-storage/refined/quiet/representative.json')
retire.append(dict(kind='exact',knownPath='native-iteration-storage-b593-20261111/representative.json',bytes=len(b),sha256=sha(b),canonicalStorage=p.as_posix(),reason='Unused terminal duplicate catalog; exact committed bytes, fresh single-alias proof. New collection uploads its own catalog.'))
p,b=canonical('benchmarks/results/2026-11-11-current-storage-costs/observations.tgz')
with tarfile.open(p)as archive:
 for entry in ['profiles/report-async/perf-stacks.txt','profiles/text-warm/perf-stacks.txt']:
  data=archive.extractfile(entry).read()
  retire.append(dict(kind='exact',knownPath='current-value-profile-b591-20261111/'+entry,bytes=len(data),sha256=sha(data),canonicalStorage=p.as_posix(),canonicalArchiveSHA256=sha(b),canonicalEntry=entry,reason='Unused terminal duplicate profile stack text, canonical exact committed archive entry verified. Fresh idle/path/UID/inode/single-alias proof.'))
config=dict(root='/home/lokad/lython-bench-checks/'+name,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha((q/'payload.tgz').read_bytes()),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
assert "len(raw['jobs'])==5"in(q/'archive-owned.py').read_text(encoding='utf-8')
assert 'native-iteration-retirement-b594-launch.py'in(q/'launch.py').read_text(encoding='utf-8')
print('Five short jobs prepared,180sowner/240sobservation bound. No VM mutation; fresh inventory required. Committed duplicate/core evidence verified, current8f862 reference protected.')
