from pathlib import Path
import fcntl,hashlib,json,os,subprocess,time
root=Path(__file__).resolve().parent
previous=root.parent/'expression-route-b48-20261101'
dotnet='/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet'
env=os.environ.copy();env['DOTNET_ROOT']=str(Path(dotnet).parent)
env.pop('DOTNET_TieredCompilation',None);env.pop('COMPlus_TieredCompilation',None)
identity=json.loads((root/'candidate-identity.json').read_text())
before=previous/'candidate/Lokad.Lython.Benchmarks.dll';candidate=root/'candidate/Lokad.Lython.Benchmarks.dll'
for worker,expected in ((before,identity['baselineCoreSHA256']),(candidate,identity['coreSHA256'])):
    assert hashlib.sha256((worker.parent/'Lokad.Lython.dll').read_bytes()).hexdigest()==expected
record=dict(processes=[],performanceQualified=False,noRetiming=True,processBudgetSeconds=60)
with (root.parent/'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
    started=time.monotonic()
    def run(command,timeout,output):
        remaining=60-(time.monotonic()-started)
        if remaining<=0:raise TimeoutError('Aggregate fixed collection budget.')
        result=subprocess.run(command,env=env,capture_output=True,timeout=min(remaining,timeout))
        output.write_bytes(result.stdout);output.with_suffix('.stderr.txt').write_bytes(result.stderr)
        result.check_returncode()
    run([dotnet,str(candidate),'--compare','check-machine','--out',str(root/'machine-before.json')],10,root/'machine-before.stdout.txt')
    assert json.loads((root/'machine-before.json').read_text())['gate']['quiet']
    for phase in ('before','candidate','before-control'):
        worker=candidate if phase=='candidate' else before
        for mode,case in (('warm-async','scripts.csv-summary-helpers.large'),('warm','scripts.csv-summary-helpers.large'),('warm','scripts.text-summary-helpers.large')):
            output=root/(phase+'-'+mode+'-'+case+'.json')
            assert not output.exists(),'Never overwrite a frozen observation.'
            begin=time.monotonic()
            run([dotnet,str(root/'jobs/bin/Release/net11.0/complete-jobs.dll'),str(worker),
                 identity['source' if phase=='candidate' else 'baselineSource'],str(root/'catalog.json'),case,mode],30,output)
            value=json.loads(output.read_text())['observations'][0]
            record['processes'].append(dict(phase=phase,case=case,mode=mode,receipt=output.name,seconds=time.monotonic()-begin))
            (root/'collection.json').write_text(json.dumps(record,indent=2)+'\n')
            print(phase,mode,case,'CPU ms',value['medianCpuMillisecondsPerJob'],'wall ms',value['medianMilliseconds'],flush=True)
    run([dotnet,str(candidate),'--compare','check-machine','--out',str(root/'machine-after.json')],10,root/'machine-after.stdout.txt')
    assert json.loads((root/'machine-after.json').read_text())['gate']['quiet']
    record.update(totalWallSeconds=time.monotonic()-started,quietGates=True,allChildrenJoined=True)
    (root/'collection.json').write_text(json.dumps(record,indent=2)+'\n')
    print('One frozen comparison complete; children joined and lease released.',flush=True)
