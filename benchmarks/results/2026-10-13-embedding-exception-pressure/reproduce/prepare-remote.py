import fcntl, hashlib, json, os, pathlib, shutil, subprocess, tarfile, time

root = pathlib.Path('/home/lokad/lython-bench-checks/embedding-exception-pressure-7f5fbf70-20261013')
started = time.monotonic()
lock = (root.parent / 'comparison-vm.lock').open('a')
fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
with tarfile.open(root / 'payload.tar') as archive: archive.extractall(root, filter='data')
manifest = json.loads((root / 'manifest.json').read_text())
for name, expected in manifest['files'].items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == expected, name
subject = root / 'subject'
subject.mkdir(exist_ok=False)
with tarfile.open(root / 'subject.tar') as archive: archive.extractall(subject, filter='data')
probe = subject / 'ImpactProbe'
probe.mkdir()
for name in ['Program.cs', 'Probe.csproj']: shutil.copyfile(root / name, probe / name)
for mode in ['unbound','bound','exposed','retained']:
    name = 'pressure-'+mode+'.py'
    shutil.copyfile(root/name,subject/'benchmarks/scripts'/name)
fixtures = root / 'fixtures'
fixtures.mkdir()
previous = root.parent / 'embedding-impact-20261012/fixtures'
for name, expected in manifest['fixtureHashes'].items():
    path = previous / name
    assert hashlib.sha256(path.read_bytes()).hexdigest() == expected, name
    target = fixtures / name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(path, target)
dotnet = pathlib.Path('/home/lokad/lython-bench-toolchains/dotnet/10.0.401/dotnet')
environment = dict(os.environ, DOTNET_ROOT=str(dotnet.parent), DOTNET_NOLOGO='1', DOTNET_CLI_TELEMETRY_OPTOUT='1')
command = [str(dotnet), 'build', str(probe / 'Probe.csproj'), '-c', 'Release', '-p:SubjectRoot='+str(subject),
           '-p:SourceRevisionId='+manifest['revision'], '-p:RepositoryCommit='+manifest['revision'],
           '-p:EnableSourceControlManagerQueries=false', '-p:EnableSourceLink=false', '--tl:off', '--nologo', '-v', 'minimal']
with (root / 'build.stdout.txt').open('wb') as out, (root / 'build.stderr.txt').open('wb') as err:
    process = subprocess.Popen(command, cwd=subject, env=environment, stdout=out, stderr=err, start_new_session=True)
    timeout = False
    try: code = process.wait(timeout=28)
    except subprocess.TimeoutExpired:
        import signal
        timeout = True; os.killpg(process.pid, signal.SIGKILL); code = process.wait(timeout=1)
elapsed = time.monotonic() - started
receipt = {'pid': process.pid, 'exitCode': code, 'timedOut': timeout, 'elapsedIncludingPreparationSeconds': elapsed,
           'source': manifest['revision'], 'productionTree': manifest['productionTree'], 'command': command}
(root / 'build.receipt.json').write_text(json.dumps(receipt, indent=2))
if code or timeout: raise RuntimeError('Owned preparation build failed; preserve output')
directory = probe / 'bin/Release/net10.0'
identities = {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in directory.iterdir() if path.suffix in ['.dll', '.json']}
(root / 'prepared.json').write_text(json.dumps({'receipt': receipt, 'identities': identities, 'dotnetSha256': hashlib.sha256(dotnet.read_bytes()).hexdigest()}, indent=2))
print(json.dumps({'prepared': True, 'elapsedSeconds': elapsed, 'artifactFiles': len(identities)}))
