"""Representative CPython delivery checkpoint through unchanged comparison workers/protocol."""
import contextlib, fcntl, hashlib, importlib.util, json, math, os, shutil
import statistics, subprocess, tempfile, time
from pathlib import Path

root = Path('/home/lokad/lython-bench-checks/current-allocation-b345-20261106')
base = Path('/home/lokad/lython-bench-checks/constructor-frames-refinement-b343-20261106')
dotnet = Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
python = Path('/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13')
catalog = root / 'representative.json'
worker = base / 'candidate/Lokad.Lython.Benchmarks.dll'
spec = importlib.util.spec_from_file_location('protocol', root / 'cpython-worker.py')
protocol = importlib.util.module_from_spec(spec); spec.loader.exec_module(protocol)
cases = {c['id']: c for c in json.loads(catalog.read_text())['cases']}
manifest_hash = hashlib.sha256(catalog.read_bytes()).hexdigest()
identity = json.loads((root / 'identity.json').read_text())
out = root / 'observations'; out.mkdir(exist_ok=False)
jobs=[('scripts.text-summary-helpers.large','warm'),('scripts.csv-summary-functions.large','warm')]
assert len(jobs)==2

env = os.environ.copy(); env['DOTNET_ROOT'] = str(dotnet.parent)
env['TMPDIR']='/dev/shm';tempfile.tempdir='/dev/shm'
assert os.stat('/dev/shm').st_dev != os.stat('/tmp').st_dev
assert not any(k.startswith(('DOTNET_', 'COMPlus_')) and k != 'DOTNET_ROOT' for k in env)

cpu_frequency = os.sysconf('SC_CLK_TCK')
def process_cpu_ticks(pid):
    text=Path(f'/proc/{pid}/stat').read_text()
    fields=text[text.rfind(')')+2:].split()
    return int(fields[11])+int(fields[12])

class Worker:
    def __init__(self, label, case, lane, stack):
        self.label, self.case, self.lane, self.sequence = label, case, lane, 0
        self.folder = Path(tempfile.mkdtemp(prefix='lython-comparison-files-'))
        self.errors = tempfile.TemporaryFile(); self.process = None
        stack.callback(self.close)
        command = ([str(python), '-I', '-S', '-X', 'utf8', str(root/'cpython-worker.py'), '--catalog', str(catalog)]
                   if label == 'CPython' else [str(dotnet), str(base/('reference' if label == 'reference' else 'candidate')/'Lokad.Lython.Benchmarks.dll'), '--compare', 'worker', '--catalog', str(catalog)])
        self.process = subprocess.Popen(command, cwd=self.folder, env=env, stdin=subprocess.PIPE,
                                        stdout=subprocess.PIPE, stderr=self.errors)
        self.identity = protocol.read_frame(self.process.stdout)
        if self.identity is None:
            self.errors.seek(0)
            (out / f'startup-{case["id"]}-{lane}-{label}.stderr').write_bytes(self.errors.read())
            raise RuntimeError('Worker exited before Ready; stderr preserved')
        assert self.identity['status'] == 'Ready' and self.identity['catalogSha256'] == manifest_hash
        assert self.identity['protocolVersion'] == 2 and self.identity['timingBoundary'].startswith('command-v2:')
        if label != 'CPython':
            core = next(v for v in self.identity['libraries'] if Path(v['path']).name == 'Lokad.Lython.dll')
            assert core['sha256'].lower() == identity['coreSHA256'][label].lower()
            assert not self.identity['serverGc'] and not any(self.identity['runtimeOverrides'].values())
        else:
            assert self.identity['gcEnabled'] and self.identity['gilEnabled']
            assert not self.identity['freeThreaded'] and not self.identity['debug']
            assert self.identity['executableSha256'] == hashlib.sha256(python.read_bytes()).hexdigest()
        self.request('verify', 2)
    def request(self, operation, count):
        self.sequence += 1
        request = {k:self.case[k] for k in ['sourceSha256','fixtureSha256','expectedOutputSha256']}
        request.update(protocolVersion=protocol.PROTOCOL_VERSION, requestId=self.sequence, operation=operation,
                       caseId=self.case['id'], lane=self.lane, iterations=count)
        before_cpu=process_cpu_ticks(self.process.pid) if operation == 'batch' else 0
        protocol.write_frame(self.process.stdin, request)
        response = protocol.read_frame(self.process.stdout)
        if operation == 'batch': self.last_cpu_ticks=process_cpu_ticks(self.process.pid)-before_cpu
        assert response['requestId'] == self.sequence and response['caseId'] == self.case['id']
        assert response['status'] == ('Equivalent' if operation == 'verify' else 'Completed'), response
        assert response['completedInvocations'] == count
        for k in ['sourceSha256','fixtureSha256','expectedOutputSha256']: assert response[k] == self.case[k]
        compile_batch = self.lane == 'compile' and operation == 'batch'
        assert response.get('actualOutputSha256') == (None if compile_batch else self.case['expectedOutputSha256'])
        assert response.get('actualFilesSha256') == (None if compile_batch else self.case.get('expectedFilesSha256'))
        return response
    def close(self):
        try:
            if self.process is not None:
                if self.process.poll() is None:
                    self.sequence += 1
                    protocol.write_frame(self.process.stdin, dict(protocolVersion=protocol.PROTOCOL_VERSION, requestId=self.sequence, operation='quit'))
                    assert protocol.read_frame(self.process.stdout)['status'] == 'Closed'
                code = self.process.wait(timeout=5)
                self.errors.seek(0); errors = self.errors.read()
                if errors: (out / f'{self.case["id"]}-{self.lane}-{self.label}.stderr').write_bytes(errors)
                assert code == 0, code
                assert not errors
        finally:
            if self.process is not None:
                if self.process.poll() is None: self.process.kill(); self.process.wait()
                self.process.stdin.close(); self.process.stdout.close()
            self.errors.close()
            assert self.folder.parent == Path(tempfile.gettempdir()) and self.folder.name.startswith('lython-comparison-files-')
            shutil.rmtree(self.folder)


