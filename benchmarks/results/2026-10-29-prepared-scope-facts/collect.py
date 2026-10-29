"""Fixed compile/complete-job comparison and real phase allocations; no retries."""
import fcntl
import json
import os
from pathlib import Path
import subprocess
import sys
import time

base = Path('/home/lokad/lython-bench-checks')
candidate = base / 'scope-facts-cc988b9d'
control = base / 'annotation-comprehensions-1c5cff2a'
dotnet = Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
python = Path('/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13')
relative = 'benchmarks/Lokad.Lython.Benchmarks/bin/Release/net11.0/Lokad.Lython.Benchmarks.dll'
worker = candidate / relative
catalogs = base / 'release-0.11-baseline-20261029'
output = Path(sys.argv[1]).resolve()
jobs = [('compile', 'scripts.' + name + size, 'representative') for name, size in [
    ('csv-summary-helpers', '.large'), ('text-summary-helpers', '.large'), ('json-summary-helpers', '.large'),
    ('config-edit', '.small'), ('file-manifest-helpers', '.large'), ('file-search', '.large')]]
jobs += [('compile-run', 'scripts.' + name + '.large', 'representative')
         for name in ['csv-summary-helpers', 'text-summary-helpers']]
jobs += [('warm', 'loops.integer.large', 'full')]
env = os.environ.copy()
env['DOTNET_ROOT'] = str(dotnet.parent)

with (base / 'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
    output.mkdir(exist_ok=False)
    started = time.monotonic()
    record = dict(performanceQualified=False, candidateRevision='cc988b9d288a0622f9849a0e1e04a3f692fe5305',
                  controlRevision='1c5cff2a59d1defd94049e70940267a569cc717a', wallBudgetSeconds=120, jobs=[])
    def save():
        (output / 'collection.json').write_text(json.dumps(record, indent=2) + '\n')
    def run(args, name, executable=worker, override=True):
        command = ['sudo', '-n', 'systemd-run', '--quiet', '--wait', '--pipe', '--collect',
                   '--unit=lython-scope-facts-' + name + '-' + str(os.getpid()), '--uid=lokad',
                   '--working-directory=' + str(candidate), '-p', 'RuntimeMaxSec=30',
                   '-p', 'TimeoutStopSec=0', '-p', 'KillMode=control-group',
                   '--setenv=DOTNET_ROOT=' + str(dotnet.parent),
                   *(['--setenv=DOTNET_TieredCompilation=0'] if override else []), str(dotnet), str(executable), *args]
        with (output / (name+'.log')).open('w') as log:
            result = subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=35)
        if result.returncode: raise RuntimeError((output / (name+'.log')).read_text())
    run(['--compare', 'check-machine', '--out', str(output / 'machine-before.json')], 'before')
    for i, (lane, case, profile) in enumerate(jobs):
        if time.monotonic()-started > 100: raise TimeoutError('Reserve cleanup inside the fixed boundary.')
        stem = str(i).zfill(2) + '-' + lane + '-' + case
        run(['--compare', 'micro', '--catalog', str(catalogs / (profile+'.json')), '--dotnet', str(dotnet),
             '--baseline-worker', str(control / relative), '--python', str(python), '--python-worker',
             str(candidate / 'benchmarks/cpython-worker.py'), '--out', str(output / (stem+'.json')),
             '--case', case, '--lane', lane], stem)
        receipt = json.loads((output / (stem+'.json')).read_text())
        record['jobs'].append(dict(case=case, lane=lane, receipt=stem+'.json', state=receipt.get('state'),
                                   summaries=receipt.get('summaries')))
        save()
        print(stem, 'complete', flush=True)
    run(['--compare', 'check-machine', '--out', str(output / 'machine-after.json')], 'after')
    record['elapsedSeconds'] = time.monotonic()-started
    save()
    diagnostic = base / 'foundation-diagnostics/bin/Release/net11.0/Diagnostics.dll'
    for name, tree in [('control', control), ('candidate', candidate)]:
        run(['phases', str((tree / relative).parent / 'Lokad.Lython.dll'), str(catalogs / 'representative.json'),
             str(output / ('phases-'+name+'.json'))], 'phases-'+name, diagnostic, override=False)
    print('Fixed comparison and allocation diagnostics complete; lease released.', flush=True)
