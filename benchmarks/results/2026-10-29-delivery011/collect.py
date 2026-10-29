"""Six fixed complete scripts at the delivery checkpoint; no lane campaign."""
import fcntl
import json
import os
from pathlib import Path
import subprocess
import sys
import time

base = Path('/home/lokad/lython-bench-checks')
tree = base / 'delivery-da0ff811'
dotnet = Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
python = Path('/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13')
relative = 'benchmarks/Lokad.Lython.Benchmarks/bin/Release/net11.0/Lokad.Lython.Benchmarks.dll'
worker = tree / relative
baseline = base / 'published-source-aff8965f/benchmarks/Lokad.Lython.Benchmarks/bin/Release/net10.0/Lokad.Lython.Benchmarks.dll'
catalog = base / 'release-0.11-baseline-20261029/representative.json'
output = Path(sys.argv[1]).resolve()
cases = ['scripts.' + name + size for name, size in [
    ('csv-summary-helpers', '.large'), ('text-summary-helpers', '.large'), ('json-summary-helpers', '.large'),
    ('config-edit', '.small'), ('file-manifest-helpers', '.large'), ('file-search', '.large')]]
env = os.environ.copy()
env['DOTNET_ROOT'] = str(dotnet.parent)

with (base / 'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
    output.mkdir(exist_ok=False)
    time.sleep(2)
    started = time.monotonic()
    record = dict(performanceQualified=False, revision='da0ff81155c27609e9d3904043af8fff2ba76943',
        publishedBaseline='actual 0.10.0 DLL; source aff8965fc18a722f19be819a03f4ea16128dd31c; net10.0.12',
        lane='compile-run', wallBudgetSeconds=90, cases=cases, jobs=[])
    def save():
        (output / 'collection.json').write_text(json.dumps(record, indent=2)+'\n')
    def run(arguments, name):
        command = ['sudo', '-n', 'systemd-run', '--quiet', '--wait', '--pipe', '--collect',
                   '--unit=lython-delivery-compare-' + name + '-' + str(os.getpid()), '--uid=lokad',
                   '--working-directory=' + str(tree), '-p', 'RuntimeMaxSec=30',
                   '-p', 'TimeoutStopSec=0', '-p', 'KillMode=control-group',
                   '--setenv=DOTNET_ROOT=' + str(dotnet.parent), '--setenv=DOTNET_TieredCompilation=0',
                   str(dotnet), str(worker), *arguments]
        with (output / (name+'.log')).open('w') as log:
            result = subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=35)
        if result.returncode: raise RuntimeError((output / (name+'.log')).read_text())
    run(['--compare', 'check-machine', '--out', str(output / 'machine-before.json')], 'before')
    for i, case in enumerate(cases):
        if time.monotonic()-started > 70: raise TimeoutError('Reserve cleanup within the prospective budget.')
        stem = str(i).zfill(2)+'-'+case
        run(['--compare', 'micro', '--catalog', str(catalog), '--dotnet', str(dotnet),
             '--baseline-worker', str(baseline), '--python', str(python), '--python-worker',
             str(tree / 'benchmarks/cpython-worker.py'), '--out', str(output / (stem+'.json')),
             '--case', case, '--lane', 'compile-run'], stem)
        receipt = json.loads((output / (stem+'.json')).read_text())
        record['jobs'].append(dict(case=case, receipt=stem+'.json', state=receipt.get('state'), summaries=receipt.get('summaries')))
        save()
        print(stem, 'complete', flush=True)
    run(['--compare', 'check-machine', '--out', str(output / 'machine-after.json')], 'after')
    record['elapsedSeconds'] = time.monotonic()-started
    save()
    print('Six-script delivery comparison complete; lease released.', flush=True)
