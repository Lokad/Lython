"""Fixed adapter diagnostic comparison; uses the existing micro CLI and shared VM lease."""
import fcntl
import json
import os
from pathlib import Path
import subprocess
import sys
import time

base = Path.home() / 'lython-bench-checks'
candidate = base / 'regex-adapter04-d005e85c'
baseline = base / 'callframes-candidate-4b408097'
dotnet = Path.home() / 'lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet'
python = Path.home() / 'lython-bench-toolchains/cpython/3.13.16/bin/python3.13'
worker_relative = 'benchmarks/Lokad.Lython.Benchmarks/bin/Release/net11.0/Lokad.Lython.Benchmarks.dll'
worker = candidate / worker_relative
catalog = base / 'regex-adapter04-catalog-20261029.json'
output = Path(sys.argv[1]).resolve()
jobs = [('warm', 'regex.iterator.1000', None), ('warm-async', 'regex.reuse.100', None)]
env = os.environ.copy()
env['DOTNET_ROOT'] = str(dotnet.parent)

with (base / 'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
    output.mkdir(exist_ok=False)
    record = dict(performanceQualified=False, purpose='bounded optional-adapter comparison',
                  candidateRevision='d005e85c4728a6cada56e00316ddeec211cffbbc',
                  baselineRevision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=baseline, text=True).strip(),
                  wallBudgetSeconds=60, plannedJobs=jobs, jobs=[])
    def save():
        (output / 'collection.json').write_text(json.dumps(record, indent=2) + '\n')
    def run(arguments, stem):
        unit = 'lython-regex04-' + str(os.getpid()) + '-' + str(time.monotonic_ns())
        command = ['sudo', '-n', 'systemd-run', '--quiet', '--wait', '--pipe', '--collect',
                   '--unit=' + unit, '--uid=lokad', '--working-directory=' + str(candidate),
                   '-p', 'RuntimeMaxSec=30', '-p', 'TimeoutStopSec=0', '-p', 'KillMode=control-group',
                   '--setenv=DOTNET_ROOT=' + str(dotnet.parent), '--setenv=DOTNET_TieredCompilation=0',
                   str(dotnet), str(worker), *arguments]
        with (output / (stem + '.log')).open('w') as log:
            return subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=35).returncode
    record['initialGateExit'] = run(['--compare', 'check-machine', '--out', str(output / 'machine-before.json')], 'before')
    save()
    if record['initialGateExit']:
        raise SystemExit('Quiet gate refused collection.')
    started = time.monotonic()
    for index, (lane, case, profile) in enumerate(jobs):
        if time.monotonic() - started >= 40:
            record['stoppingReason'] = 'Reserve cleanup within the one-minute boundary.'
            break
        stem = f'{index:02d}-{lane}-{case}'
        receipt = output / (stem + '.json')
        code = run(['--compare', 'micro', '--catalog', str(catalog),
                    '--dotnet', str(dotnet), '--baseline-worker', str(baseline / worker_relative),
                    '--python', str(python), '--python-worker', str(candidate / 'benchmarks/cpython-worker.py'),
                    '--out', str(receipt), '--case', case, '--lane', lane], stem)
        row = dict(case=case, lane=lane, exitCode=code, receipt=receipt.name)
        if receipt.exists():
            data = json.loads(receipt.read_text())
            row.update({key: data.get(key) for key in ['state', 'error', 'summaries']})
        record['jobs'].append(row)
        save()
        print(json.dumps(row), flush=True)
        if code:
            record['stoppingReason'] = 'A fixed comparison failed; no retry.'
            break
    record['elapsedSeconds'] = time.monotonic() - started
    record['finalGateExit'] = run(['--compare', 'check-machine', '--out', str(output / 'machine-after.json')], 'after')
    save()
    print('Fixed adapter comparison finished; lease released.', flush=True)
