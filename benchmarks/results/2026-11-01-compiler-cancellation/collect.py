from pathlib import Path
import fcntl,json,os,subprocess,time
root=Path(__file__).resolve().parent
dotnet='/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet'
env=os.environ.copy();env['DOTNET_ROOT']=str(Path(dotnet).parent)
env.pop('DOTNET_TieredCompilation',None);env.pop('COMPlus_TieredCompilation',None)
jobs=[('compile','scripts.'+case+'.large') for case in ('csv-summary-helpers','text-summary-helpers')]
jobs += [('warm','scripts.text-summary-helpers.large')]
record=dict(processes=[],performanceQualified=False,noRetiming=True,processBudgetSeconds=60)
with (root.parent/'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
    started=time.monotonic()
    def run(command,timeout,output=None):
        remaining=60-(time.monotonic()-started)
        if remaining<=0:raise TimeoutError('Aggregate fixed collection budget.')
        result=subprocess.run(command,env=env,capture_output=True,timeout=min(remaining,timeout))
        if output is not None:
            output.write_bytes(result.stdout);output.with_suffix('.stderr.txt').write_bytes(result.stderr)
        result.check_returncode()
        return result
    for side in ('before','after'):
        if side=='after':
            for phase in ('before','after','before-control'):
                worker='candidate' if phase=='after' else 'baseline'
                for mode,case in jobs:
                    output=root/(phase+'-'+mode+'-'+case+'.json')
                    assert not output.exists(),'Never overwrite a frozen observation.'
                    begin=time.monotonic()
                    run([dotnet,str(root/'jobs/bin/Release/net11.0/complete-jobs.dll'),
                         str(root/worker/'Lokad.Lython.Benchmarks.dll'),
                         '6b631425 + source.patch' if phase=='after' else '6b631425',str(root/'catalog.json'),case,mode],30,output)
                    value=json.loads(output.read_text())['observations'][0]
                    record['processes'].append(dict(phase=phase,case=case,mode=mode,receipt=output.name,seconds=time.monotonic()-begin))
                    (root/'collection.json').write_text(json.dumps(record,indent=2)+'\n')
                    print(phase,mode,case,'CPU ms',value['medianCpuMillisecondsPerJob'],'wall ms',value['medianMilliseconds'],flush=True)
        output=root/('machine-'+side+'.json')
        run([dotnet,str(root/'candidate/Lokad.Lython.Benchmarks.dll'),'--compare','check-machine','--out',str(output)],10)
        assert json.loads(output.read_text())['gate']['quiet'],side
    record.update(totalWallSeconds=time.monotonic()-started,quietGates=True,allChildrenJoined=True)
    (root/'collection.json').write_text(json.dumps(record,indent=2)+'\n')
    print('One fixed collection complete; children joined and lease released.',flush=True)
