import datetime,fcntl,hashlib,json,os,pathlib,signal,statistics,subprocess,time
root=pathlib.Path('/home/lokad/lython-bench-checks/proved-string-admission-db040f52-20261013')
old=root.parent/'embedding-exception-pressure-7f5fbf70-20261013'
lease=(root.parent/'comparison-vm.lock').open('a');fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
m=json.loads((root/'manifest.json').read_text());prepared=json.loads((root/'prepared.json').read_text())
results=root/'micro';results.mkdir()
started=time.monotonic();deadline=started+30
dotnet=pathlib.Path('/home/lokad/lython-bench-toolchains/dotnet/10.0.401/dotnet')
keys=('DOTNET_ReadyToRun','COMPlus_ReadyToRun','CORECLR_ENABLE_PROFILING','DOTNET_EnableDiagnostics')
overrides={k:v for k,v in os.environ.items() if k in keys or k.lower().startswith(('dotnet_gc','complus_gc','dotnet_tiered','complus_tiered','dotnet_jit','complus_jit'))};assert not overrides,overrides
env=dict(os.environ,DOTNET_ROOT=str(dotnet.parent),DOTNET_NOLOGO='1',DOTNET_CLI_TELEMETRY_OPTOUT='1')
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def cpu():
 v=list(map(int,pathlib.Path('/proc/stat').read_text().splitlines()[0].split()[1:9]));return sum(v),v[3]+v[4],v[7]
def vm():return {l.split()[0]:int(l.split()[1]) for l in pathlib.Path('/proc/vmstat').read_text().splitlines() if l.split()[0] in ('pgmajfault','pswpin','pswpout')}
for version,files in prepared['identities'].items():
 for name,expected in files.items():assert digest(root/version/name)==expected,name
for name,expected in m['fixtureHashes'].items():assert digest(old/'fixtures'/name)==expected,name
assert digest(dotnet)==prepared['dotnetSha256']
rows=[]
for label in m['cases']:
 stage=results/label;stage.mkdir();version,kind=label.split('-');control=kind=='control'
 row={'label':label,'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'eligible':False,'exclusion':None}
 if deadline-time.monotonic()<9:row['exclusion']='campaign cleanup reservation'
 else:
  idle=[]
  for _ in range(2):
   a=cpu();time.sleep(.25);b=cpu();idle.append(100*(b[0]-a[0]-(b[1]-a[1]))/(b[0]-a[0]))
  row['idleCpuPercent']=idle
  if statistics.median(idle)>3 or max(idle)>5:row['exclusion']='idle CPU gate'
  else:
   a=cpu();noise=vm();begin=time.monotonic()
   name='pressure-bound-sync'+('-control' if control else '')
   p=subprocess.Popen([str(dotnet),str(root/version/'Probe.dll'),name,str(old/'fixtures'),str(root)],env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE,start_new_session=True)
   timeout=False
   try:out,err=p.communicate(timeout=8)
   except subprocess.TimeoutExpired:timeout=True;os.killpg(p.pid,signal.SIGKILL);out,err=p.communicate(timeout=1)
   elapsed=time.monotonic()-begin;b=cpu();after=vm();(stage/'stdout.json').write_bytes(out);(stage/'stderr.txt').write_bytes(err)
   row.update(pid=p.pid,exitCode=p.returncode,timedOut=timeout,elapsedIncludingCleanupSeconds=elapsed,stdoutSha256=digest(stage/'stdout.json'),stderrSha256=digest(stage/'stderr.txt'),stealTicks=b[2]-a[2],noiseDelta={k:after[k]-noise[k] for k in noise})
   if timeout or p.returncode:row['exclusion']='owned child/output gate'
   else:
    data=json.loads(out);row['result']={k:v for k,v in data.items() if k not in ('fixtureHashes','requestLatencyMilliseconds','heartbeatLatenessMilliseconds')}
    assert data['policy']==m['policy'] and data['name']==name and data['windowSeconds']==1
    assert data['subjectDllSha256'].lower()==prepared['identities'][version]['Lokad.Lython.dll']
    assert data['subjectInformationalVersion'].endswith('+'+(m['beforeRevision'] if version=='before' else m['revision']))
    assert data['capBytes']==48000 and data['retainedPayloadBytes']==32000 and data['caughtDenialsPerJob']==128 and data['peakBytes']<=48000
    assert data['processorCount']==4 and data['preparationCollections']==[2,2,2] and data['hostGenerationsBefore']==[0,0,131072]
    assert data['warmupYieldedJobs']==data['warmupDelayedHostCalls']==data['yieldedJobs']==data['delayedHostCalls']==0
    assert data['samples']>0 and (data['completed']==0 if control else data['completed']>0)
    assert data['originalSourceSha256'].lower()==digest(root/'pressure-bound.py')
    if row['stealTicks'] or any(row['noiseDelta'].values()):row['exclusion']='steal/paging gate'
    else:row['eligible']=True
 rows.append(row);(stage/'receipt.json').write_text(json.dumps(row,indent=2));(results/'progress.json').write_text(json.dumps(rows,indent=2))
for version,files in prepared['identities'].items():
 for name,expected in files.items():assert digest(root/version/name)==expected,name
assert digest(dotnet)==prepared['dotnetSha256']
for name,expected in m['files'].items():assert digest(root/name)==expected,name
summary={'policy':m['policy'],'rows':rows,'elapsedSeconds':time.monotonic()-started,'runtimeOverrides':overrides,'allChildrenExited':all(r.get('exitCode') is not None for r in rows if 'pid' in r),'status':'short diagnostic only; no percentile/SLO/CPython qualification'}
(results/'summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps({'samples':len(rows),'eligible':sum(r['eligible'] for r in rows),'seconds':summary['elapsedSeconds']}))
