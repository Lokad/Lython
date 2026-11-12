from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path('.git/agent-notes/review-20261031/receiver-operations-r256');q=r/'b603';q.mkdir(exist_ok=True);old=r.parent/'native-inputs-r251/b598';b602=r/'refined/b602'
sha=lambda b:hashlib.sha256(b).hexdigest()
identity=json.loads((b602/'identity.json').read_text(encoding='utf-8'));h=identity['coreSHA256']['candidate'];identity['coreSHA256']={k:h for k in ['candidate','candidate-control']};identity.pop('protectedPriorReferenceSHA256',None)
identity['sourceRevision']='82ed92d422a947726fdc802108cd0ffebbaa5472 exact frozen refined source/core0106; fullCI43139754244pending. Three affected complete scripts: captured text, original native search, guarded search. CPython3.13.16, separate compile/warm/compile-run; unchanged worker/host/goldens. No source-pair attribution.'
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8');shutil.copyfile(b602/'source-identity.json',q/'source-identity.json');shutil.copyfile(r/'catalog.json',q/'representative.json')
name='receiver-cpython-b603-20261112';unit='lython-b603-receiver-cpython-20261112'
jobs=[[case,lane]for case in ['scripts.text-summary-helpers.nonlocal-counter.large','scripts.file-search.large','scripts.file-search.guarded-helper.large']for lane in ['compile','warm','compile-run']]
for n in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=(old/n).read_text(encoding='utf-8').replace('native-inputs-cpython-b598-20261112',name).replace('lython-b598-native-inputs-cpython-20261112',unit).replace('native-inputs-cpython-b598-launch.py','receiver-cpython-b603-launch.py').replace('lython-b598-fixtures','lython-b603-fixtures').replace('B598','B603')
 if n=='collect.py':
  a=t.index('jobs=');b=t.index('\nenv =',a);t=t[:a]+'jobs='+repr(jobs)+'\nassert len(jobs)==9 and all(case in cases for case,lane in jobs)\n'+t[b:]
  assert ' < 520' in t
  t=t.replace(' < 520',' < 265').replace('Reserve50seconds','Reserve35seconds')
  a=t.index("purpose='");b=t.index("',\n                  warmupSeconds",a);t=t[:a]+"purpose='Retained82ed92d4 shared receiver checkpoint versus stock CPython3.13.16. Three affected complete scripts, separate compile/warm/compile-run; fixed worker/adapter/command-v2 boundary. Original source-pair uncertainty preserved, no isolated attribution"+t[b:]
 if n=='remote-launch.py':
  t=t.replace("source=base/'native-inputs-b597-20261112/candidate'", "source=base/'receiver-ops-b602-20261112/candidate'")
  a=t.index('protected=');b=t.index('\nwith(base/',a);t=t[:a]+"protected={source/'Lokad.Lython.dll':config['identity']['coreSHA256']['candidate'],base/'native-inputs-b597-20261112/candidate/Lokad.Lython.dll':'c9829194548b8c94b1f2bb4c4d5d3eea548d43b4b10a4920e27d273d9e64b351'}"+t[b:]
  t=t.replace('jobs=18,ownerSeconds=570','jobs=9,ownerSeconds=300').replace('RuntimeMaxSec=570','RuntimeMaxSec=300')
 if n=='launch.py':t=t.replace('seconds=630','seconds=360').replace('ownerSeconds=570','ownerSeconds=300')
 if n=='archive-owned.py':t=t.replace("len(raw['jobs'])==18","len(raw['jobs'])==9").replace('complete=True,jobs=18','complete=True,jobs=9')
 if n=='retrieve.py':t=t.replace("['jobs']==18","['jobs']==9").replace("['elapsedSeconds']<570","['elapsedSeconds']<300").replace('eighteen-job','nine-job')
 if n=='summarize.py':t=t.replace("len(raw['jobs'])==18","len(raw['jobs'])==9").replace('complete=True,jobs=18','complete=True,jobs=9')
 (q/n).write_text(t,encoding='utf-8',newline='\n')
 if n.endswith('.py')and n!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as a:
 for n in ['representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:a.add(q/n,arcname=n)
with tarfile.open(q/'payload.tgz')as a:expanded=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())
p=Path('benchmarks/results/2026-11-12-receiver-operations/refined/b602/observations.tgz');b=p.read_bytes();assert b==subprocess.check_output(['git','show','HEAD:'+p.as_posix()])
config=dict(root='/home/lokad/lython-bench-checks/'+name,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha((q/'payload.tgz').read_bytes()),requiredFreeBytes=expanded+252*1024*1024,retire=[dict(kind='exact',knownPath='receiver-ops-b602-20261112/observations.tgz',bytes=len(b),sha256=sha(b),canonicalStorage=p.as_posix(),reason='Only exact terminal raw duplicate; source/core first-form/refined evidence committed82ed92d4. Protect current0106 and baselinec982 core aliases. Fresh idle/UID/path/inode/allaliases proof required; headroom unchanged.')],identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
for n,needle in [('collect.py','len(jobs)==9'),('archive-owned.py',"len(raw['jobs'])==9"),('retrieve.py',"['jobs']==9"),('remote-launch.py','RuntimeMaxSec=300'),('launch.py','seconds=360')]:assert needle in(q/n).read_text(encoding='utf-8'),n
print('Nine affected CPython jobs,300sowner/265slast-start/360sobservation; existing images reused, no build or framework.')
