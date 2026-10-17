import datetime, fcntl, hashlib, json, os, pathlib, select, struct, subprocess, time, sys, tempfile, shutil
base = pathlib.Path('/home/lokad/lython-bench-checks')
role = sys.argv[1]
assert role == 'csv'
root = base / 'csv-warm-async-1b6a1531/source-candidate'
notes = base / 'csv-warm-async-1b6a1531' / 'profile'
notes.mkdir(exist_ok=False)
lease = open(base / 'comparison-vm.lock', 'w')
fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
sdk = pathlib.Path('/home/lokad/lython-bench-toolchains/dotnet/10.0.401')
assembly = root / 'benchmarks/Lokad.Lython.Benchmarks/bin/Release/net10.0/Lokad.Lython.Benchmarks.dll'
catalog = base / 'csv-warm-async-1b6a1531/catalog.json'
case = next(c for c in json.loads(catalog.read_text())['cases'] if c['id'] == 'scripts.csv-summary-helpers.large')
tracer_path = pathlib.Path('/home/lokad/lython-bench-toolchains/diagnostics/dotnet-trace/10.0.750501/dotnet-trace')
files = [assembly, assembly.with_suffix('.deps.json'), assembly.with_suffix('.runtimeconfig.json'),
         assembly.parent / 'Lokad.Lython.dll', sdk / 'dotnet', root / 'benchmarks/cpython-worker.py', catalog, tracer_path]
