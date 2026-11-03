import json,hashlib,subprocess,resource,time,os,shutil
from pathlib import Path
r=Path('/home/lokad/lython-bench-checks/completed-fault-b167-20261103')
dotnet='/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet'
env=os.environ.copy();env['DOTNET_ROOT']=str(Path(dotnet).parent)
rows=[]
for label,source in [('before','ordinary-call-transport-b165-20261103/candidate'),('candidate','completed-fault-b167-20261103/candidate')]:
 target=r/('guard-'+label);target.mkdir(exist_ok=True)
 for p in r.glob('Lokad.Lython.WorkerFixture.*'):
  output=target/p.name
  if output.is_symlink():output.unlink()
  shutil.copyfile(p,output)
 image=r.parent/source
 for name in ['Lokad.Lython.dll','Lokad.Lython.Utf8Regex.dll','Lokad.Utf8Regex.dll','Lokad.Utf8Regex.PythonRe.dll']:
  if not (target/name).exists():(target/name).symlink_to(image/name)
 cmd=[dotnet,str(target/'Lokad.Lython.WorkerFixture.dll'),'stack-guard',str(target/'Lokad.Lython.dll'),'copyhook']
 before=resource.getrusage(resource.RUSAGE_CHILDREN);start=time.monotonic()
 try:
  p=subprocess.run(cmd,env=env,capture_output=True,text=True,timeout=25)
  row=dict(label=label,exitCode=p.returncode,stdout=p.stdout,stderr=p.stderr,timedOut=False)
 except subprocess.TimeoutExpired as e:
  row=dict(label=label,exitCode=None,stdout=(e.stdout or b'').decode(),stderr=(e.stderr or b'').decode(),timedOut=True)
 after=resource.getrusage(resource.RUSAGE_CHILDREN)
 row.update(seconds=time.monotonic()-start,cpuSeconds=after.ru_utime+after.ru_stime-before.ru_utime-before.ru_stime,coreSHA256=hashlib.sha256((target/'Lokad.Lython.dll').read_bytes()).hexdigest(),unchangedProcessBoundSeconds=25)
 rows.append(row);(r/'copyhook-assessment.json').write_text(json.dumps(rows,indent=2)+'\n');print(json.dumps(row),flush=True)
assert all(x['exitCode']==0 and json.loads(x['stdout'])['Success'] and json.loads(x['stdout'])['value']=='3' and json.loads(x['stdout'])['completedAsynchronously']>0 for x in rows)
