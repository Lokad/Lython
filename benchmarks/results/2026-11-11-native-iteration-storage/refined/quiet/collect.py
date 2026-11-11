"""Representative CPython delivery checkpoint through unchanged comparison workers/protocol."""
import contextlib, fcntl, hashlib, importlib.util, json, math, os, shutil
import statistics, subprocess, tempfile, time, signal
from pathlib import Path

root = Path('/home/lokad/lython-bench-checks/native-iteration-storage-b593-20261111')
dotnet = Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
python = Path('/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13')
catalog = root / 'representative.json'
worker = root / 'candidate/Lokad.Lython.Benchmarks.dll'
spec = importlib.util.spec_from_file_location('protocol', root / 'cpython-worker.py')
protocol = importlib.util.module_from_spec(spec); spec.loader.exec_module(protocol)
cases = {c['id']: c for c in json.loads(catalog.read_text())['cases']}
manifest_hash = hashlib.sha256(catalog.read_bytes()).hexdigest()
identity = json.loads((root / 'identity.json').read_text())
assert set(identity['coreSHA256']) == {'reference','reference-control','candidate','candidate-control'}
out = root / 'confirmation'; out.mkdir(exist_ok=False)
jobs=[['scripts.text-summary-helpers.nonlocal-counter.large', 'warm'], ['scripts.text-summary-helpers.nonlocal-counter.large', 'warm-async'], ['scripts.text-summary-helpers.large', 'warm'], ['scripts.text-summary-helpers.large', 'warm-async'], ['scripts.file-search.large', 'warm'], ['scripts.csv-validated-records.large', 'warm'], ['scripts.report-render.large', 'warm-async']]
assert len(jobs)==7 and all(case in cases for case,lane in jobs)

env = os.environ.copy(); env['DOTNET_ROOT'] = str(dotnet.parent)
owned_ram=root/'guest-fixtures';assert owned_ram.is_dir()
env['TMPDIR']=str(owned_ram);tempfile.tempdir=str(owned_ram)
assert os.stat(owned_ram).st_dev != os.stat('/tmp').st_dev
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
                   if label.startswith('CPython') else [str(dotnet), str(root/('reference' if label.startswith('reference') else 'candidate')/'Lokad.Lython.Benchmarks.dll'), '--compare', 'worker', '--catalog', str(catalog)])
        self.process = subprocess.Popen(command, cwd=self.folder, env=env, stdin=subprocess.PIPE,
                                        stdout=subprocess.PIPE, stderr=self.errors)
        self.identity = protocol.read_frame(self.process.stdout)
        if self.identity is None:
            self.errors.seek(0)
            (out / f'startup-{case["id"]}-{lane}-{label}.stderr').write_bytes(self.errors.read())
            raise RuntimeError('Worker exited before Ready; stderr preserved')
        assert self.identity['status'] == 'Ready' and self.identity['catalogSha256'] == manifest_hash
        assert self.identity['protocolVersion'] == 2 and self.identity['timingBoundary'].startswith('command-v2:')
        if not label.startswith('CPython'):
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

def stop(*_):raise InterruptedError('Owned checkpoint stopped; join all users before cleanup.')
signal.signal(signal.SIGTERM,stop)

with (root.parent/'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease, fcntl.LOCK_EX|fcntl.LOCK_NB)
    time.sleep(5)  # Settled pre-gate, outside all measurement clocks.
    subprocess.run([str(dotnet),str(worker),'--compare','check-machine','--out',str(out/'machine-before.json')],env=env,check=True)
    assert json.loads((out/'machine-before.json').read_text())['gate']['quiet']
    (out/'fixture-filesystem.txt').write_text(subprocess.check_output(['findmnt','-T',str(owned_ram),'-o','TARGET,SOURCE,FSTYPE,OPTIONS'],text=True))
    started = time.monotonic()
    record = dict(sourceRevision=identity['sourceRevision'], purpose='R249 refined unified native iteration storage. Captured/ordinary text both APIs plus search/CSV and reportAsync controls. Initial allocation reduction and adverse local elapsed results retained. Existing warm boundary with mediated RAM I/O/checks/prior-result cleanup; no CPython ratio',
                  warmupSeconds=5, targetBatchSeconds=.15, rounds=7, jobs=[], complete=False)
    def save():
        temporary=out/'confirmation.json.tmp';temporary.write_text(json.dumps(record,indent=2)+'\n');temporary.replace(out/'confirmation.json')
    save()
    for job_index,(case_id,lane) in enumerate(jobs):
        assert time.monotonic()-started < 170, 'Reserve40seconds for final owned work/cleanup.'
        with contextlib.ExitStack() as stack:
            labels=['reference','candidate','reference-control','candidate-control']
            if job_index >= 2: labels.reverse()
            workers = [Worker(label,cases[case_id],lane,stack) for label in labels]
            counts=[]; warmups=[]
            for w in workers:
                count=1; end=time.monotonic()+5; rows=[]
                while time.monotonic()<end:
                    response=w.request('batch',count); rows.append(response)
                    seconds=response['elapsedTicks']/response['clockFrequency']
                    count=max(1,min(100000,math.ceil(count*.15/max(seconds,.000001))))
                counts.append(count); warmups.append(rows)
            time.sleep(1)
            samples=[]
            for round in range(7):
                for offset in range(len(workers)):
                    i=(round+offset)%len(workers) if round%2==0 else (round+len(workers)-offset)%len(workers)
                    response=workers[i].request('batch',counts[i])
                    samples.append(dict(round=round,engine=workers[i].label,response=response,externalProcessCpuTicks=workers[i].last_cpu_ticks,externalCpuFrequency=cpu_frequency))
            observation=dict(block=0,case=case_id,lane=lane,identities={w.label:w.identity for w in workers},
                             counts=counts,warmups=warmups,samples=samples)
        observation['workersJoinedAndFixturesRemoved']=True
        record['jobs'].append(observation); save(); print(lane,case_id,'complete',flush=True)
    time.sleep(5)  # Outside clocks; settle after all worker/process cleanup.
    subprocess.run([str(dotnet),str(worker),'--compare','check-machine','--out',str(out/'machine-after.json')],env=env,check=True)
    assert json.loads((out/'machine-after.json').read_text())['gate']['quiet']
    record.update(complete=True,elapsedSeconds=time.monotonic()-started); save()

    assert not list(owned_ram.iterdir());subprocess.run(['sudo','-n','umount',str(owned_ram)],check=True);owned_ram.rmdir()
    subprocess.run(["python3",str(root/"archive-owned.py")],check=True)
