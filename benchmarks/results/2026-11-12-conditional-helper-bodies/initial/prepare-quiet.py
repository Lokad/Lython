from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b612';q.mkdir(exist_ok=False);old=r.parent/'text-output-batches-r263/refined/b608'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
assert all(sha(Path(n))==h for n,h in meta['sourceHashes'].items())
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'));identity['coreSHA256']={k:meta['beforeCoreSHA256']if k.startswith('reference')else meta['candidateCoreSHA256']for k in ['reference','reference-control','candidate','candidate-control']}
identity['sourceRevision']='UncommittedR266 complete unaryconditional helperbodies;994focusedactual11/10 and5whole scriptsbothAPIs/separatecompile goldens/balancespass. Codefacts confirmguardedhelperNativeBodytrue. Threeproductionfiles versusfullyqualified983bab96/796c3218 GREEN43166350709cached. Warmallocationflat, compileguarded+1.10%. DesktopguardedRun-11..-2%(control9.48), guardedAsync-.75..+4.81%(control5.31), nativeAsync+3..6%, CSVAsync+1.7..5.6%, reportCompile+2.93..4.77% preserved. NoCPUgainclaim beforeboundedcomparison; no externalbaseline.'
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'candidate/Lokad.Lython.dll',q/'current-core.dll');identity['catalogSHA256']=sha(q/'representative.json')
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps(dict(sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],focusedChecks=meta['focusedChecks'],uncommittedPrototype=True),indent=2)+'\n',encoding='utf-8')
jobs=[['scripts.file-search.guarded-helper.large','warm'],['scripts.file-search.guarded-helper.large','warm-async'],['scripts.file-search.large','warm'],['scripts.csv-validated-records.large','warm-async'],['scripts.report-render.large','warm'],['scripts.text-summary-helpers.nonlocal-counter.large','warm']]
for name in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=(old/name).read_text(encoding='utf-8').replace('text-batches-b608-20261112','conditional-helper-b612-20261112').replace('lython-b608-text-batches-20261112','lython-b612-conditional-helper-20261112').replace('text-batches-b608-launch.py','conditional-helper-b612-launch.py').replace('lython-b608-fixtures','lython-b612-fixtures').replace('B608','B612')
 if name=='collect.py':
  a=t.index('jobs=');b=t.index('\nassert len(jobs)',a);t=t[:a]+'jobs='+repr(jobs)+t[b:]
  a=t.index("purpose='");b=t.index("',\n                  warmupSeconds",a);t=t[:a]+"purpose='R266 whole unaryconditional helperbodies viaexistingnativeplan againstfullyqualified983bab96/796c. GuardedsearchbothAPIs,nativesearch/CSV/report/capturedcontrols. Desktopadversity andcompileallocation+1.10% guardedsource preserved, noCPUforecast. Sameworker/RAMIO/fullvalidation/priorresultcleanup"+t[b:]
 if name=='remote-launch.py':
  t=t.replace("source=base/'cell-authority-b606-20261112/candidate'","source=base/'text-batches-b608-20261112/candidate'")
 if name=='summarize.py':t=t.replace('RefinedR262/R263','R266')
 (q/name).write_text(t,encoding='utf-8',newline='\n')
 if name.endswith('.py')and name!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as t:
 for name in ['representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh','current-core.dll']:t.add(q/name,arcname=name)
with tarfile.open(q/'payload.tgz')as t:expanded=sum(((m.size+4095)//4096)*4096 for m in t.getmembers())
retire=[]
archive=Path('benchmarks/results/2026-11-12-positional-default-binding/b611/observations.tgz');assert archive.read_bytes()==subprocess.check_output(['git','show','HEAD:'+archive.as_posix()])
with tarfile.open(archive)as t:b=t.extractfile('current-core.dll').read()
retire.append(dict(kind='all-aliases',knownPath='positional-binding-b611-20261112/candidate/Lokad.Lython.dll',bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),canonicalStorage=archive.as_posix(),canonicalEntry='current-core.dll',canonicalArchiveSHA256=sha(archive),reason='WithdrawnR265prototype, allsource/core/rawadversity restored/preserved in committed4511d547. Currentqualifiedb2andnewR266 protected. Freshidle/UID/path/inode/allaliasesproof, unchangedfloors.'))
for member in [None,'representative.json','confirmation/confirmation.json']:
 with tarfile.open(archive)as t:b=archive.read_bytes()if member is None else t.extractfile(member).read()
 spec=dict(kind='exact',knownPath='positional-binding-b611-20261112/'+(member or 'observations.tgz'),bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),canonicalStorage=archive.as_posix(),reason='OnlyunusedterminalB611duplicate, exactcommitted4511d547 canonicalGitSHA verified, alladverse/originalfailedconditions preserved. FreshUID/path/inode/allaliasesproof, b2protected, floorsunchanged.')
 if member:spec.update(canonicalEntry=member,canonicalArchiveSHA256=sha(archive))
 retire.append(spec)
archive=Path('benchmarks/results/2026-11-12-cell-write-authority/b606/observations.tgz');assert archive.read_bytes()==subprocess.check_output(['git','show','HEAD:'+archive.as_posix()])
member='confirmation/confirmation.json'
with tarfile.open(archive)as t:b=t.extractfile(member).read()
retire.append(dict(kind='exact',knownPath='cell-authority-b606-20261112/'+member,bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),canonicalStorage=archive.as_posix(),canonicalEntry=member,canonicalArchiveSHA256=sha(archive),reason='Exactterminal historicalrawrecord duplicate only; canonicalcommittedGit/SHAverified, allfailed/adverseconditions preserved. Freshidle/UID/path/inode/allaliasesproof, unchangedfloors.'))
config=dict(root='/home/lokad/lython-bench-checks/conditional-helper-b612-20261112',unit='lython-b612-conditional-helper-20261112',payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha(q/'payload.tgz'),requiredFreeBytes=expanded+252*1024*1024,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
assert "source=base/'text-batches-b608-20261112/candidate'"in(q/'remote-launch.py').read_text(encoding='utf-8')
print('6sourcepair jobs210sowner/175slaststart/270sobservation, qualifiedb2referenceprotected; alloriginaladversityretained.')
