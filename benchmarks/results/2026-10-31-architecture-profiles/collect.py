import fcntl,json,os,subprocess,time
from pathlib import Path
base=Path('/home/lokad/lython-bench-checks')
root=base/'architecture-20261031-07c150a6'
dotnet=Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
worker=root/'worker/Lokad.Lython.Benchmarks.dll'
env=dict(os.environ,DOTNET_ROOT=str(dotnet.parent))
with (base/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 started=time.monotonic()
 for when in ['before']:
  subprocess.run([str(dotnet),str(worker),'--compare','check-machine','--out',str(root/('machine-'+when+'.json'))],env=env,check=True,timeout=20)
  assert json.loads((root/('machine-'+when+'.json')).read_text())['gate']['quiet']
 for mode in ['eventpipe','perf']:
  args=['python3',str(root/'profile.py'),str(root/mode)]
  if mode=='perf':args.append('perf')
  subprocess.run(args,env=env,check=True,timeout=100)
 subprocess.run([str(dotnet),str(worker),'--compare','check-machine','--out',str(root/'machine-after.json')],env=env,check=True,timeout=20)
 assert json.loads((root/'machine-after.json').read_text())['gate']['quiet']
 (root/'collection.json').write_text(json.dumps(dict(source='07c150a6d2c3085206622e16dd32793e6333e558',elapsedSeconds=time.monotonic()-started,performanceQualified=False,note='Fixed attribution capture, not timing; no reruns.'),indent=2)+'\n')
 print('Bounded attribution collection complete',flush=True)
