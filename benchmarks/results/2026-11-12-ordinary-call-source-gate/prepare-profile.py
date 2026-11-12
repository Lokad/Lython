from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b610';q.mkdir(exist_ok=False)
old=r.parent/'current-value-cost-r246/b591';current=r.parent/'text-output-batches-r263/b609'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
identity=json.loads((current/'identity.json').read_text(encoding='utf-8'));identity['coreSHA256'].pop('candidate-control')
identity['sourceRevision']='983bab96/productionunchanged796c3218 exact frozenb2cbfdb6. R264 one6s currentguarded-searchRun sourcegate: nativeproof absent, alreadydeferredcontext/sharedbacking. Attribute remainingbinding/work/dispatch vs usefulfile/string work before selecting completecall/frame redesign. No elapsedcomparison/removablepercentage.'
shutil.copyfile(r/'catalog.json',q/'catalog.json');shutil.copyfile(current/'source-identity.json',q/'source-identity.json')
identity['catalogSHA256']=sha(q/'catalog.json');(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
name='call-boundary-profile-b610-20261112';unit='lython-b610-call-boundary-profile-20261112'
for filename in ['ops-profile.py','analyze-cpu-guest.py','archive.py','run.sh','launch.py','retrieve-original.py','remote-launch.py']:
 t=(old/filename).read_text(encoding='utf-8').replace('current-value-profile-b591-20261111',name).replace('lython-b591-current-value-profile-20261111',unit).replace('lython-b591-fixtures','lython-b610-fixtures').replace('current-value-profile-b591-launch.py','call-boundary-profile-b610-launch.py').replace('B591','B610')
 if filename=='ops-profile.py':
  a=t.index('for name, case_id, lane in [');b=t.index(':\n',a)
  t=t[:a]+'for name, case_id, lane in [("guarded-search", "scripts.file-search.guarded-helper.large", "warm")]'+t[b:]
  t=t.replace('TryExecuteDictionaryLoop EvaluateNativeReturn GuardNativeReturn TryReadRecordValue','BeginFunctionCall BeginBoundCall BindFunctionArgumentsInto ExecuteInstructionPrefix')
  t=t.replace('total_capture_seconds > 16','total_capture_seconds > 8')
 if filename=='run.sh':t=t.replace('text-warm report-async','guarded-search')
 if filename=='archive.py':
  t=t.replace("['text-warm','report-async']","['guarded-search']").replace('seconds<16','seconds<8')
 if filename=='retrieve-original.py':t=t.replace("['capturedSeconds']<16","['capturedSeconds']<8")
 if filename=='remote-launch.py':
  t=t.replace("source=base/'dictionary-checkpoint-b590-20261111/candidate'","source=base/'text-batches-b608-20261112/candidate'")
  t=t.replace('sampledSeconds=16','sampledSeconds=8')
 (q/filename).write_text(t,encoding='utf-8',newline='\n')
 if filename.endswith('.py')and filename!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as a:
 for filename in ['catalog.json','identity.json','source-identity.json','ops-profile.py','analyze-cpu-guest.py','archive.py','run.sh']:a.add(q/filename,arcname=filename)
with tarfile.open(q/'payload.tgz')as a:expanded=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())
archive=Path('benchmarks/results/2026-11-12-output-cpython-checkpoint/observations.tgz')
assert archive.read_bytes()==subprocess.check_output(['git','show','HEAD:'+archive.as_posix()])
retire=[]
for member in [None,'representative.json','confirmation/confirmation.json']:
 with tarfile.open(archive)as t:b=archive.read_bytes()if member is None else t.extractfile(member).read()
 spec=dict(kind='exact',knownPath='output-cpython-b609-20261112/'+(member or 'observations.tgz'),bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),canonicalStorage=archive.as_posix(),reason='Only exact terminalB609duplicates, committed10a9f09f canonicalGitSHA verified; selectedb2cbprotected. Original/adverseconditions preserved. Freshidle/UID/path/inode/allaliasesproof, unchangedfloors.')
 if member:spec.update(canonicalEntry=member,canonicalArchiveSHA256=sha(archive))
 retire.append(spec)
config=dict(root='/home/lokad/lython-bench-checks/'+name,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha(q/'payload.tgz'),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
assert "source=base/'text-batches-b608-20261112/candidate'"in(q/'remote-launch.py').read_text(encoding='utf-8')
print('One6s guardedsearch source-selection capture, existing120sowner; no elapsedcomparison/newframework.')
