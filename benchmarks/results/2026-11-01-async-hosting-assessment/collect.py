from pathlib import Path
import fcntl,hashlib,json,os,subprocess,time
root=Path(__file__).resolve().parent
previous=root.parent/'expression-route-b48-20261101'
dotnet='/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet'
env=os.environ.copy();env['DOTNET_ROOT']=str(Path(dotnet).parent)
env.pop('DOTNET_TieredCompilation',None);env.pop('COMPlus_TieredCompilation',None)
worker=previous/'candidate/Lokad.Lython.Benchmarks.dll'
assert hashlib.sha256((worker.parent/'Lokad.Lython.dll').read_bytes()).hexdigest()=='ecf3ee0fa0318e0143d01483ad8f27343e5d5e4bfe36c5f616b2a50b4fa8bde6'
record=dict(processes=[],performanceQualified=False,noRetiming=True,processBudgetSeconds=60,
            boundary='Same frozen core/source; Run versus RunAsync. Complete warm jobs, compilation/startup excluded; Task joined before result inspection/disposal.')
with (root.parent/'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
    started=time.monotonic()
    def run(command,timeout,output):
        remaining=60-(time.monotonic()-started)
        if remaining<=0:raise TimeoutError('Aggregate fixed collection budget.')
        result=subprocess.run(command,env=env,capture_output=True,timeout=min(remaining,timeout))
        output.write_bytes(result.stdout);output.with_suffix('.stderr.txt').write_bytes(result.stderr)
        result.check_returncode()
        return result
    run([dotnet,str(worker),'--compare','check-machine','--out',str(root/'machine-before.json')],10,root/'machine-before.stdout.txt')
    assert json.loads((root/'machine-before.json').read_text())['gate']['quiet']
    for phase in ('sync','async','sync-control'):
        catalog=root/'catalog.json'
        mode='warm-async' if phase=='async' else 'warm'
        for case in ('scripts.csv-summary-helpers.large','scripts.text-summary-helpers.large'):
            output=root/(phase+'-'+case+'.json')
            assert not output.exists(),'Never overwrite a frozen observation.'
            begin=time.monotonic()
            run([dotnet,str(root/'jobs/bin/Release/net11.0/complete-jobs.dll'),str(worker),
                 '082dd130 + B48 source.patch; '+phase,str(catalog),case,mode],30,output)
            value=json.loads(output.read_text())['observations'][0]
            record['processes'].append(dict(phase=phase,case=case,receipt=output.name,seconds=time.monotonic()-begin))
            (root/'collection.json').write_text(json.dumps(record,indent=2)+'\n')
            print(phase,case,'CPU ms',value['medianCpuMillisecondsPerJob'],'wall ms',value['medianMilliseconds'],flush=True)
    run([dotnet,str(worker),'--compare','check-machine','--out',str(root/'machine-after.json')],10,root/'machine-after.stdout.txt')
    assert json.loads((root/'machine-after.json').read_text())['gate']['quiet']
    record.update(totalWallSeconds=time.monotonic()-started,quietGates=True,allChildrenJoined=True)
    (root/'collection.json').write_text(json.dumps(record,indent=2)+'\n')
    print('One fixed API-variant collection complete; children joined and lease released.',flush=True)
