"""One bounded confirmation through unchanged comparison workers/protocol."""
import contextlib, fcntl, hashlib, importlib.util, json, math, os, shutil
import statistics, subprocess, tempfile, time
from pathlib import Path

root = Path('/home/lokad/lython-bench-checks/shared-driver-checkpoint-b163-20261103')
dotnet = Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
python = Path('/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13')
catalog = root / 'representative.json'
worker = root / 'candidate/Lokad.Lython.Benchmarks.dll'
spec = importlib.util.spec_from_file_location('protocol', root / 'cpython-worker.py')
protocol = importlib.util.module_from_spec(spec); spec.loader.exec_module(protocol)
cases = {c['id']: c for c in json.loads(catalog.read_text())['cases']}
manifest_hash = hashlib.sha256(catalog.read_bytes()).hexdigest()
identity = json.loads((root / 'identity.json').read_text())
out = root / 'confirmation'; out.mkdir(exist_ok=False)
jobs = [('scripts.csv-summary-helpers.large', 'warm'), ('scripts.csv-summary-helpers.large', 'compile-run'), ('scripts.text-summary-helpers.large', 'warm'), ('scripts.text-summary-helpers.large', 'compile-run'), ('scripts.json-summary-helpers.large', 'warm'), ('scripts.json-summary-helpers.large', 'compile-run'), ('scripts.report-render.large', 'warm'), ('scripts.report-render.large', 'compile-run'), ('scripts.csv-summary-helpers.large', 'compile'), ('scripts.report-render.large', 'compile')]

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
                   if label == 'CPython' else [str(dotnet), str(root/('candidate' if label == 'candidate' else 'legacy' if label.startswith('legacy') else 'baseline')/'Lokad.Lython.Benchmarks.dll'), '--compare', 'worker', '--catalog', str(catalog)])
        self.process = subprocess.Popen(command, cwd=self.folder, env=env, stdin=subprocess.PIPE,
                                        stdout=subprocess.PIPE, stderr=self.errors)
        self.identity = protocol.read_frame(self.process.stdout)
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
                assert self.process.wait(timeout=5) == 0
                self.errors.seek(0); assert not self.errors.read()
        finally:
            if self.process is not None:
                if self.process.poll() is None: self.process.kill(); self.process.wait()
                self.process.stdin.close(); self.process.stdout.close()
            self.errors.close()
            assert self.folder.parent == Path(tempfile.gettempdir()) and self.folder.name.startswith('lython-comparison-files-')
            shutil.rmtree(self.folder)

with (root.parent/'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease, fcntl.LOCK_EX|fcntl.LOCK_NB)
    subprocess.run([str(dotnet),str(worker),'--compare','check-machine','--out',str(out/'machine-before.json')],env=env,check=True)
    assert json.loads((out/'machine-before.json').read_text())['gate']['quiet']
    (out/'fixture-filesystem.txt').write_text(subprocess.check_output(['findmnt','-T','/dev/shm','-o','TARGET,SOURCE,FSTYPE,OPTIONS'],text=True))
    started = time.monotonic()
    record = dict(sourceRevision=identity['sourceRevision'], purpose='B163 current b087c7b8 synchronous execution versus CPython; four complete scripts, warm/fresh plus two compilation-only cases; same-source Lython control; RAM filesystem; not a source-pair speedup claim',
                  warmupSeconds=3, targetBatchSeconds=.20, rounds=9, jobs=[], complete=False)
    def save(): (out/'confirmation.json').write_text(json.dumps(record,indent=2)+'\n')
    save()
    for job_index,(case_id,lane) in enumerate(jobs):
        assert time.monotonic()-started < 300, 'Reserve40 seconds for the final owned job/cleanup.'
        with contextlib.ExitStack() as stack:
            labels=['before-control','candidate','CPython']
            if job_index >= 4: labels.reverse()
            workers = [Worker(label,cases[case_id],lane,stack) for label in labels]
            counts=[]; warmups=[]
            for w in workers:
                count=1; end=time.monotonic()+3; rows=[]
                while time.monotonic()<end:
                    response=w.request('batch',count); rows.append(response)
                    seconds=response['elapsedTicks']/response['clockFrequency']
                    count=max(1,min(100000,math.ceil(count*.20/max(seconds,.000001))))
                counts.append(count); warmups.append(rows)
            time.sleep(1)
            samples=[]
            for round in range(9):
                for offset in range(len(workers)):
                    i=(round+offset)%len(workers) if round%2==0 else (round+len(workers)-offset)%len(workers)
                    response=workers[i].request('batch',counts[i])
                    samples.append(dict(round=round,engine=workers[i].label,response=response,externalProcessCpuTicks=workers[i].last_cpu_ticks,externalCpuFrequency=cpu_frequency))
            observation=dict(block=0,case=case_id,lane=lane,identities={w.label:w.identity for w in workers},
                             counts=counts,warmups=warmups,samples=samples)
        observation['workersJoinedAndFixturesRemoved']=True
        record['jobs'].append(observation); save(); print(lane,case_id,'complete',flush=True)
    subprocess.run([str(dotnet),str(worker),'--compare','check-machine','--out',str(out/'machine-after.json')],env=env,check=True)
    assert json.loads((out/'machine-after.json').read_text())['gate']['quiet']
    record.update(complete=True,elapsedSeconds=time.monotonic()-started); save()
