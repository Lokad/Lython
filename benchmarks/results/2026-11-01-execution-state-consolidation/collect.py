from pathlib import Path
import fcntl, json, os, subprocess, sys, time

root=Path(__file__).resolve().parent
dotnet=Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
python=Path('/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13')
env=os.environ.copy(); env['DOTNET_ROOT']=str(dotnet.parent)
env.pop('DOTNET_TieredCompilation',None); env.pop('COMPlus_TieredCompilation',None)
worker=str(root/'candidate/Lokad.Lython.Benchmarks.dll')
with (root.parent/'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
    started=time.monotonic()
    def run(arguments, timeout):
        remaining=90-(time.monotonic()-started)
        if remaining <= 0: raise TimeoutError('Fixed aggregate collection budget.')
        subprocess.run(arguments,env=env,check=True,timeout=min(timeout,remaining))
    for side in ('before','after'):
        if side=='after':
            for phase in ('before','after','before-control'):
                run([sys.executable,str(root/'run-jobs.py'),phase],65)
            run([str(dotnet),worker,'--compare','micro','--catalog',str(root/'catalog.json'),
                 '--dotnet',str(dotnet),'--baseline-worker',str(root/'baseline/Lokad.Lython.Benchmarks.dll'),
                 '--python',str(python),'--python-worker',str(root/'cpython-worker.py'),
                 '--out',str(root/'compile-control.json'),'--case','scripts.csv-summary-helpers.large',
                 '--lane','compile'],30)
        output=root/('machine-'+side+'.json')
        run([str(dotnet),worker,'--compare','check-machine','--out',str(output)],10)
        assert json.loads(output.read_text())['gate']['quiet'],side
    (root/'collection.json').write_text(json.dumps(dict(totalWallSeconds=time.monotonic()-started,
        workerTiering='ordinary',quietGates=True,noRetiming=True,source='44e29e05 + source.patch + new-source',
        baseline='5d16ead3; runtime9e99f640',processesJoined=True),indent=2)+'\n')
    print('Fixed six-process warm comparison and compile control complete; all users joined.',flush=True)
