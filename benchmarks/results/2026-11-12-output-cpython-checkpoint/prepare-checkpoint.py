from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b609';q.mkdir(exist_ok=False)
old=r.parent/'receiver-operations-r256/b603';current=r/'refined/b608'
sha=lambda b:hashlib.sha256(b).hexdigest()
identity=json.loads((current/'identity.json').read_text(encoding='utf-8'))
h=identity['coreSHA256']['candidate'];identity['coreSHA256']={k:h for k in ['candidate','candidate-control']}
identity['sourceRevision']='983bab966f13d4575b514eb20dd237a12159bc65; exact568 frozenproductionfiles/coreb2cbfdb6, fullCI43165101428pending. UTF8/UTF16reports,capturedtext,ordinarytextcontrol vsCPython3.13.16. Compile/warm/compile-run separate. No attribution tosinglechange; originaladversity preserved.'
catalog=json.loads((r/'catalog.json').read_text(encoding='utf-8'))
more=json.loads((old/'representative.json').read_text(encoding='utf-8'))
cases={c['id']:c for c in catalog['cases']+more['cases']}
selected=['scripts.report-render.large','scripts.report-render.utf16.large','scripts.text-summary-helpers.nonlocal-counter.large','scripts.text-summary-helpers.large']
catalog['cases']=[cases[k]for k in selected]
(q/'representative.json').write_text(json.dumps(catalog,indent=2)+'\n',encoding='utf-8')
identity['catalogSHA256']=sha((q/'representative.json').read_bytes())
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
shutil.copyfile(current/'source-identity.json',q/'source-identity.json')
jobs=[[c,l]for c in selected for l in ['compile','warm','compile-run']]
name='output-cpython-b609-20261112';unit='lython-b609-output-cpython-20261112'
for n in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 t=(old/n).read_text(encoding='utf-8').replace('receiver-cpython-b603-20261112',name).replace('lython-b603-receiver-cpython-20261112',unit).replace('receiver-cpython-b603-launch.py','output-cpython-b609-launch.py').replace('lython-b603-fixtures','lython-b609-fixtures').replace('B603','B609')
 if n=='collect.py':
  a=t.index('jobs=');b=t.index('\nenv =',a);t=t[:a]+'jobs='+repr(jobs)+'\nassert len(jobs)==12 and all(case in cases for case,lane in jobs)\n'+t[b:]
  assert ' < 265'in t;t=t.replace(' < 265',' < 355')
  a=t.index("purpose='");b=t.index("',\n                  warmupSeconds",a)
  t=t[:a]+"purpose='Retained983bab96 complete output checkpoint versusstockCPython3.13.16. UTF8/UTF16reports,capturedtext,ordinarytextcontrol. Separatecompile/warm/compile-run; fixedworker/host/validation/cleanup. No isolatedattribution"+t[b:]
 if n=='remote-launch.py':
  t=t.replace("source=base/'receiver-ops-b602-20261112/candidate'","source=base/'text-batches-b608-20261112/candidate'")
  a=t.index('protected=');b=t.index('\nwith(base/',a)
  t=t[:a]+"protected={source/'Lokad.Lython.dll':config['identity']['coreSHA256']['candidate'],base/'cell-authority-b606-20261112/candidate/Lokad.Lython.dll':'09ad301e790ce664ed044088f80e8e144c5d502de70f656ccb55eeb235e0e463'}"+t[b:]
  t=t.replace('jobs=9,ownerSeconds=300','jobs=12,ownerSeconds=390').replace('RuntimeMaxSec=300','RuntimeMaxSec=390')
 if n=='launch.py':t=t.replace('seconds=360','seconds=450').replace('ownerSeconds=300','ownerSeconds=390')
 if n=='archive-owned.py':t=t.replace("len(raw['jobs'])==9","len(raw['jobs'])==12").replace('complete=True,jobs=9','complete=True,jobs=12')
 if n=='retrieve.py':t=t.replace("['jobs']==9","['jobs']==12").replace("['elapsedSeconds']<300","['elapsedSeconds']<390").replace('nine-job','twelve-job')
 if n=='summarize.py':t=t.replace("len(raw['jobs'])==9","len(raw['jobs'])==12").replace('len(external)==9','len(external)==12')
 (q/n).write_text(t,encoding='utf-8',newline='\n')
 if n.endswith('.py')and n!='remote-launch.py':ast.parse(t)
with tarfile.open(q/'payload.tgz','w:gz')as a:
 for n in ['representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:a.add(q/n,arcname=n)
with tarfile.open(q/'payload.tgz')as a:expanded=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())
retire=[]
archive=Path('benchmarks/results/2026-11-12-text-output-batches/refined/b608/observations.tgz')
assert archive.read_bytes()==subprocess.check_output(['git','show','HEAD:'+archive.as_posix()])
for member in [None,'representative.json','confirmation/confirmation.json']:
 with tarfile.open(archive)as t:b=archive.read_bytes()if member is None else t.extractfile(member).read()
 spec=dict(kind='exact',knownPath='text-batches-b608-20261112/'+(member or 'observations.tgz'),bytes=len(b),sha256=sha(b),canonicalStorage=archive.as_posix(),reason='Only terminalexact duplicate; committed983bab96 canonicalGitSHA verified, originaladverseforms preserved. Currentb2cb/f4cores protected. Freshidle/UID/path/inode/allaliasesproof; unchangedfloors.')
 if member:spec.update(canonicalEntry=member,canonicalArchiveSHA256=sha(archive.read_bytes()))
 retire.append(spec)
config=dict(root='/home/lokad/lython-bench-checks/'+name,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha((q/'payload.tgz').read_bytes()),requiredFreeBytes=expanded+252*1024*1024,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
for n,needle in [('collect.py','len(jobs)==12'),('archive-owned.py',"len(raw['jobs'])==12"),('retrieve.py',"['jobs']==12"),('remote-launch.py','RuntimeMaxSec=390'),('launch.py','seconds=450'),('summarize.py','len(external)==12')]:assert needle in(q/n).read_text(encoding='utf-8'),n
print('12 existing-worker CPythonjobs,390sowner/355slaststart/450sobservation; existingexactimage reused, noframework/build.')