def hashes(): return {str(f): hashlib.sha256(f.read_bytes()).hexdigest() for f in files}
def source():
    paths = sorted(p for section in ('src/Lokad.Lython', 'benchmarks/Lokad.Lython.Benchmarks/Comparison')
                   for p in (root/section).rglob('*.cs') if not {'bin','obj'}.intersection(p.relative_to(root).parts))
    return dict(revision=subprocess.run(['git','-C',str(root),'rev-parse','HEAD'],capture_output=True,text=True,check=True).stdout.strip(),
                sourceHashes={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
result = dict(controllerPid=os.getpid(), diagnosticOnly=True, state='Running', started=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              sourceBefore=source(), filesBefore=hashes(), case=case, traceDurationSeconds=10, serviceHardCapSeconds=30)
assert result['sourceBefore']['revision'] == '1b6a15318b00ec056af02e23b72fa64f861964df'
def save():
    temp = notes / 'diagnostic.json.tmp'
    temp.write_text(json.dumps(result,indent=2)+'\n')
    temp.replace(notes / 'diagnostic.json')
save()
env = dict(os.environ, DOTNET_ROOT=str(sdk), DOTNET_NOLOGO='1', DOTNET_CLI_TELEMETRY_OPTOUT='1')
assert not any(k.startswith(('DOTNET_Tiered','COMPlus_Tiered','DOTNET_GC','COMPlus_GC')) for k in env)
worker_log = open(notes/'worker.stderr.log','wb')
trace_log = open(notes/'tracer.log','wb')
records = open(notes/'responses.jsonl','w')
worker = tracer = None
fixture_dir = pathlib.Path(tempfile.mkdtemp(prefix='lython-comparison-files-'))
result['fileFixtureDirectory'] = str(fixture_dir)
deadline = time.monotonic() + 27
try:
    worker = subprocess.Popen([str(sdk/'dotnet'),str(assembly),'--compare','worker','--catalog',str(catalog)],cwd=fixture_dir,env=env,
                              stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=worker_log,bufsize=0)
    result['workerPid'] = worker.pid
    def exact(n):
        data = bytearray()
        while len(data) < n:
            remaining = min(5, deadline-time.monotonic())
            if remaining <= 0 or not select.select([worker.stdout],[],[],remaining)[0]: raise TimeoutError('Protocol deadline')
            part = os.read(worker.stdout.fileno(),n-len(data))
            if not part: raise EOFError('Truncated protocol frame')
            data.extend(part)
        return data
    def receive():
        length=struct.unpack('>I',exact(4))[0]
        assert 0 < length <= 4*1024*1024
        return json.loads(exact(length))
    identity = receive()
    result['workerIdentity'] = identity
    save()
    assert identity['status']=='Ready' and identity['processId']==worker.pid
    request_id=0
    def request(operation,iterations=None):
        global request_id
        request_id+=1
        frame=dict(protocolVersion=1,requestId=request_id,operation=operation)
        if operation!='quit':
            frame.update({k:case[k] for k in ('sourceSha256','fixtureSha256','expectedOutputSha256')})
            frame['caseId']=case['id']
        if iterations is not None: frame.update(lane='warm-async',iterations=iterations)
        payload=json.dumps(frame,separators=(',',':')).encode()
        worker.stdin.write(struct.pack('>I',len(payload))+payload)
        answer=receive()
        records.write(json.dumps(dict(phase=phase,request=frame,response=answer))+'\n'); records.flush()
        assert answer['requestId']==request_id
        if operation!='quit':
            assert answer['status']==('Equivalent' if operation=='verify' else 'Completed'),answer
            assert answer['actualOutputSha256']==case['expectedOutputSha256']
            assert answer['completedInvocations']==(2 if operation=='verify' else iterations)
            assert answer['actualFilesSha256']==case['expectedFilesSha256']
        return answer
    phase='verify'; request('verify')
    phase='warmup'; start=time.monotonic()
    while time.monotonic()-start<2: request('batch',2)
    trace=notes/'csv.nettrace'
    command=['sudo','-n','/usr/bin/env','DOTNET_ROOT='+str(sdk),str(tracer_path),'collect-linux','--process-id',str(worker.pid),
             '--profile','cpu-sampling,dotnet-common','--duration','00:00:00:10','--max-memory','128','--output',str(trace)]
    result['tracerCommand']=command
    tracer=subprocess.Popen(command,cwd=root,env=env,stdout=trace_log,stderr=subprocess.STDOUT)
    result['tracerPid']=tracer.pid; save()
    phase='profile'; profile_start=time.monotonic()
    time.sleep(2)
    while tracer.poll() is None and time.monotonic()-profile_start<20: request('batch',2)
    result['tracerExitCode']=tracer.wait(timeout=2)
    result['profileWallSeconds']=time.monotonic()-profile_start
    phase='verify-after'; request('verify')
    phase='shutdown'; request('quit'); worker.stdin.close()
    result['workerExitCode']=worker.wait(timeout=2)
    subprocess.run(['sudo','-n','chown','lokad:lokad',str(trace)],check=True,timeout=2)
    result.update(sourceAfter=source(),filesAfter=hashes(),traceSha256=hashlib.sha256(trace.read_bytes()).hexdigest())
    assert result['filesBefore']==result['filesAfter'] and result['sourceBefore']==result['sourceAfter']
    assert result['workerExitCode']==0 and result['tracerExitCode']==0
    assert fixture_dir.resolve().parent == pathlib.Path(tempfile.gettempdir()).resolve() and fixture_dir.name.startswith('lython-comparison-files-')
    result['finalFileHashes']={str(p.relative_to(fixture_dir)):hashlib.sha256(p.read_bytes()).hexdigest() for p in fixture_dir.rglob('*') if p.is_file()}
    shutil.rmtree(fixture_dir)
    result['fixtureRemoved']=not fixture_dir.exists()
    result['state']='Completed'
except BaseException as error:
    result.update(state='Failed',error=repr(error))
    raise
finally:
    if worker is not None and worker.poll() is None:
        worker.kill(); worker.wait(timeout=2)
    if tracer is not None and tracer.poll() is None:
        tracer.terminate(); tracer.wait(timeout=2)
    result['workerStopped']=worker is None or worker.poll() is not None
    result['tracerStopped']=tracer is None or tracer.poll() is not None
    result['stopped']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    save(); records.close(); worker_log.close(); trace_log.close()
print('PROFILE_COMPLETED',result['profileWallSeconds'],'seconds')
