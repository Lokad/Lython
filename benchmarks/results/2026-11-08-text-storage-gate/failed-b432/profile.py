from pathlib import Path
import contextlib,datetime,hashlib,importlib.util,json,os,select,shutil,subprocess,tempfile,time

root=Path('/home/lokad/lython-bench-checks/text-allocation-b432-20261108')
base=root/'candidate'
dotnet=Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
catalog=root/'catalog.json'
spec=importlib.util.spec_from_file_location('protocol',root/'cpython-worker.py')
protocol=importlib.util.module_from_spec(spec);spec.loader.exec_module(protocol)
env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['TMPDIR']=str(root/'guest-fixtures');tempfile.tempdir=str(root/'guest-fixtures');assert os.stat(root/'guest-fixtures').st_dev!=os.stat('/tmp').st_dev
assert not any(k.startswith(('DOTNET_','COMPlus_')) and k!='DOTNET_ROOT' for k in env)
identity=json.loads((root/'identity.json').read_text())
frequency=os.sysconf('SC_CLK_TCK')
def utc():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def threads(pid):
    rows={}
    for task in Path(f'/proc/{pid}/task').iterdir():
        try:
            text=(task/'stat').read_text();fields=text[text.rfind(')')+2:].split()
            rows[int(task.name)]=dict(name=(task/'comm').read_text().strip(),cpuTicks=int(fields[11])+int(fields[12]))
        except OSError:pass
    return rows

records=[]
for case in json.loads(catalog.read_text())['cases']:
    folder=root/('text' if 'text-summary' in case['id'] else 'ordinary')
    folder.mkdir()
    with tempfile.TemporaryDirectory(prefix='lython-comparison-files-') as fixture, (folder/'worker.stderr').open('w') as errors, (folder/'collector.stderr').open('w') as collector_errors:
        worker=subprocess.Popen([str(dotnet),str(base/'Lokad.Lython.Benchmarks.dll'),'--compare','worker','--catalog',str(catalog)],cwd=fixture,env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=errors)
        collector=None;sequence=0;closed=False;collector_joined=False
        def request(operation,count):
            global sequence
            sequence+=1
            message={k:case[k] for k in ('sourceSha256','fixtureSha256','expectedOutputSha256')}
            message.update(protocolVersion=2,requestId=sequence,operation=operation,caseId=case['id'],lane='warm',iterations=count)
            protocol.write_frame(worker.stdin,message);response=protocol.read_frame(worker.stdout)
            assert response['requestId']==sequence
            if operation=='quit':assert response['status']=='Closed'
            else:
                assert response['status']==('Equivalent' if operation=='verify' else 'Completed'),response
                assert response['completedInvocations']==count
                assert response['actualOutputSha256']==case['expectedOutputSha256']
                assert response['actualFilesSha256']==case['expectedFilesSha256']
            return response
        try:
            ready=protocol.read_frame(worker.stdout)
            assert ready['status']=='Ready' and ready['catalogSha256']==hashlib.sha256(catalog.read_bytes()).hexdigest()
            core=next(v for v in ready['libraries'] if Path(v['path']).name=='Lokad.Lython.dll')
            assert core['sha256'].lower()==identity['coreSHA256']
            assert all(next(v for v in ready['libraries'] if Path(v['path']).name==name)['sha256'].lower()==digest for name,digest in identity['auxiliaryFiles'].items())
            assert not ready['serverGc'] and not any(ready['runtimeOverrides'].values())
            record=dict(case=case['id'],ready=ready,verificationBefore=request('verify',2),captureInvocations=1024,cpuTickFrequency=frequency)
            elapsed=0.0
            for _ in range(128):
                response=request('batch',32)
                elapsed+=response['elapsedTicks']/response['clockFrequency']
                if elapsed>=6:break
            else:raise RuntimeError('Bounded warmup did not reach6s')
            record['warmupGuestSeconds']=elapsed
            time.sleep(2)
            collector=subprocess.Popen([str(dotnet),str(root/'collector/collector.dll'),str(worker.pid),str(folder)],env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=collector_errors)
            if not select.select([collector.stdout],[],[],10)[0]:raise TimeoutError('collector attachment')
            attached=json.loads(collector.stdout.readline());assert attached['status']=='Attached' and attached['targetPid']==worker.pid
            record['collectorReady']=attached
            before=threads(worker.pid);record['threadsBefore']=before
            record['captureBeginUtc']=utc()
            record['captureResponse']=request('batch',1024)
            record['captureEndUtc']=utc()
            after=threads(worker.pid);record['threadsAfter']=after
            candidates=[tid for tid in after if after[tid]['name'].startswith('Lython execut') and after[tid]['cpuTicks']>before.get(tid,dict(cpuTicks=0))['cpuTicks']];assert candidates,after
            guest=max(candidates,key=lambda tid:after[tid]['cpuTicks']-before.get(tid,dict(cpuTicks=0))['cpuTicks'])
            record['guestThreadId']=guest
            record['guestThreadCpuSeconds']=(after[guest]['cpuTicks']-before.get(guest,dict(cpuTicks=0))['cpuTicks'])/frequency
            collector.stdin.write(b'stop\n');collector.stdin.flush()
            assert collector.wait(timeout=15)==0
            collector_joined=True
            record['collector']=json.loads((folder/'collector.json').read_text())
            assert record['collector']['allocationTicks']>0 and record['collector']['eventsLost']==0
            record['verificationAfter']=request('verify',2)
            request('quit',0);assert worker.wait(timeout=10)==0;closed=True
            record.update(workerClosedAndJoined=True,collectorJoined=True,performanceQualified=False)
            records.append(record)
            (folder/'record.json').write_text(json.dumps(record,indent=2)+'\n')
        finally:
            if collector is not None:
                if collector.poll() is None:
                    try:collector.stdin.write(b'stop\n');collector.stdin.flush();collector.wait(timeout=10)
                    except (OSError,subprocess.TimeoutExpired):collector.kill();collector.wait()
                collector.stdin.close();collector.stdout.close()
            if worker.poll() is None:
                try:request('quit',0);worker.wait(timeout=10)
                except Exception:worker.kill();worker.wait()
            worker.stdin.close();worker.stdout.close()
        assert closed and collector_joined
    assert not (folder/'worker.stderr').read_text() and not (folder/'collector.stderr').read_text()
    assert not Path(fixture).exists()
    print(case['id'],'complete',flush=True)
(root/'records.json').write_text(json.dumps(records,indent=2)+'\n')
