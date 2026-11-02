"""One bounded confirmation through unchanged comparison workers/protocol."""
import contextlib, fcntl, hashlib, importlib.util, json, math, os, shutil
import statistics, subprocess, tempfile, time
from pathlib import Path

root = Path('/home/lokad/lython-bench-checks/cpython-checkpoint-b125-20261102')
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
jobs = [('scripts.csv-summary-helpers.large', 'compile-run'), ('scripts.csv-summary-helpers.large', 'warm'), ('scripts.json-summary-helpers.large', 'compile-run'), ('scripts.json-summary-helpers.large', 'warm'), ('scripts.text-summary-helpers.large', 'compile-run'), ('scripts.text-summary-helpers.large', 'warm'), ('scripts.file-search.large', 'compile-run'), ('scripts.file-search.large', 'warm'), ('scripts.report-render.large', 'compile-run'), ('scripts.report-render.large', 'warm'), ('scripts.csv-summary-helpers.large', 'compile'), ('scripts.report-render.large', 'compile')]

env = os.environ.copy(); env['DOTNET_ROOT'] = str(dotnet.parent)
assert not any(k.startswith(('DOTNET_', 'COMPlus_')) and k != 'DOTNET_ROOT' for k in env)

class Worker:
    def __init__(self, label, case, lane, stack):
        self.label, self.case, self.lane, self.sequence = label, case, lane, 0
        self.folder = Path(tempfile.mkdtemp(prefix='lython-comparison-files-'))
        self.errors = tempfile.TemporaryFile(); self.process = None
        stack.callback(self.close)
        command = ([str(python), '-I', '-S', '-X', 'utf8', str(root/'cpython-worker.py'), '--catalog', str(catalog)]
                   if label == 'CPython' else [str(dotnet), str(worker), '--compare', 'worker', '--catalog', str(catalog)])
        self.process = subprocess.Popen(command, cwd=self.folder, env=env, stdin=subprocess.PIPE,
                                        stdout=subprocess.PIPE, stderr=self.errors)
        self.identity = protocol.read_frame(self.process.stdout)
        assert self.identity['status'] == 'Ready' and self.identity['catalogSha256'] == manifest_hash
        if label != 'CPython':
            core = next(v for v in self.identity['libraries'] if Path(v['path']).name == 'Lokad.Lython.dll')
            assert core['sha256'].lower() == identity['files']['Lokad.Lython.dll'].lower()
            assert not self.identity['serverGc'] and not any(self.identity['runtimeOverrides'].values())
        else:
            assert self.identity['gcEnabled'] and self.identity['gilEnabled']
            assert not self.identity['freeThreaded'] and not self.identity['debug']
            assert self.identity['executableSha256'] == hashlib.sha256(python.read_bytes()).hexdigest()
        self.request('verify', 2)
    def request(self, operation, count):
        self.sequence += 1
        request = {k:self.case[k] for k in ['sourceSha256','fixtureSha256','expectedOutputSha256']}
        request.update(protocolVersion=1, requestId=self.sequence, operation=operation,
                       caseId=self.case['id'], lane=self.lane, iterations=count)
        protocol.write_frame(self.process.stdin, request)
        response = protocol.read_frame(self.process.stdout)
        assert response['requestId'] == self.sequence and response['caseId'] == self.case['id']
        assert response['status'] == ('Equivalent' if operation == 'verify' else 'Completed'), response
        assert response['completedInvocations'] == count
        for k in ['sourceSha256','fixtureSha256','expectedOutputSha256']: assert response[k] == self.case[k]
        if self.lane == 'compile' and operation == 'batch':
            assert response['actualOutputSha256'] is None
            assert response.get('actualFilesSha256') is None
        else:
            assert response['actualOutputSha256'] == self.case['expectedOutputSha256']
            assert response['actualFilesSha256'] == self.case['expectedFilesSha256']
        return response
    def close(self):
        try:
            if self.process is not None:
                if self.process.poll() is None:
                    self.sequence += 1
                    protocol.write_frame(self.process.stdin, dict(protocolVersion=1, requestId=self.sequence, operation='quit'))
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
    started = time.monotonic()
    record = dict(sourceRevision=identity['sourceRevision'], purpose='B125 coherent five-workload checkpoint source90785737; fresh/warm plus CSV/report compile-only, no source speedup inference from independent medians',
                  warmupSeconds=4, targetBatchSeconds=.2, rounds=9, jobs=[], complete=False)
    def save(): (out/'confirmation.json').write_text(json.dumps(record,indent=2)+'\n')
    save()
    for case_id,lane in jobs:
        assert time.monotonic()-started < 250, 'Reserve40 seconds for the final owned job/cleanup.'
        with contextlib.ExitStack() as stack:
            workers = [Worker(label,cases[case_id],lane,stack) for label in ['baseline','candidate','CPython']]
            counts=[]; warmups=[]
            for w in workers:
                count=1; end=time.monotonic()+4; rows=[]
                while time.monotonic()<end:
                    response=w.request('batch',count); rows.append(response)
                    seconds=response['elapsedTicks']/response['clockFrequency']
                    count=max(1,min(100000,math.ceil(count*.2/max(seconds,.000001))))
                counts.append(count); warmups.append(rows)
            samples=[]
            for round in range(9):
                for offset in range(3):
                    i=(round+offset)%3 if round%2==0 else (round+3-offset)%3
                    samples.append(dict(round=round,engine=workers[i].label,response=workers[i].request('batch',counts[i])))
            observation=dict(case=case_id,lane=lane,identities={w.label:w.identity for w in workers},
                             counts=counts,warmups=warmups,samples=samples)
        observation['workersJoinedAndFixturesRemoved']=True
        record['jobs'].append(observation); save(); print(lane,case_id,'complete',flush=True)
    subprocess.run([str(dotnet),str(worker),'--compare','check-machine','--out',str(out/'machine-after.json')],env=env,check=True)
    assert json.loads((out/'machine-after.json').read_text())['gate']['quiet']
    record.update(complete=True,elapsedSeconds=time.monotonic()-started); save()
