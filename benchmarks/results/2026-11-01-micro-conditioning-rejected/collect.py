"""Two fixed representative old/new observations using the existing micro command."""
import fcntl,json,os,signal,subprocess,sys,time
signal.signal(signal.SIGTERM, lambda *_: sys.exit(124))
from pathlib import Path
base=Path('/home/lokad/lython-bench-checks')
root=base/'micro-conditioning-20261101'
dotnet=Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
python=Path('/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13')
candidate=root/'candidate/Lokad.Lython.Benchmarks.dll'
baseline=root/'baseline/Lokad.Lython.Benchmarks.dll'
catalog=root/'vm-catalog.json'
helper=base/'json-writer-20261030/cpython-worker.py'
output=root/'observations'
cases=['scripts.file-search.replace.large', 'scripts.numeric-report.float.replace.large', 'scripts.csv-summary-helpers.replace.large', 'scripts.numeric-report.float.large', 'scripts.file-search.replace.large']
with (base/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 output.mkdir(exist_ok=False)
 started=time.monotonic()
 record=dict(performanceQualified=False,baseline='01beee54 historical sampler A/A',candidate='01beee54 + source.patch conditioned sampler A/A',wallBudgetSeconds=120,observations=1,jobs=[])
 def save(): (output/'collection.json').write_text(json.dumps(record,indent=2)+'\n')
 def run(arguments,name):
  unit='lython-micro-conditioning-'+name+'-'+str(os.getpid())
  command=['sudo','-n','systemd-run','--quiet','--wait','--pipe','--collect','--unit='+unit,'--uid=lokad','--working-directory='+str(root),'-p','RuntimeMaxSec=30','-p','TimeoutStopSec=0','-p','KillMode=control-group','--setenv=DOTNET_ROOT='+str(dotnet.parent),'--setenv=DOTNET_TieredCompilation=0',str(dotnet),str(orchestrator),*arguments]
  with (output/(name+'.log')).open('w') as log:
   try:
    result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,timeout=35)
   except BaseException:
    subprocess.run(['sudo','-n','systemctl','stop',unit+'.service'],stdout=log,stderr=subprocess.STDOUT,timeout=10)
    raise
  if result.returncode: raise RuntimeError((output/(name+'.log')).read_text())
 orchestrator=candidate
 run(['--compare','check-machine','--out',str(output/'machine-before.json')],'before')
 record['quietBefore']=json.loads((output/'machine-before.json').read_text())['gate']['quiet']
 for algorithm in ['baseline','candidate']:
  orchestrator=root/algorithm/'Lokad.Lython.Benchmarks.dll'
  for index,(lane,case) in enumerate([('warm','scripts.file-search.replace.large'),('warm','scripts.numeric-report.float.replace.large'),('compile-run','scripts.file-search.replace.large')]):
   if time.monotonic()-started>95: raise TimeoutError('Leave cleanup headroom within fixed 120 seconds.')
   name=algorithm+'-'+lane+'-'+str(index)
   receipt=output/(name+'.json')
   run(['--compare','micro','--catalog',str(catalog),'--dotnet',str(dotnet),'--baseline-worker',str(orchestrator),'--python',str(python),'--python-worker',str(helper),'--out',str(receipt),'--case',case,'--lane',lane],name)
   result=json.loads(receipt.read_text())
   record['jobs'].append(dict(algorithm=algorithm,lane=lane,case=case,receipt=receipt.name,state=result['state'],summaries=result['summaries']))
   save()
   print(name,case,'complete',flush=True)
 run(['--compare','check-machine','--out',str(output/'machine-after.json')],'after')
 record['quietAfter']=json.loads((output/'machine-after.json').read_text())['gate']['quiet']
 record['elapsedSeconds']=time.monotonic()-started
 save()
 print('Fixed sampler A/A comparison complete; lease released.',flush=True)
