import os, json, subprocess, fcntl, hashlib
from pathlib import Path
from datetime import datetime, timezone

stage = Path('/home/lokad/lython-bench-checks/text-milestone-4acd532f')
inspection = json.loads((stage / 'inspection.json').read_text())
tool = inspection['toolchains']
dotnet = tool['dotnet']['executable']
revision = '4acd532f7848f8a3a4cd54ff5c02d75320190830'
status = {'startedUtc': datetime.now(timezone.utc).isoformat(), 'phase': 'lease',
    'workers': {}}
def save():
    (stage / 'preparation.json').write_text(json.dumps(status, indent=2) + '\n')
env = dict(os.environ, DOTNET_ROOT=str(Path(dotnet).parent), DOTNET_CLI_TELEMETRY_OPTOUT='1',
    DOTNET_NOLOGO='1', MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0',
    GIT_TERMINAL_PROMPT='0')
def run(args, cwd=None, log=None):
    result = subprocess.run(args, cwd=cwd, env=env, capture_output=True, text=True)
    if log:
        (stage / log).write_text(result.stdout + result.stderr)
    if result.returncode:
        raise RuntimeError(f'{args[0]} failed:{result.returncode}: {(result.stderr or result.stdout)[-2000:]}')
    return result.stdout.strip()
save()
try:
    with Path('/home/lokad/lython-bench-checks/comparison-vm.lock').open('a+') as lease:
        fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
        launch = stage / 'build-launch'
        launch.mkdir()
        (launch / 'global.json').write_text(json.dumps({'sdk': {'version': '10.0.401', 'rollForward': 'disable'}}))
        status.update(dotnetVersion=run([dotnet, '--version'], cwd=launch), loadBefore=os.getloadavg())
        assert status['dotnetVersion'] == '10.0.401'
        source = stage / 'source-candidate'
        source.mkdir()
        status.update(phase='fetch-candidate'); save()
        run(['git', 'init', '-q', str(source)])
        run(['git', '-C', str(source), 'remote', 'add', 'origin', 'https://github.com/Lokad/Lython.git'])
        run(['git', '-C', str(source), 'fetch', '--depth=1', '--filter=blob:none', 'origin', revision], log='fetch-candidate.log')
        run(['git', '-C', str(source), 'config', 'core.sparseCheckout', 'true'])
        (source / '.git/info/sparse-checkout').write_text('/*\n!/*/\n/src/\n/benchmarks/Lokad.Lython.Benchmarks/\n/benchmarks/scripts/\n/benchmarks/cpython-worker.py\n/third_party/\n')
        run(['git', '-C', str(source), 'checkout', '--detach', revision], log='checkout-candidate.log')
        assert run(['git', '-C', str(source), 'rev-parse', 'HEAD']) == revision
        assert not run(['git', '-C', str(source), 'status', '--porcelain', '--untracked-files=normal'])
        status.update(phase='build-candidate'); save()
        project = source / 'benchmarks/Lokad.Lython.Benchmarks/Lokad.Lython.Benchmarks.csproj'
        run([dotnet, 'build', str(project), '-c', 'Release', '-v', 'minimal', '-nodeReuse:false', '-p:UseSharedCompilation=false'], cwd=launch, log='build-candidate.log')
        assert not run(['git', '-C', str(source), 'status', '--porcelain', '--untracked-files=normal'])
        worker = project.parent / 'bin/Release/net10.0/Lokad.Lython.Benchmarks.dll'
        status['workers']['candidate'] = {'revision': revision, 'root': str(source), 'worker': str(worker),
            'workerSha256': hashlib.sha256(worker.read_bytes()).hexdigest(),
            'librarySha256': hashlib.sha256(worker.with_name('Lokad.Lython.dll').read_bytes()).hexdigest(), 'sourceClean': True}
        status['workers']['old'] = dict(status['workers']['candidate'])
        status.update(phase='catalog'); save()
        catalog = stage / 'catalog.json'
        run([dotnet, str(worker), '--compare', 'list', '--profile', 'representative', '--out', str(catalog)], cwd=source, log='catalog.log')
        status.update(phase='complete', catalog=str(catalog), catalogSha256=hashlib.sha256(catalog.read_bytes()).hexdigest(),
            completedUtc=datetime.now(timezone.utc).isoformat(), loadAfter=os.getloadavg())
        save()
except BaseException as error:
    status.update(phase='failed', error=repr(error), completedUtc=datetime.now(timezone.utc).isoformat())
    save()
    raise
