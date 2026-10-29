"""One fixed diagnostic collection, existing lease/host/catalog, no timing retries."""
import fcntl
import json
import os
from pathlib import Path
import subprocess
import sys
import time

base = Path('/home/lokad/lython-bench-checks')
tree = base / 'annotation-comprehensions-1c5cff2a'
dotnet = Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
source = Path(__file__).resolve().parent / 'diagnostics'
worker = tree / 'benchmarks/Lokad.Lython.Benchmarks/bin/Release/net11.0/Lokad.Lython.Benchmarks.dll'
catalog = base / 'release-0.11-baseline-20261029/representative.json'
output = Path(sys.argv[1]).resolve()
env = os.environ.copy()
env['DOTNET_ROOT'] = str(dotnet.parent)
env['PATH'] = str(dotnet.parent) + ':' + env['PATH']
env.pop('DOTNET_TieredCompilation', None)

with (base / 'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
    output.mkdir(exist_ok=False)
    with (output / 'build.log').open('w') as log:
        subprocess.run([str(dotnet), 'build', str(source / 'Diagnostics.csproj'), '-c', 'Release',
                        '-p:LythonCorePath=' + str(worker.parent / 'Lokad.Lython.dll'),
                        '--tl:off', '--nologo', '-v', 'minimal'], env=env, stdout=log,
                       stderr=subprocess.STDOUT, check=True, timeout=60)
    time.sleep(2)
    started = time.monotonic()

    def run(arguments, executable, name, seconds):
        command = ['sudo', '-n', 'systemd-run', '--quiet', '--wait', '--pipe', '--collect',
                   '--unit=lython-runtime-phases-' + name + '-' + str(os.getpid()), '--uid=lokad',
                   '--working-directory=' + str(tree), '-p', 'RuntimeMaxSec=' + str(seconds),
                   '-p', 'TimeoutStopSec=0', '-p', 'KillMode=control-group',
                   '--setenv=DOTNET_ROOT=' + str(dotnet.parent), str(dotnet), str(executable), *arguments]
        with (output / (name + '.log')).open('w') as log:
            result = subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=seconds + 5)
        print(name, result.returncode, flush=True)
        if result.returncode:
            raise RuntimeError((output / (name + '.log')).read_text())

    run(['--compare', 'check-machine', '--out', str(output / 'machine-before.json')], worker, 'before', 20)
    run([str(worker), str(catalog), str(output / 'observations.json')],
        source / 'bin/Release/net11.0/Diagnostics.dll', 'observations', 60)
    run(['--compare', 'check-machine', '--out', str(output / 'machine-after.json')], worker, 'after', 20)
    (output / 'collection.json').write_text(json.dumps(dict(
        performanceQualified=False, candidateRevision=subprocess.check_output(
            ['git', 'rev-parse', 'HEAD'], cwd=tree, text=True).strip(),
        elapsedSeconds=time.monotonic()-started, ordinaryTiering=True,
        note='One fixed diagnostic collection; not CPython timing or a campaign qualification.'), indent=2) + '\n')
    print('Collection complete; lease released.', flush=True)
