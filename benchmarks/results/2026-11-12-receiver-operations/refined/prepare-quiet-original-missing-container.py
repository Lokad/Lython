from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path('.git/agent-notes/review-20261031/receiver-operations-r256').resolve();s=r/'refined';q=s/'b602';q.mkdir();old=r/'b601'
sha=lambda b:hashlib.sha256(b).hexdigest();meta=json.loads((s/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n).read_bytes())==h,n
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'))
identity['sourceRevision']='R256 refined shared canonical operations over qualified2ec source: one shape switch instead of two. Original B601 full source/adversity preserved committed27b55703. Same nine complete jobs; focused4562pass, seven-case diagnostics pending before launch. No new policy/cache/owner. Async replacement correctness fix independent.'
identity['coreSHA256']={k:meta['images']['before'if k.startswith('reference')else'candidate']['Lokad.Lython.dll']for k in ['reference','reference-control','candidate','candidate-control']}
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
source=json.loads((old/'source-identity.json').read_text(encoding='utf-8'));source['sourceSHA256']=meta['sourceHashes'];source['candidateQualification']='Refined one-switch proof:4562focused actual11/10, seven-case bothAPIs/compile required before launch; full delivery pending.'
(q/'source-identity.json').write_text(json.dumps(source,indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(s/'image/Lokad.Lython.dll',q/'current-core.dll')
for n in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=(old/n).read_text(encoding='utf-8').replace('receiver-ops-b601-20261112','receiver-ops-b602-20261112').replace('lython-b601-receiver-ops-20261112','lython-b602-receiver-ops-20261112').replace('receiver-ops-b601-launch.py','receiver-ops-b602-launch.py').replace('lython-b601-fixtures','lython-b602-fixtures').replace('B601','B602')
 if n=='collect.py':t=t.replace('R256 shared canonical receiver/truth source pair','R256 refined one-switch canonical receiver/truth source pair')
 (q/n).write_text(t,encoding='utf-8',newline='\n')
 if n.endswith('.py')and n!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as a:
 for n in ['current-core.dll','representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:a.add(q/n,arcname=n)
with tarfile.open(q/'payload.tgz')as a:expanded=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())
retire=[];canonical=Path('benchmarks/results/2026-11-12-receiver-operations/first-form/b601/observations.tgz');data=canonical.read_bytes();assert data==subprocess.check_output(['git','show','HEAD:'+canonical.as_posix()])
with tarfile.open(canonical)as a:
 for entry,kind in [('current-core.dll','all-aliases'),('representative.json','exact')]:
  b=a.extractfile(entry).read();retire.append(dict(kind=kind,knownPath='receiver-ops-b601-20261112/'+entry,bytes=len(b),sha256=sha(b),canonicalStorage=canonical.as_posix(),canonicalEntry=entry,canonicalArchiveSHA256=sha(data),reason='Superseded first form preserved in exact committed archive; verify fresh idle lease, UID/path/inode and all hardlink aliases. Protect reference and new candidate.'))
retire.append(dict(kind='exact',knownPath='receiver-ops-b601-20261112/observations.tgz',bytes=len(data),sha256=sha(data),canonicalStorage=canonical.as_posix(),reason='Terminal duplicate raw archive, independently committed byte-for-byte.'))
p=Path('benchmarks/results/2026-11-08-receiver-contract/observations.tgz');b=p.read_bytes();assert b==subprocess.check_output(['git','show','HEAD:'+p.as_posix()]);assert len(b)==3860016
retire.append(dict(kind='exact',knownPath='receiver-contract-b448-20261108/observations.tgz',bytes=len(b),sha256=sha(b),canonicalStorage=p.as_posix(),reason='Unused terminal duplicate; exact committed canonical source/core/evidence preserved. Verify fresh path/UID/inode/allaliases. No current image retirement.'))
config=dict(root='/home/lokad/lython-bench-checks/receiver-ops-b602-20261112',unit='lython-b602-receiver-ops-20261112',payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha((q/'payload.tgz').read_bytes()),requiredFreeBytes=expanded+252*1024*1024,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
assert "len(raw['jobs'])==9"in(q/'archive-owned.py').read_text(encoding='utf-8')
assert "['jobs']==9"in(q/'retrieve.py').read_text(encoding='utf-8')and"['elapsedSeconds']<300"in(q/'retrieve.py').read_text(encoding='utf-8')
print('Prepared same9-job,300s/360s paired refinement; fresh inventory only, no launch until diagnostic gates pass.')
