"""One fixed complete-job CPU/allocation diagnostic; no timing selection/retry."""
from pathlib import Path
import json, os, subprocess, sys, time

root = Path(__file__).resolve().parent
phase = sys.argv[1]
assert phase in ('before', 'after', 'before-control')
worker = 'candidate' if phase == 'after' else 'baseline'
revision = '4cca2041 + source.patch' if phase == 'after' else 'ae22392d'
state_path = root / 'diagnostic-state.json'
state = json.loads(state_path.read_text(encoding='utf-8')) if state_path.exists() else {'processBudgetSeconds': 90, 'elapsedProcessSeconds': 0, 'processes': []}
env = os.environ.copy()
env['DOTNET_ROOT'] = '/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148'
env.pop('DOTNET_TieredCompilation', None)
env.pop('COMPlus_TieredCompilation', None)
for ident in ['scripts.text-summary-helpers.large', 'scripts.csv-summary-helpers.large']:
    output = root / (phase + '-' + ident.removeprefix('scripts.') + '.json')
    if output.exists():
        raise RuntimeError('Preserve the frozen observation: ' + str(output))
    remaining = state['processBudgetSeconds'] - state['elapsedProcessSeconds']
    if remaining <= 0:
        raise TimeoutError('Fixed aggregate diagnostic process-time budget exhausted.')
    started = time.monotonic()
    process = subprocess.run([
        env['DOTNET_ROOT'] + '/dotnet',
        str(root / 'jobs/bin/Release/net11.0/complete-jobs.dll'),
        str(root / worker / 'Lokad.Lython.Benchmarks.dll'), revision,
        str(root / 'catalog.json'), ident], env=env, capture_output=True,
        timeout=min(30, remaining))
    elapsed = time.monotonic() - started
    output.with_suffix('.stderr.txt').write_bytes(process.stderr)
    process.check_returncode()
    output.write_bytes(process.stdout)
    value = json.loads(process.stdout)
    assert len(value['observations']) == 1
    row = value['observations'][0]
    state['elapsedProcessSeconds'] += elapsed
    state['processes'].append({'phase': phase, 'case': ident, 'seconds': elapsed, 'receipt': output.name})
    state_path.write_text(json.dumps(state, indent=2) + '\n', encoding='utf-8')
    print(phase, ident, 'cpu/job ms', row['medianCpuMillisecondsPerJob'],
          'wall/job ms', row['medianMilliseconds'], 'allocation B', row['medianAllocatedBytes'],
          'minimum CPU sample ms', min(row['cpuMilliseconds']), flush=True)
