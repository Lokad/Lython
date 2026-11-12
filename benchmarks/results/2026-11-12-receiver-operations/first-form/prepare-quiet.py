from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile

r=Path(__file__).resolve().parent;q=r/'b601';q.mkdir();old=r.parent/'native-proof-r254/b600'
sha=lambda b:hashlib.sha256(b).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n).read_bytes())==h,n
for phase in ['warm-sync','warm-async','compile']:
    j=json.loads((r/phase/'measurements.json').read_text(encoding='utf-8-sig'));assert j['allGoldensPassed']and j['allBalancesZero']and j['ownedFixturesRemoved']
name='receiver-ops-b601-20261112';unit='lython-b601-receiver-ops-20261112'
jobs=[['scripts.file-search.guarded-helper.large','warm'],['scripts.file-search.guarded-helper.large','warm-async'],['scripts.file-search.large','warm'],['scripts.file-search.large','warm-async'],['scripts.text-summary-helpers.nonlocal-counter.large','warm'],['scripts.text-summary-helpers.nonlocal-counter.large','warm-async'],['scripts.report-render.large','warm'],['scripts.csv-validated-records.large','warm-async'],['scripts.file-search.guarded-helper.large','compile']]
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'))
identity.update(sourceRevision='R256 shared canonical receiver/truth services versus qualified2ec9d67f. One selector/shape/invocation across native bodies and ordinary prefixes; unary not shares existing conditional truth eligibility. No new owner/cache/dispatch framework. Async replacement count protocol fix with old/new validation before callback. Actual11/10:1650internal/631public per target pass; seven complete scripts bothAPIs/separatecompile goldens/balances/cleanup. Guarded search allocation−71.28%Run/−71.19%Async; existing controls/compile flat. Desktop guardedelapsed improves but reportRun+8.30..+15.23%, CSVAsync+6.06..+9.57%, guardedcompile+9.83..+11.56% remain adverse. One9-job group explicitly covers those plus native search/captured text both APIs. No CPU claim before qualification.',coreSHA256={k:meta['images']['before'if k.startswith('reference')else'candidate']['Lokad.Lython.dll']for k in ['reference','reference-control','candidate','candidate-control']},catalogSHA256=sha((r/'catalog.json').read_bytes()))
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps({'beforeRevision':'2ec9d67f','baselineQualification':'43119174279Windows/Ubuntu actual11/10stress/packageGREENcached; production unchanged d4bdd605','sourceSHA256':meta['sourceHashes'],'changedProductionFiles':meta['changedProductionFiles'],'candidateQualification':'4562focused actual11/10 checks and seven complete scripts bothAPIs/separatecompile; no full prototype delivery.','prototypeUncommitted':True,'selectedJobs':jobs,'limits':'Targeted9-job source pair, not new CPython or universal CPU/latency qualification.'},indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'images/candidate/Lokad.Lython.dll',q/'current-core.dll')
for filename in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
    s=(old/filename).read_text(encoding='utf-8').replace('native-proof-b600-20261112',name).replace('lython-b600-native-proof-20261112',unit).replace('native-proof-b600-launch.py','receiver-ops-b601-launch.py').replace('lython-b600-fixtures','lython-b601-fixtures').replace('B600','B601')
    if filename=='collect.py':
        a=s.index('jobs=');b=s.index('\nenv =',a)
        s=s[:a]+'jobs='+repr(jobs)+'\nassert len(jobs)==9 and all(case in cases for case,lane in jobs)\n'+s[b:]
        s=s.replace("time.monotonic()-started < 295, 'Reserve35seconds", "time.monotonic()-started < 265, 'Reserve35seconds")
        a=s.index("purpose='");b=s.index("',\n                  warmupSeconds",a)
        s=s[:a]+"purpose='R256 shared canonical receiver/truth source pair versus qualified2ec9d67f. Guarded/native search and captured text bothAPIs, adverse reportRun/CSVAsync and guardedcompile. Fixed worker/adapter, RAM I/O/fullvalidation/prior-resultcleanup included. Original desktop adversity retained; no CPython or allocation-as-CPU inference"+s[b:]
    if filename=='remote-launch.py':s=s.replace('RuntimeMaxSec=390','RuntimeMaxSec=300').replace('jobs=12,ownerSeconds=390','jobs=9,ownerSeconds=300')
    if filename=='launch.py':s=s.replace('seconds=450','seconds=360').replace('ownerSeconds=390','ownerSeconds=300')
    if filename=='archive-owned.py':s=s.replace("len(raw['jobs'])==12","len(raw['jobs'])==9").replace('complete=True,jobs=10','complete=True,jobs=9')
    if filename=='retrieve.py':s=s.replace("['jobs']==10", "['jobs']==9").replace("['elapsedSeconds']<330","['elapsedSeconds']<300")
    if filename=='summarize.py':s=s.replace("len(raw['jobs'])==10","len(raw['jobs'])==9").replace('complete=True,jobs=10','complete=True,jobs=9')
    (q/filename).write_text(s,encoding='utf-8',newline='\n')
    if filename.endswith('.py')and filename!='remote-launch.py':ast.parse(s)
with tarfile.open(q/'payload.tgz','w:gz')as a:
    for n in ['current-core.dll','representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:a.add(q/n,arcname=n)
with tarfile.open(q/'payload.tgz')as a:expanded=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())
retire=[]
canonical=Path('benchmarks/results/2026-11-12-native-proof/first-form/observations-partial.tgz');b=canonical.read_bytes();assert b==subprocess.check_output(['git','show','HEAD:'+canonical.as_posix()])
for remote,entry,kind in [('native-proof-b600-20261112/candidate/Lokad.Lython.dll','current-core.dll','all-aliases'),('native-proof-b600-20261112/observations-partial.tgz',None,'exact'),('native-proof-b600-20261112/representative.json','representative.json','exact')]:
    if entry:
        with tarfile.open(canonical)as a:data=a.extractfile(entry).read()
    else:data=b
    assert sha(data)not in identity['coreSHA256'].values()
    spec=dict(kind=kind,knownPath=remote,bytes=len(data),sha256=sha(data),canonicalStorage=canonical.as_posix(),reason='Withdrawn R254 first prototype or terminal duplicates. Exact committed canonical source/core/adverse/failure evidence verified; fresh idle/path/UID/inode/all-alias proof required. Current c982reference and new R256 candidate protected.')
    if entry:spec.update(canonicalEntry=entry,canonicalArchiveSHA256=sha(b))
    retire.append(spec)
config=dict(root='/home/lokad/lython-bench-checks/'+name,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha((q/'payload.tgz').read_bytes()),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
assert '< 265'in(q/'collect.py').read_text(encoding='utf-8')
assert 'complete=True,jobs=9'in(q/'archive-owned.py').read_text(encoding='utf-8')
assert "['jobs']==9"in(q/'retrieve.py').read_text(encoding='utf-8')and"['elapsedSeconds']<300"in(q/'retrieve.py').read_text(encoding='utf-8')
assert 'seconds=360'in(q/'launch.py').read_text(encoding='utf-8')
assert 'RuntimeMaxSec=300'in(q/'remote-launch.py').read_text(encoding='utf-8')
print('9 targeted short jobs prepared:300sowner,265s last-start cutoff,35s cleanup headroom,360sobservation. Matrix/archive/retrieval/summary counts checked together. Fresh inventory required.')
