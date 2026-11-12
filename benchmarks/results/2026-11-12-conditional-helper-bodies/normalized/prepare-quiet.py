from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b614';q.mkdir(exist_ok=False);old=r.parent.parent/'text-output-batches-r263/refined/b608'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
assert all(sha(Path(n))==h for n,h in meta['sourceHashes'].items())
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'));identity['coreSHA256']={k:meta['beforeCoreSHA256']if k.startswith('reference')else meta['candidateCoreSHA256']for k in ['reference','reference-control','candidate','candidate-control']}
identity['sourceRevision']='UncommittedR266compiler-only negated-predicate normalization,1productionfile; allruntime sourcebytes equalqualified983bab96/796c3218 GREEN43166350709cached.996focusedactual11/10,3independentCP3.13.2goldens, admissionreentry/hostIO/cleanup and5whole scriptsbothAPIs/separatecompilepass. GuardedhelperNativeBodytrue. Rawunary enum/shape/guard/eval additions removed. Warmallocationflat, compileguarded+1.02%. DesktopguardedAsync+6..20%(control13.46), capturedRun+1.92..6.49%(control4.29), nativeAsync-22..+1.72%(control23.36), alladversity preserved. Newsource, no causalrecovery/CPUforecast/externalbaselineclaim.'
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'candidate/Lokad.Lython.dll',q/'current-core.dll');identity['catalogSHA256']=sha(q/'representative.json')
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps(dict(sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],focusedChecks=meta['focusedChecks'],uncommittedPrototype=True),indent=2)+'\n',encoding='utf-8')
jobs=[['scripts.file-search.guarded-helper.large','warm'],['scripts.file-search.guarded-helper.large','warm-async'],['scripts.file-search.large','warm'],['scripts.text-summary-helpers.nonlocal-counter.large','warm']]
for name in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=(old/name).read_text(encoding='utf-8').replace('text-batches-b608-20261112','conditional-normalized-b614-20261112').replace('lython-b608-text-batches-20261112','lython-b614-conditional-normalized-20261112').replace('text-batches-b608-launch.py','conditional-normalized-b614-launch.py').replace('lython-b608-fixtures','lython-b614-fixtures').replace('B608','B614')
 if name=='collect.py':
  a=t.index('jobs=');b=t.index('\nassert len(jobs)',a);t=t[:a]+'jobs='+repr(jobs)+t[b:]
  t=t.replace('len(jobs)==6','len(jobs)==4').replace('warmupSeconds=5, targetBatchSeconds=.15','warmupSeconds=12, targetBatchSeconds=.35').replace('end=time.monotonic()+5','end=time.monotonic()+12').replace('count*.15','count*.35').replace(' < 175',' < 265')
  a=t.index("purpose='");b=t.index("',\n                  warmupSeconds",a);t=t[:a]+"purpose='R266 whole unaryconditional helperbodies viaexistingnativeplan againstfullyqualified983bab96/796c. GuardedsearchbothAPIs,nativesearch/CSV/report/capturedcontrols. Desktopadversity andcompileallocation+1.10% guardedsource preserved, noCPUforecast. Sameworker/RAMIO/fullvalidation/priorresultcleanup"+t[b:]
 if name=='remote-launch.py':
  t=t.replace("source=base/'cell-authority-b606-20261112/candidate'","source=base/'text-batches-b608-20261112/candidate'")
  t=t.replace('jobs=6,ownerSeconds=210','jobs=4,ownerSeconds=300').replace('RuntimeMaxSec=210','RuntimeMaxSec=300')
 if name=='archive-owned.py':t=t.replace("len(raw['jobs'])==6","len(raw['jobs'])==4").replace('complete=True,jobs=6','complete=True,jobs=4')
 if name=='launch.py':t=t.replace('seconds=270','seconds=360').replace('ownerSeconds=210','ownerSeconds=300')
 if name=='retrieve.py':t=t.replace("['jobs']==6","['jobs']==4").replace("['elapsedSeconds']<210","['elapsedSeconds']<300").replace('six-job','four-job')
 if name=='summarize.py':t=t.replace("len(raw['jobs'])==6","len(raw['jobs'])==4").replace('complete=True,jobs=6','complete=True,jobs=4')
 if name=='summarize.py':t=t.replace('RefinedR262/R263','R266normalized')
 (q/name).write_text(t,encoding='utf-8',newline='\n')
 if name.endswith('.py')and name!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as t:
 for name in ['representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh','current-core.dll']:t.add(q/name,arcname=name)
with tarfile.open(q/'payload.tgz')as t:expanded=sum(((m.size+4095)//4096)*4096 for m in t.getmembers())
retire=[]
archive=Path('benchmarks/results/2026-11-12-conditional-helper-bodies/initial/b612/observations.tgz');assert archive.read_bytes()==subprocess.check_output(['git','show','HEAD:'+archive.as_posix()])
with tarfile.open(archive)as t:b=t.extractfile('current-core.dll').read()
retire.append(dict(kind='all-aliases',knownPath='conditional-helper-b612-20261112/candidate/Lokad.Lython.dll',bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),canonicalStorage=archive.as_posix(),canonicalEntry='current-core.dll',canonicalArchiveSHA256=sha(archive),reason='SupersededrawUnaryprototype, exactsource/core/initialadversity andsame-sourceconfirmation preserved in committed38745af9/354618ab. b2reference/newcompiler-onlysourceprotected. Freshidle/UID/path/inode/allaliasesproof, floorsunchanged.'))
archive=Path('benchmarks/results/2026-11-12-conditional-helper-bodies/confirmation/observations.tgz');assert archive.read_bytes()==subprocess.check_output(['git','show','HEAD:'+archive.as_posix()])
for member in [None,'representative.json','confirmation/confirmation.json']:
 with tarfile.open(archive)as t:b=archive.read_bytes()if member is None else t.extractfile(member).read()
 spec=dict(kind='exact',knownPath='conditional-confirmation-b613-20261112/'+(member or 'observations.tgz'),bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),canonicalStorage=archive.as_posix(),reason='OnlyterminalB613duplicate, exactcommitted354618ab canonicalGitSHA verified; originalallfailed/adverseconditions preserved. Freshidle/UID/path/inode/allaliasesproof, b2protected, floorsunchanged.')
 if member:spec.update(canonicalEntry=member,canonicalArchiveSHA256=sha(archive))
 retire.append(spec)
config=dict(root='/home/lokad/lython-bench-checks/conditional-normalized-b614-20261112',unit='lython-b614-conditional-normalized-20261112',payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha(q/'payload.tgz'),requiredFreeBytes=expanded+252*1024*1024,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
assert "source=base/'text-batches-b608-20261112/candidate'"in(q/'remote-launch.py').read_text(encoding='utf-8')
print('4sourcepair jobs12swarmups/350msrounds/300sowner/265slaststart/360sobservation, qualifiedb2referenceprotected; originalvariants/adversityretained.')
