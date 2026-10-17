import json,subprocess,os,fcntl,hashlib
from pathlib import Path
from datetime import datetime,timezone
stage=Path('/home/lokad/lython-bench-checks/csv-warm-async-1b6a1531')
capture=stage/'capture'
prep=json.loads((stage/'preparation.json').read_text())
tool=json.loads(Path('/home/lokad/lython-bench-toolchains/toolchains.json').read_text())
receipt={'collectionCount':1,'unit':'lython-csv-async-capture-1b6a1531-20261017','startedUtc':datetime.now(timezone.utc).isoformat(),'state':'Starting'}
def save(): (capture/'ownership.json').write_text(json.dumps(receipt,indent=2)+'\n')
save()
try:
    with Path('/home/lokad/lython-bench-checks/comparison-vm.lock').open('a+') as lease:
        fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
        receipt.update(state='LeaseHeld',loadBefore=os.getloadavg());save()
        dotnet=tool['dotnet']['executable'];candidate=prep['workers']['candidate']
        command=[dotnet,candidate['worker'],'--compare','micro','--catalog',prep['catalog'],'--dotnet',dotnet,'--baseline-worker',prep['workers']['old']['worker'],'--python',tool['python']['executable'],'--python-worker',str(Path(candidate['root'])/'benchmarks/cpython-worker.py'),'--out',str(capture/'micro.json'),'--case','scripts.csv-summary-helpers.large','--lane','warm-async']
        receipt.update(command=command,supervisorTieredCompilation='0');save()
        env=dict(os.environ,DOTNET_ROOT=str(Path(dotnet).parent),DOTNET_TieredCompilation='0',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1')
        with (capture/'micro.log').open('w') as output:
            result=subprocess.run(command,cwd=candidate['root'],env=env,stdout=output,stderr=subprocess.STDOUT)
        receipt.update(state='Completed' if result.returncode==0 else 'Failed',exitCode=result.returncode,loadAfter=os.getloadavg(),stoppedUtc=datetime.now(timezone.utc).isoformat());save()
except BaseException as error:
    receipt.update(state='Failed',error=repr(error),stoppedUtc=datetime.now(timezone.utc).isoformat());save();raise
raise SystemExit(receipt['exitCode'])
