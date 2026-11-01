"""Fixed post-B27 representative phase checkpoint; no lane campaign."""
import fcntl
import json
import os
from pathlib import Path
import subprocess
import sys
import time

base = Path('/home/lokad/lython-bench-checks')
tree = base / 'representative-checkpoint-20261101'
dotnet = Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
python = Path('/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13')
relative = 'candidate/Lokad.Lython.Benchmarks.dll'
worker = tree / relative
baseline = base / 'json-writer-20261030/candidate/Lokad.Lython.Benchmarks.dll'
catalog = tree / 'representative.json'
output = Path(sys.argv[1]).resolve()
jobs = [(f'scripts.{name}-summary-helpers.large', lane)
        for lane in ('compile-run','warm') for name in ('csv','text','json')]
jobs.append(('scripts.csv-summary-helpers.large','compile'))
cases = [case for case, lane in jobs]

env = os.environ.copy()
env['DOTNET_ROOT'] = str(dotnet.parent)

with (base / 'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
    output.mkdir(exist_ok=False)
    time.sleep(2)
    started = time.monotonic()
    record = dict(performanceQualified=False, revision='830c6251',
        baselineRevision='4433986e; same net11 runtime',
        lanes='three compile-run, three warm, one CSV compile-only', wallBudgetSeconds=90, cases=cases, jobs=[])
    def save():
        (output / 'collection.json').write_text(json.dumps(record, indent=2)+'\n')
    def run(arguments, name):
        command = ['sudo', '-n', 'systemd-run', '--quiet', '--wait', '--pipe', '--collect',
                   '--unit=lython-representative-checkpoint-' + name + '-' + str(os.getpid()), '--uid=lokad',
                   '--working-directory=' + str(tree), '-p', 'RuntimeMaxSec=30',
                   '-p', 'TimeoutStopSec=0', '-p', 'KillMode=control-group',
                   '--setenv=DOTNET_ROOT=' + str(dotnet.parent), '--setenv=DOTNET_TieredCompilation=0',
                   str(dotnet), str(worker), *arguments]
        with (output / (name+'.log')).open('w') as log:
            result = subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=35)
        if result.returncode: raise RuntimeError((output / (name+'.log')).read_text())
    run(['--compare', 'check-machine', '--out', str(output / 'machine-before.json')], 'before')
    for i, (case, lane) in enumerate(jobs):
        if time.monotonic()-started > 70: raise TimeoutError('Reserve cleanup within the prospective budget.')
        stem = str(i).zfill(2)+'-'+lane+'-'+case
        run(['--compare', 'micro', '--catalog', str(catalog), '--dotnet', str(dotnet),
             '--baseline-worker', str(baseline), '--python', str(python), '--python-worker',
             str(tree / 'cpython-worker.py'), '--out', str(output / (stem+'.json')),
             '--case', case, '--lane', lane], stem)
        receipt = json.loads((output / (stem+'.json')).read_text())
        record['jobs'].append(dict(case=case, lane=lane, receipt=stem+'.json', state=receipt.get('state'), summaries=receipt.get('summaries')))
        save()
        print(stem, 'complete', flush=True)
    run(['--compare', 'check-machine', '--out', str(output / 'machine-after.json')], 'after')
    record['elapsedSeconds'] = time.monotonic()-started
    save()
    print('Fixed seven-job diagnostic complete; lease released.', flush=True)
