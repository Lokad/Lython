import pathlib,tarfile,json,hashlib,fcntl,os,subprocess,time,signal
root=pathlib.Path('/home/lokad/lython-bench-checks/call-transfer-09aeae83-20261013')
lease=(root.parent/'comparison-vm.lock').open('a');fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
started=time.monotonic()
with tarfile.open(root/'payload.tar.gz') as t:t.extractall(root,filter='data')
m=json.loads((root/'manifest.json').read_text())
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for name,sha in m['files'].items():assert digest(root/name)==sha,name
overrides={k:v for k,v in os.environ.items() if k.lower().startswith(('dotnet_gc','complus_gc','dotnet_tiered','complus_tiered','dotnet_jit','complus_jit'))};assert not overrides,overrides
def cpu():
 v=list(map(int,pathlib.Path('/proc/stat').read_text().splitlines()[0].split()[1:9]));return sum(v),v[3]+v[4],v[7]
def noise():return {l.split()[0]:int(l.split()[1]) for l in pathlib.Path('/proc/vmstat').read_text().splitlines() if l.startswith(('pswpin ','pswpout ','pgmajfault '))}
idle=[]
for _ in range(4):
 a=cpu();time.sleep(.25);b=cpu();idle.append(100*(1-(b[1]-a[1])/(b[0]-a[0])));assert b[2]==a[2]
assert sorted(idle)[len(idle)//2]<=3 and max(idle)<=5,idle
dotnet='/home/lokad/lython-bench-toolchains/dotnet/10.0.401/dotnet'
python='/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13'
env=dict(os.environ,DOTNET_ROOT=str(pathlib.Path(dotnet).parent),DOTNET_NOLOGO='1',DOTNET_CLI_TELEMETRY_OPTOUT='1')
cmd=[dotnet,str(root/'after/Lokad.Lython.Benchmarks.dll'),'--compare','micro','--catalog',str(root/'catalog.json'),'--dotnet',dotnet,'--baseline-worker',str(root/'before/Lokad.Lython.Benchmarks.dll'),'--python',python,'--python-worker',str(root/'cpython-worker.py'),'--out',str(root/'micro.json'),'--case','calls.keyword.large']
a=cpu();n=noise()
with (root/'stdout.txt').open('wb') as out,(root/'stderr.txt').open('wb') as err:
 p=subprocess.Popen(cmd,env=env,cwd=root,stdout=out,stderr=err,start_new_session=True)
 timeout=False
 try:code=p.wait(timeout=24)
 except subprocess.TimeoutExpired:timeout=True;os.killpg(p.pid,signal.SIGKILL);code=p.wait(timeout=1)
b=cpu();nn=noise()
receipt={'pid':p.pid,'hasExited':p.poll() is not None,'exitCode':code,'timedOut':timeout,'elapsedIncludingCleanupSeconds':time.monotonic()-started,'idleBusyPercent':idle,'noiseDelta':{k:nn[k]-n[k] for k in n},'stealTicks':b[2]-a[2],'runtimeOverrides':overrides,'command':cmd,'dotnetSha256':digest(pathlib.Path(dotnet)),'pythonSha256':digest(pathlib.Path(python)),'stdoutSha256':digest(root/'stdout.txt'),'stderrSha256':digest(root/'stderr.txt')}
(root/'process.receipt.json').write_text(json.dumps(receipt,indent=2))
for name,sha in m['files'].items():assert digest(root/name)==sha,name
print(json.dumps({'code':code,'seconds':receipt['elapsedIncludingCleanupSeconds'],'noise':receipt['noiseDelta']}))
assert not code and not timeout and receipt['elapsedIncludingCleanupSeconds']<30
