"""Fixed two-pair comparison of the unchanged owned stack witnesses."""
import fcntl
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

root = Path('/home/lokad/lython-bench-checks/class-method-preparation-20261030')
dotnet = Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
quiet_worker = root.parent / 'json-writer-20261030/candidate/Lokad.Lython.Benchmarks.dll'
out = root / 'results'
cases = ['class-getitem-descriptor', 'class-getitem', 'function-finally']
record = dict(baseline='0411ee184822a064533cdc30587aea124d317a19',
              candidate='same base plus candidate.patch', rounds=2,
              performanceQualified=False, jobs=[])

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def run(arguments, stem, seconds=25):
    command = ['sudo', '-n', 'systemd-run', '--quiet', '--wait', '--pipe', '--collect',
               '--unit=lython-class-method-' + stem + '-' + str(os.getpid()),
               '--uid=lokad', '--working-directory=' + str(root),
               '-p', 'RuntimeMaxSec=' + str(seconds), '-p', 'TimeoutStopSec=0',
               '-p', 'KillMode=control-group', '--setenv=DOTNET_ROOT=' + str(dotnet.parent),
               *arguments]
    started = time.monotonic()
    with (out / (stem + '.log')).open('w') as log:
        completed = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=seconds + 5)
    return completed.returncode, time.monotonic() - started

with (root.parent / 'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
    out.mkdir(exist_ok=False)
    record['identities'] = {side: dict(library=digest(root / side / 'Lokad.Lython.dll'),
        fixture=digest(root / side / 'Lokad.Lython.WorkerFixture.dll'))
        for side in ['baseline', 'candidate']}
    record['patchSha256'] = digest(root / 'candidate.patch')
    def save():
        (out / 'comparison.json').write_text(json.dumps(record, indent=2) + '\n')
    save()
    code, _ = run([str(dotnet), str(quiet_worker), '--compare', 'check-machine',
        '--out', str(out / 'machine-before.json')], 'before')
    if code or not json.loads((out / 'machine-before.json').read_text())['gate']['quiet']:
        raise RuntimeError('Initial quiet gate failed; preserve evidence without retry.')
    for round_number in range(2):
        for case in cases:
            for side in (['baseline', 'candidate'] if round_number == 0 else ['candidate', 'baseline']):
                stem = str(round_number) + '-' + case + '-' + side
                binary = root / side / 'Lokad.Lython.WorkerFixture.dll'
                library = root / side / 'Lokad.Lython.dll'
                code, wall = run(['/usr/bin/time', '-f', 'user=%U system=%S wall=%e maxrss=%M',
                    '-o', str(out / (stem + '.time')), str(dotnet), str(binary),
                    'stack-guard', str(library), case], stem)
                row = dict(round=round_number, case=case, side=side, exitCode=code,
                           outerWallSeconds=wall, log=stem + '.log', timing=stem + '.time')
                record['jobs'].append(row)
                save()
                print(stem, 'exit', code, 'outer seconds', round(wall, 3), flush=True)
                if code:
                    raise RuntimeError('Unchanged witness failed; stop without retry.')
    code, _ = run([str(dotnet), str(quiet_worker), '--compare', 'check-machine',
        '--out', str(out / 'machine-after.json')], 'after')
    record['quietAfter'] = code == 0 and json.loads((out / 'machine-after.json').read_text())['gate']['quiet']
    save()
    if not record['quietAfter']:
        raise RuntimeError('Final quiet gate failed; preserve evidence without retry.')
    print('Fixed comparison complete; lease released.', flush=True)