import datetime, signal, tarfile
collector = Path('/home/lokad/lython-bench-checks/foundation-diagnostics/bin/Release/net11.0/Diagnostics.dll')
clock_ticks = os.sysconf('SC_CLK_TCK')
def threads(pid):
    result={}
    for task in Path(f'/proc/{pid}/task').iterdir():
        try:
            value=(task/'stat').read_text(); fields=value[value.rfind(')')+2:].split()
            result[task.name]=dict(name=(task/'comm').read_text().strip(),cpu=(int(fields[11])+int(fields[12]))/clock_ticks)
        except OSError: pass
    return result

def interrupt(signum, frame): raise InterruptedError(f'collection signal {signum}')
signal.signal(signal.SIGTERM, interrupt)
record=dict(complete=False,source=identity['sourceRevision'],coreSHA256=identity['coreSHA256']['candidate'],
            workerSHA256=identity['auxiliaryFiles']['Lokad.Lython.Benchmarks.dll'],
            performanceQualified=False,captureBoundary='Warm complete checked jobs, actual named guest thread allocation ticks; all-thread totals retained offline. Not exact allocation/CPU/retained memory.',
            startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),jobs=[])

def save(): (out/'record.json').write_text(json.dumps(record,indent=2)+'\n')
archive_path=root/'raw-capture.tgz'
with (root.parent/'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
    assert shutil.disk_usage(root).free>24*1024*1024, '8MiB archive allowance plus16MiB free guard'
    assert hashlib.sha256((base/'core/Lokad.Lython.dll').read_bytes()).hexdigest()==identity['coreSHA256']['candidate']
    for name,digest in identity['auxiliaryFiles'].items():assert hashlib.sha256((base/'candidate'/name).read_bytes()).hexdigest()==digest
    assert collector.is_file()
    assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
    assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[D]iagnostics.dll collect'],capture_output=True).returncode==1
    time.sleep(5)
    subprocess.run([str(dotnet),str(worker),'--compare','check-machine','--out',str(out/'machine-before.json')],env=env,check=True)
    assert json.loads((out/'machine-before.json').read_text())['gate']['quiet']
    trace_root=Path(tempfile.mkdtemp(prefix='lython-b345-traces-',dir='/dev/shm'))
    record['collectorSHA256']=hashlib.sha256(collector.read_bytes()).hexdigest()
    record['initialPersistentFreeBytes']=shutil.disk_usage(root).free
    started=time.monotonic();capture_seconds=0;failure=None;archived=False
    save()
    try:
        for case_id,lane in jobs:
            assert time.monotonic()-started<50, 'Reserve owned cleanup/archive headroom'
            name='text-warm' if '.text-' in case_id else 'csv-warm'
            folder=out/name;folder.mkdir();traces=trace_root/name;traces.mkdir()
            observation=dict(case=case_id,lane=lane,count=256,performanceQualified=False,traceRelativePath=f'eventpipe/{name}/profile.nettrace')
            profiler=None;profiler_log=None
            with contextlib.ExitStack() as stack:
                w=Worker('candidate',cases[case_id],lane,stack)
                observation['identity']=w.identity
                try:
                    observation['warmups']=[];warm_end=time.monotonic()+5
                    while time.monotonic()<warm_end:observation['warmups'].append(w.request('batch',32))
                    time.sleep(2)
                    profiler_log=(folder/'collector.log').open('w')
                    profiler=subprocess.Popen([str(dotnet),str(collector),'collect',str(w.process.pid),str(traces/'profile.nettrace'),str(folder/'ready'),str(folder/'stop')],env=env,stdout=profiler_log,stderr=subprocess.STDOUT)
                    deadline=time.monotonic()+8
                    while not (folder/'ready').exists():
                        if profiler.poll() is not None or time.monotonic()>deadline:raise RuntimeError('Actual collector readiness not observed')
                        time.sleep(.01)
                    before=threads(w.process.pid)
                    observation['startedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
                    observation['response']=w.request('batch',256)
                    observation['stoppedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
                    after=threads(w.process.pid)
                    observation['threadCpuSeconds']=[dict(tid=tid,name=after.get(tid,before.get(tid))['name'],seconds=(after[tid]['cpu']-before.get(tid,dict(cpu=0))['cpu']) if tid in after else None) for tid in sorted(set(before)|set(after))]
                    guest=[row['tid'] for row in observation['threadCpuSeconds'] if row['name'].startswith('Lython executio')]
                    assert guest, 'No actual named execution thread'
                    observation['guestTids']=guest
                    if len(guest)==1:observation['guestTid']=guest[0]
                    seconds=observation['response']['elapsedTicks']/observation['response']['clockFrequency']
                    observation['captureSeconds']=seconds;capture_seconds+=seconds
                    (folder/'stop').touch();profiler.wait(timeout=12);assert profiler.returncode==0
                    observation['verificationAfter']=w.request('verify',2)
                    print(name,'captured',seconds,'guestTids',guest,flush=True)
                finally:
                    (folder/'stop').touch()
                    if profiler is not None and profiler.poll() is None:
                        try:profiler.wait(timeout=5)
                        except subprocess.TimeoutExpired:profiler.kill();profiler.wait()
                    if profiler_log is not None:profiler_log.close()
                    (folder/'record.json').write_text(json.dumps(observation,indent=2)+'\n')
            observation['workersJoinedAndFixturesRemoved']=True
            (folder/'record.json').write_text(json.dumps(observation,indent=2)+'\n')
            record['jobs'].append(observation);record['totalCaptureSeconds']=capture_seconds;save()
            assert capture_seconds<=5, 'Capture exceeded planned bound; preserve uncertainty'
        assert len(record['jobs'])==2
        record['complete']=True
    except BaseException as error:
        failure=error;record['error']=repr(error)
    finally:
        assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
        assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[D]iagnostics.dll collect'],capture_output=True).returncode==1
        time.sleep(5)
        subprocess.run([str(dotnet),str(worker),'--compare','check-machine','--out',str(out/'machine-after.json')],env=env,check=True)
        record['quietAfter']=json.loads((out/'machine-after.json').read_text())['gate']['quiet']
        record['elapsedSeconds']=time.monotonic()-started
        record['finishedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
        raw_hashes={f'eventpipe/{p.relative_to(trace_root).as_posix()}':hashlib.sha256(p.read_bytes()).hexdigest() for p in trace_root.rglob('*') if p.is_file()}
        record['rawTraceSHA256']=raw_hashes;save()
        # Archive and verify raw RAM evidence before the collection lease ends.
        with tarfile.open(archive_path,'w:gz') as a:
            a.add(trace_root,arcname='eventpipe');a.add(out,arcname='observations')
            for name in ['identity.json','representative.json','collect.py','cpython-worker.py','contract.md','run.sh']:a.add(root/name,arcname=name)
        with tarfile.open(archive_path) as a:
            for name,digest in raw_hashes.items():assert hashlib.sha256(a.extractfile(name).read()).hexdigest()==digest
        receipt=dict(complete=record['complete'],jobs=len(record['jobs']),totalCaptureSeconds=capture_seconds,elapsedSeconds=record['elapsedSeconds'],allWorkersJoined=True,guestFixtureDirectories=[],rawTraceSHA256=raw_hashes,archiveSHA256=hashlib.sha256(archive_path.read_bytes()).hexdigest(),archiveBytes=archive_path.stat().st_size,persistentFreeBytes=shutil.disk_usage(root).free,quietBeforeAndAfter=record['quietAfter'],rawArchivedBeforeLeaseRelease=True)
        (root/'archive-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt),flush=True)
        archived=True
        if archived:shutil.rmtree(trace_root)
        assert archive_path.stat().st_size<=8*1024*1024 and shutil.disk_usage(root).free>16*1024*1024
    if failure is not None:raise failure
    assert record['quietAfter']
