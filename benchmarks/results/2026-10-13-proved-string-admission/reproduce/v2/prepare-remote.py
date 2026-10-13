import fcntl,hashlib,json,os,pathlib,shutil,subprocess,tarfile,time
root=pathlib.Path('/home/lokad/lython-bench-checks/proved-string-admission-db040f52-micro2-20261013')
old=root.parent/'embedding-exception-pressure-7f5fbf70-20261013'
started=time.monotonic()
lock=(root.parent/'comparison-vm.lock').open('a');fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
with tarfile.open(root/'payload.tar') as a:a.extractall(root,filter='data')
m=json.loads((root/'manifest.json').read_text())
for name,digest in m['files'].items():assert hashlib.sha256((root/name).read_bytes()).hexdigest()==digest,name
subject=root/'subject';subject.mkdir()
with tarfile.open(root/'subject.tar') as a:a.extractall(subject,filter='data')
probe=subject/'ImpactProbe';probe.mkdir()
for name in ['Program.cs','Probe.csproj']:shutil.copyfile(root/name,probe/name)
dotnet=pathlib.Path('/home/lokad/lython-bench-toolchains/dotnet/10.0.401/dotnet')
env=dict(os.environ,DOTNET_ROOT=str(dotnet.parent),DOTNET_NOLOGO='1',DOTNET_CLI_TELEMETRY_OPTOUT='1')
cmd=[str(dotnet),'build',str(probe/'Probe.csproj'),'-c','Release','-p:SubjectRoot='+str(subject),'-p:SourceRevisionId='+m['revision'],'-p:RepositoryCommit='+m['revision'],'-p:EnableSourceControlManagerQueries=false','-p:EnableSourceLink=false','--tl:off','--nologo','-v','minimal']
with (root/'build.stdout.txt').open('wb') as out,(root/'build.stderr.txt').open('wb') as err:
 p=subprocess.Popen(cmd,env=env,cwd=subject,stdout=out,stderr=err,start_new_session=True)
 timeout=False
 try:code=p.wait(timeout=28)
 except subprocess.TimeoutExpired:
  import signal
  timeout=True;os.killpg(p.pid,signal.SIGKILL);code=p.wait(timeout=1)
receipt={'pid':p.pid,'exitCode':code,'timedOut':timeout,'elapsedSeconds':time.monotonic()-started,'command':cmd}
(root/'build.receipt.json').write_text(json.dumps(receipt,indent=2))
assert not code and not timeout
shutil.copytree(probe/'bin/Release/net10.0',root/'after')
shutil.copytree(root/'after',root/'before')
oldbin=old/'subject/ImpactProbe/bin/Release/net10.0'
for name,digest in m['oldIdentities'].items():assert hashlib.sha256((oldbin/name).read_bytes()).hexdigest()==digest,name
for name in ['Lokad.Lython.dll','Lokad.Lython.pdb']:shutil.copyfile(oldbin/name,root/'before'/name)
identities={case:{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/case).iterdir() if p.suffix in ['.dll','.json']} for case in ['before','after']}
assert identities['before']['Probe.dll']==identities['after']['Probe.dll']
for name,digest in identities['before'].items():
 if name!='Lokad.Lython.dll':assert identities['after'][name]==digest,name
assert identities['before']['Lokad.Lython.dll']==m['beforeDllSha256']
(root/'prepared.json').write_text(json.dumps({'receipt':receipt,'identities':identities,'dotnetSha256':hashlib.sha256(dotnet.read_bytes()).hexdigest()},indent=2))
print(json.dumps({'prepared':True,'elapsedSeconds':time.monotonic()-started,'sameObserver':True}))
