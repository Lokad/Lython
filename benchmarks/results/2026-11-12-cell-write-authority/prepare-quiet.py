from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path('.git/agent-notes/review-20261031/cell-write-authority-r260').resolve();q=r/'b606';q.mkdir();old=r.parent/'completed-call-lifecycle-r259/b605';sha=lambda b:hashlib.sha256(b).hexdigest();meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n).read_bytes())==h,n
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'));identity['coreSHA256']={k:meta['beforeCoreSHA256'if k.startswith('reference')else'candidateCoreSHA256']for k in ['reference','reference-control','candidate','candidate-control']};identity['catalogSHA256']=sha((r/'catalog.json').read_bytes());identity['sourceRevision']='R260 cellValue setter owns activecache+conditionalnamespace, cachedslot replacesnameprobe; necessaryResume sync retained, mirroredresumerefreshesnamespace. Two baselinelexical/generatorbugs fixed andindependentCPythonprobes match.9184focusedactual11/10pass,3whole scriptsbothAPIsgoldens/balances. Allocationflat, desktopcapturedRun-4.06..+9.10(control12.07),Async+2.86..+5.30(control2.31). Fourtarget/controljobs only, no speedclaim; preservecorrectnessfixes ifperformanceformwithdrawn.'
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8');(q/'source-identity.json').write_text(json.dumps(dict(beforeRevision=meta['beforeRevision'],qualification=meta['baselineQualification'],sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],prototypeUncommitted=True,candidateQualification='9184focused actual11/10; threecomplete scriptsbothAPIs andindependentCPythonprobes; fullcandidate deliverypending',limits='Targeted4-job sourcepair, not CPython/performanceorsemanticscampaign.'),indent=2)+'\n',encoding='utf-8');shutil.copyfile(r/'candidate/Lokad.Lython.dll',q/'current-core.dll');shutil.copyfile(r/'catalog.json',q/'representative.json')
jobs=[['scripts.text-summary-helpers.nonlocal-counter.large','warm'],['scripts.text-summary-helpers.nonlocal-counter.large','warm-async'],['scripts.text-summary-helpers.large','warm'],['scripts.report-render.large','warm']]
for n in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=(old/n).read_text(encoding='utf-8').replace('completed-calls-b605-20261112','cell-authority-b606-20261112').replace('lython-b605-completed-calls-20261112','lython-b606-cell-authority-20261112').replace('completed-calls-b605-launch.py','cell-authority-b606-launch.py').replace('lython-b605-fixtures','lython-b606-fixtures').replace('B605','B606')
 if n=='collect.py':
  a=t.index('jobs=');b=t.index('\nenv =',a);t=t[:a]+'jobs='+repr(jobs)+'\nassert len(jobs)==4 and all(case in cases for case,lane in jobs)\n'+t[b:];t=t.replace(' < 265',' < 115')
  a=t.index("purpose='");b=t.index("',\n                  warmupSeconds",a);t=t[:a]+"purpose='R260 cell-write authority versusqualified7114021e. CapturedtextbothAPIs, ordinarytext/reportRuncontrols. Allocationflat, all desktopadversityretained; no externalratioorCPUclaim beforeobservation. Two independentlyvalidated correctnessfixes standseparately"+t[b:]
 if n=='remote-launch.py':t=t.replace("source=base/'receiver-ops-b602-20261112/candidate'", "source=base/'completed-calls-b605-20261112/candidate'").replace('jobs=9,ownerSeconds=300','jobs=4,ownerSeconds=150').replace('RuntimeMaxSec=300','RuntimeMaxSec=150')
 if n=='launch.py':t=t.replace('seconds=360','seconds=210').replace('ownerSeconds=300','ownerSeconds=150')
 if n=='archive-owned.py':t=t.replace("len(raw['jobs'])==9","len(raw['jobs'])==4").replace('complete=True,jobs=9','complete=True,jobs=4')
 if n=='retrieve.py':t=t.replace("['jobs']==9","['jobs']==4").replace("['elapsedSeconds']<300","['elapsedSeconds']<150").replace('five-job','four-job')
 if n=='summarize.py':t=t.replace("len(raw['jobs'])==9","len(raw['jobs'])==4").replace('complete=True,jobs=9','complete=True,jobs=4')
 (q/n).write_text(t,encoding='utf-8',newline='\n')
 if n.endswith('.py')and n!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as a:
 for n in ['current-core.dll','representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:a.add(q/n,arcname=n)
with tarfile.open(q/'payload.tgz')as a:expanded=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())
retire=[]
p=Path('benchmarks/results/2026-11-12-receiver-operations/refined/b602/observations.tgz');b=p.read_bytes();assert b==subprocess.check_output(['git','show','HEAD:'+p.as_posix()])
with tarfile.open(p)as a:data=a.extractfile('current-core.dll').read()
assert sha(data)=='0106a7115097fee0924b322a00bdd8785411969e1a099b34b935091114a758e4'
retire.append(dict(kind='all-aliases',knownPath='receiver-ops-b602-20261112/candidate/Lokad.Lython.dll',bytes=len(data),sha256=sha(data),canonicalStorage=p.as_posix(),canonicalEntry='current-core.dll',canonicalArchiveSHA256=sha(b),reason='Superseded82ed/0106 fullyqualifiedhistoricalcore; current7114/0e715reference is fullyqualified andprotected. Exactcommittedcanonicalbytes/source/evidence; freshidle/UID/path/inode/allaliases proof; floors unchanged.'))
for remote,canonical,entry in [('completed-calls-b605-20261112/observations.tgz','benchmarks/results/2026-11-12-completed-call-lifecycle/b605/observations.tgz',None),('completed-calls-b605-20261112/representative.json','benchmarks/results/2026-11-12-completed-call-lifecycle/b605/observations.tgz','representative.json'),('current-runtime-b599-20261112/observations.tgz','benchmarks/results/2026-11-12-current-runtime-costs/observations.tgz',None)]:
 p=Path(canonical);b=p.read_bytes();assert b==subprocess.check_output(['git','show','HEAD:'+p.as_posix()])
 if entry:
  with tarfile.open(p)as a:data=a.extractfile(entry).read()
 else:data=b
 spec=dict(kind='exact',knownPath=remote,bytes=len(data),sha256=sha(data),canonicalStorage=canonical,reason='Unusedterminalduplicate; exactcommittedcanonical evidence. Freshidle/UID/path/inode/allhardlink proof, selectedreference/newcandidateprotected; no relaxedspacefloor.')
 if entry:spec.update(canonicalEntry=entry,canonicalArchiveSHA256=sha(b))
 retire.append(spec)
config=dict(root='/home/lokad/lython-bench-checks/cell-authority-b606-20261112',unit='lython-b606-cell-authority-20261112',payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha((q/'payload.tgz').read_bytes()),requiredFreeBytes=expanded+252*1024*1024,retire=retire,identity=identity);(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
for n,needle in [('collect.py','len(jobs)==4'),('collect.py',' < 115'),('archive-owned.py',"len(raw['jobs'])==4"),('retrieve.py',"['jobs']==4"),('retrieve.py',"['elapsedSeconds']<150"),('remote-launch.py','RuntimeMaxSec=150'),('launch.py','seconds=210')]:assert needle in(q/n).read_text(encoding='utf-8'),(n,needle)
print('Fourtargetedshort jobs,150sowner/115slaststart/210sobservation; freshproof beforemutation.')
