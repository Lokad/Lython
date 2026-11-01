from pathlib import Path
import fcntl,json,os,subprocess,sys,time
root=Path(__file__).resolve().parent
base=root.parent
dotnet=Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['DOTNET_TieredCompilation']='0'
with (base/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 started=time.monotonic()
 for side in ('before','after'):
  if side=='after':
   for phase in ('before','after','before-control'):
    subprocess.run([sys.executable,str(root/'run-jobs.py'),phase],check=True,timeout=100)
  path=root/('machine-'+side+'.json')
  subprocess.run([str(dotnet),str(root/'candidate/Lokad.Lython.Benchmarks.dll'),'--compare','check-machine','--out',str(path)],env=env,check=True,timeout=10)
  assert json.loads(path.read_text())['gate']['quiet'],side
 (root/'collection.json').write_text(json.dumps({'totalWallSeconds':time.monotonic()-started,'workerTiering':'ordinary','quietGates':True,'noRetiming':True,'source':'e349f64b + source.patch','baseline':'830c6251'},indent=2)+'\n')
 print('Fixed six-process comparison complete; all children joined and lease released.',flush=True)
