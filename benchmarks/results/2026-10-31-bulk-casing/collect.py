"""Two fixed representative old/new observations using the existing micro command."""
import fcntl,json,os,subprocess,time
from pathlib import Path
base=Path('/home/lokad/lython-bench-checks')
root=base/'bulk-casing-20261031'
dotnet=Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
python=Path('/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13')
candidate=root/'candidate/Lokad.Lython.Benchmarks.dll'
baseline=root/'baseline/Lokad.Lython.Benchmarks.dll'
catalog=base/'release-0.11-baseline-20261029/representative.json'
helper=base/'json-writer-20261030/cpython-worker.py'
output=root/'observations'
cases=['scripts.'+stem+size for stem,size in [('text-summary','.large'),('text-summary-helpers','.large'),('csv-summary-helpers','.large'),('json-summary-helpers','.large'),('config-edit','.small')]]
with (base/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 output.mkdir(exist_ok=False)
 started=time.monotonic()
 record=dict(performanceQualified=False,baseline='2b01c4d6',candidate='2b01c4d6 + source.patch',wallBudgetSeconds=180,observations=2,jobs=[])
 def save(): (output/'collection.json').write_text(json.dumps(record,indent=2)+'\n')
 def run(arguments,name):
  command=['sudo','-n','systemd-run','--quiet','--wait','--pipe','--collect','--unit=lython-casing-'+name+'-'+str(os.getpid()),'--uid=lokad','--working-directory='+str(root),'-p','RuntimeMaxSec=30','-p','TimeoutStopSec=0','-p','KillMode=control-group','--setenv=DOTNET_ROOT='+str(dotnet.parent),'--setenv=DOTNET_TieredCompilation=0',str(dotnet),str(candidate),*arguments]
  with (output/(name+'.log')).open('w') as log:
   result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,timeout=35)
  if result.returncode: raise RuntimeError((output/(name+'.log')).read_text())
 run(['--compare','check-machine','--out',str(output/'machine-before.json')],'before')
 record['quietBefore']=json.loads((output/'machine-before.json').read_text())['gate']['quiet']
 for observation in [1,2]:
  for lane,selected in [('warm',cases[:4]),('compile-run',cases[:2]+cases[4:])]:
   for index,case in enumerate(selected):
    if time.monotonic()-started>150: raise TimeoutError('Leave cleanup headroom in the fixed collection budget.')
    name=str(observation)+'-'+lane+'-'+str(index)
    receipt=output/(name+'.json')
    run(['--compare','micro','--catalog',str(catalog),'--dotnet',str(dotnet),'--baseline-worker',str(baseline),'--python',str(python),'--python-worker',str(helper),'--out',str(receipt),'--case',case,'--lane',lane],name)
    result=json.loads(receipt.read_text())
    record['jobs'].append(dict(observation=observation,lane=lane,case=case,receipt=receipt.name,state=result['state'],summaries=result['summaries']))
    save()
    print(name,case,'complete',flush=True)
 run(['--compare','check-machine','--out',str(output/'machine-after.json')],'after')
 record['quietAfter']=json.loads((output/'machine-after.json').read_text())['gate']['quiet']
 record['elapsedSeconds']=time.monotonic()-started
 save()
 print('Fixed bulk casing comparison complete; lease released.',flush=True)
