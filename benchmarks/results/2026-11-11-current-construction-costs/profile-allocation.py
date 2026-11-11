from pathlib import Path
import datetime,hashlib,importlib.util,json,os,shutil,subprocess,threading,time
home=Path(__file__).parent;out=home/'allocation';out.mkdir()
spec=importlib.util.spec_from_file_location('existing_protocol',Path('benchmarks/cpython-worker.py').resolve())
protocol=importlib.util.module_from_spec(spec);spec.loader.exec_module(protocol)
catalog=(home/'catalog.json').resolve();worker=Path('D:\\code\\Lython\\.git\\agent-notes\\review-20261031\\native-loop-admission-r247\\images\\before\\Lokad.Lython.Benchmarks.dll')
collector=(home.parent/'activation-allocation-d36/collector/bin/Release/net11.0/collector.dll').resolve()
assert collector.is_file()
env=os.environ.copy();env['DOTNET_ROOT']='C:/Users/JoannesVermorel/.dotnet11'
assert not any(k.startswith(('DOTNET_','COMPlus_')) and k not in ['DOTNET_ROOT','DOTNET_HOST_PATH'] for k in env)
fixtures=out/'fixtures';fixtures.mkdir();env['TMP']=str(fixtures.resolve());env['TEMP']=str(fixtures.resolve())
dotnet=env['DOTNET_ROOT']+'/dotnet.exe';owned=[];timed_out=False
def terminate_owned():
    global timed_out
    timed_out=True
    for p in owned:
        if p.poll() is None:p.kill()
timer=threading.Timer(120,terminate_owned);timer.start();records=[]
utc=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
try:
    for label,case_id,lane in [('captured-text','scripts.text-summary-helpers.nonlocal-counter.large','warm'),('report-async','scripts.report-render.large','warm-async')]:
        case=next(c for c in json.loads(catalog.read_text(encoding='utf-8'))['cases'] if c['id']==case_id)
        folder=out/label;folder.mkdir();cwd=fixtures/('lython-comparison-files-'+label);cwd.mkdir()
        w=c=None;sequence=0;record=dict(case=case_id,performanceQualified=False,captureInvocations=256)
        with (folder/'worker.stderr').open('wb') as we,(folder/'collector.stderr').open('wb') as ce:
            try:
                w=subprocess.Popen([dotnet,str(worker),'--compare','worker','--catalog',str(catalog)],cwd=cwd,env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=we);owned.append(w)
                ready=protocol.read_frame(w.stdout);assert ready['status']=='Ready'
                assert not ready['serverGc'] and not any(ready['runtimeOverrides'].values())
                core=next(x for x in ready['libraries'] if Path(x['path']).name=='Lokad.Lython.dll')
                assert core['sha256']=='75a14f1399637646b21a0a12fcb08f8664c5c559c3a527859293083b6f54732e'
                assert ready['catalogSha256']==hashlib.sha256(catalog.read_bytes()).hexdigest();record['ready']=ready
                def request(operation,count):
                    global sequence
                    sequence+=1
                    message={k:case[k] for k in ['sourceSha256','fixtureSha256','expectedOutputSha256']}
                    message.update(protocolVersion=2,requestId=sequence,operation=operation,caseId=case_id,lane=lane,iterations=count)
                    protocol.write_frame(w.stdin,message);response=protocol.read_frame(w.stdout)
                    assert response['requestId']==sequence
                    if operation=='quit':assert response['status']=='Closed'
                    else:
                        assert response['status']==('Equivalent' if operation=='verify' else 'Completed'),response
                        assert response['completedInvocations']==count
                        assert response['actualOutputSha256']==case['expectedOutputSha256'] and response['actualFilesSha256']==case['expectedFilesSha256']
                    return response
                record['verificationBefore']=request('verify',2)
                record['warmup']=request('batch',256);time.sleep(2)
                c=subprocess.Popen([dotnet,str(collector),str(w.pid),str(folder.resolve())],env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=ce);owned.append(c)
                attached=json.loads(c.stdout.readline());assert attached['status']=='Attached' and attached['targetPid']==w.pid
                record['collectorReady']=attached
                record['captureBeginUtc']=utc();record['captureResponse']=request('batch',256);record['captureEndUtc']=utc()
                c.stdin.write(b'stop\n');c.stdin.flush();assert c.wait(timeout=15)==0
                record['collector']=json.loads((folder/'collector.json').read_text(encoding='utf-8'))
                assert record['collector']['allocationTicks']>0 and record['collector']['eventsLost']==0
                record['verificationAfter']=request('verify',2)
                request('quit',0);assert w.wait(timeout=10)==0
                record.update(allGoldensPassed=True,collectorJoined=True,workerClosedAndJoined=True)
            finally:
                if c is not None and c.poll() is None:
                    try:c.stdin.write(b'stop\n');c.stdin.flush();c.wait(timeout=10)
                    except (OSError,subprocess.TimeoutExpired):c.kill();c.wait()
                if w is not None and w.poll() is None:w.kill();w.wait()
                for p in [c,w]:
                    if p is not None:
                        p.stdin.close();p.stdout.close()
        assert not (folder/'worker.stderr').read_bytes() and not (folder/'collector.stderr').read_bytes()
        assert cwd.resolve().parent==fixtures.resolve() and cwd.name=='lython-comparison-files-'+label and not cwd.is_symlink()
        shutil.rmtree(cwd);record['ownedFixturesRemoved']=True
        (folder/'record.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8');records.append(record)
        print(label,'sampled',record['collector']['allocationTicks'],'allocation ticks; zero lost; goldens/Closed/joins/fixtures pass',flush=True)
finally:
    timer.cancel()
    for p in owned:
        if p.poll() is None:p.kill();p.wait()
assert not timed_out and not list(fixtures.iterdir())
receipt=dict(complete=True,records=records,collectorSHA256=hashlib.sha256(collector.read_bytes()).hexdigest(),purpose='Existing sampled allocation-tick diagnostic only; type denotes the allocation triggering a tick, not an exact type byte total. Includes full request reset/I/O/checks/prior-result cleanup, excludes warmup/compile/final teardown. Sampling is neither exact type allocation nor CPU attribution; no forced collection/rundown/root tracing.',platform='Windows x64/.NET11RC3, fully qualified8f862 source/frozen75a14, normal optional0.4; no VM timing comparison')
(out/'records.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
print('Both R248 current allocation profiles complete; no runtime prototype or timing claim.',flush=True)
