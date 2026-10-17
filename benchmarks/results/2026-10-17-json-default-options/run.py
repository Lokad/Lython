import json,subprocess,os,fcntl
from pathlib import Path
from datetime import datetime,timezone
s=Path('/home/lokad/lython-bench-checks/json-default-options-f728636a');c=s/'capture'
p=json.loads((s/'preparation.json').read_text());t=json.loads(Path('/home/lokad/lython-bench-toolchains/toolchains.json').read_text())
r={'collectionCount':1,'state':'Starting','startedUtc':datetime.now(timezone.utc).isoformat()}
def save():(c/'ownership.json').write_text(json.dumps(r,indent=2)+'\n')
save()
try:
    with Path('/home/lokad/lython-bench-checks/comparison-vm.lock').open('a+') as lease:
        fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
        cmd=[t['dotnet']['executable'],p['worker'],'--compare','micro','--catalog',p['catalog'],'--dotnet',t['dotnet']['executable'],'--baseline-worker',p['baselineWorker'],'--python',t['python']['executable'],'--python-worker',str(Path(p['root'])/'benchmarks/cpython-worker.py'),'--out',str(c/'micro.json'),'--case','scripts.json-summary-helpers.large','--lane','warm-async']
        r.update(state='LeaseHeld',command=cmd,loadBefore=os.getloadavg(),supervisorTieredCompilation='0');save()
        env=dict(os.environ,DOTNET_ROOT=str(Path(t['dotnet']['executable']).parent),DOTNET_TieredCompilation='0',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1')
        with (c/'micro.log').open('w') as log:result=subprocess.run(cmd,cwd=p['root'],env=env,stdout=log,stderr=subprocess.STDOUT)
        r.update(state='Completed' if result.returncode==0 else 'Failed',exitCode=result.returncode,loadAfter=os.getloadavg(),stoppedUtc=datetime.now(timezone.utc).isoformat());save()
except BaseException as error:
    r.update(state='Failed',error=repr(error),stoppedUtc=datetime.now(timezone.utc).isoformat());save();raise
raise SystemExit(r['exitCode'])
