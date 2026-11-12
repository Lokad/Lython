from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b611';q.mkdir(exist_ok=False);old=r.parent/'text-output-batches-r263/refined/b608'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
assert all(sha(Path(n))==h for n,h in meta['sourceHashes'].items())
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'));identity['coreSHA256']={k:meta['beforeCoreSHA256']if k.startswith('reference')else meta['candidateCoreSHA256']for k in ['reference','reference-control','candidate','candidate-control']}
identity['sourceRevision']='UncommittedR265 sharedpositionalbinding/keyworddefaultcompletion;628focusedactual11/10 and5whole scriptsbothAPIs/separatecompile goldens/balancespass. Threeproductionfiles versusfullyqualified983bab96/796c3218 GREEN43166350709cached. Allocationflat; desktopnativeRun+11..18%, guardedAsync+7..13%, CSVAsync+2..3%, reportRun+9..24% preserved withallcontrols. NoCPUgainclaim beforeboundedcomparison; no externalbaseline.'
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'candidate/Lokad.Lython.dll',q/'current-core.dll');identity['catalogSHA256']=sha(q/'representative.json')
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps(dict(sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],focusedChecks=meta['focusedChecks'],uncommittedPrototype=True),indent=2)+'\n',encoding='utf-8')
jobs=[['scripts.file-search.guarded-helper.large','warm'],['scripts.file-search.guarded-helper.large','warm-async'],['scripts.file-search.large','warm'],['scripts.csv-validated-records.large','warm-async'],['scripts.report-render.large','warm'],['scripts.text-summary-helpers.nonlocal-counter.large','warm']]
for name in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=(old/name).read_text(encoding='utf-8').replace('text-batches-b608-20261112','positional-binding-b611-20261112').replace('lython-b608-text-batches-20261112','lython-b611-positional-binding-20261112').replace('text-batches-b608-launch.py','positional-binding-b611-launch.py').replace('lython-b608-fixtures','lython-b611-fixtures').replace('B608','B611')
 if name=='collect.py':
  a=t.index('jobs=');b=t.index('\nassert len(jobs)',a);t=t[:a]+'jobs='+repr(jobs)+t[b:]
  a=t.index("purpose='");b=t.index("',\n                  warmupSeconds",a);t=t[:a]+"purpose='R265 ordinarypositional binding inclkeywordonlydefaults againstfullyqualified983bab96/796c. GuardedsearchbothAPIs,nativesearch/CSV/report/capturedcontrols. Desktopadversity preserved, noCPUforecast. Sameworker/RAMIO/fullvalidation/priorresultcleanup"+t[b:]
 if name=='remote-launch.py':
  t=t.replace("source=base/'cell-authority-b606-20261112/candidate'","source=base/'text-batches-b608-20261112/candidate'")
 if name=='summarize.py':t=t.replace('RefinedR262/R263','R265')
 (q/name).write_text(t,encoding='utf-8',newline='\n')
 if name.endswith('.py')and name!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as t:
 for name in ['representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh','current-core.dll']:t.add(q/name,arcname=name)
with tarfile.open(q/'payload.tgz')as t:expanded=sum(((m.size+4095)//4096)*4096 for m in t.getmembers())
retire=[]
archive=Path('benchmarks/results/2026-11-12-cell-write-authority/b606/observations.tgz');assert archive.read_bytes()==subprocess.check_output(['git','show','HEAD:'+archive.as_posix()])
with tarfile.open(archive)as t:b=t.extractfile('current-core.dll').read()
retire.append(dict(kind='all-aliases',knownPath='cell-authority-b606-20261112/candidate/Lokad.Lython.dll',bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),canonicalStorage=archive.as_posix(),canonicalEntry='current-core.dll',canonicalArchiveSHA256=sha(archive),reason='Supersededqualifiedf4/09ad afternewb2/796c fullGREEN43166350709. Exactcommittedsource/core/evidence preserved; no activeusers. Currentb2andnewprototype protected. Freshidle/UID/path/inode/allaliasesproof, unchangedfloors.'))
archive=Path('benchmarks/results/2026-11-12-ordinary-call-source-gate/observations.tgz');assert archive.read_bytes()==subprocess.check_output(['git','show','HEAD:'+archive.as_posix()])
for member in [None,'catalog.json']:
 with tarfile.open(archive)as t:b=archive.read_bytes()if member is None else t.extractfile(member).read()
 spec=dict(kind='exact',knownPath='call-boundary-profile-b610-20261112/'+(member or 'observations.tgz'),bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),canonicalStorage=archive.as_posix(),reason='Exactterminalduplicate only, committed0964e84c canonicalGitSHA verified. Allmaps/unknowns/currentcode preserved; freshUID/path/inode/allaliasesproof and unchangedfloors.')
 if member:spec.update(canonicalEntry=member,canonicalArchiveSHA256=sha(archive))
 retire.append(spec)
config=dict(root='/home/lokad/lython-bench-checks/positional-binding-b611-20261112',unit='lython-b611-positional-binding-20261112',payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha(q/'payload.tgz'),requiredFreeBytes=expanded+252*1024*1024,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
assert "source=base/'text-batches-b608-20261112/candidate'"in(q/'remote-launch.py').read_text(encoding='utf-8')
print('6sourcepair jobs210sowner/175slaststart/270sobservation, qualifiedb2referenceprotected; alloriginaladversityretained.')
