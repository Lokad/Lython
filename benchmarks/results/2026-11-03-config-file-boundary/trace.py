import fcntl,hashlib,importlib.util,json,os,shutil,subprocess,tempfile,time
from pathlib import Path
root=Path('/home/lokad/lython-bench-checks/config-boundary-b155-20261103')
images=root.parent/'json-iterator-storage-b154-20261103'
dotnet=Path('/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet')
python=Path('/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13')
catalog=images/'representative.json'
spec=importlib.util.spec_from_file_location('protocol',images/'cpython-worker.py');protocol=importlib.util.module_from_spec(spec);spec.loader.exec_module(protocol)
case=next(c for c in json.loads(catalog.read_text())['cases'] if c['id']=='scripts.config-edit.large')
identity=json.loads((images/'identity.json').read_text());manifest=hashlib.sha256(catalog.read_bytes()).hexdigest()
env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent)
out=root/'results';out.mkdir(exist_ok=False);records=[]
with (root.parent/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 subprocess.run([str(dotnet),str(images/'candidate/Lokad.Lython.Benchmarks.dll'),'--compare','check-machine','--out',str(out/'machine-before.json')],env=env,check=True)
 assert json.loads((out/'machine-before.json').read_text())['gate']['quiet']
 for label in ['baseline','candidate','CPython']:
  fixture=Path(tempfile.mkdtemp(prefix='lython-comparison-files-'));errors=tempfile.TemporaryFile();worker=None;trace=None;tracer=0
  record={'label':label,'performanceQualified':False,'purpose':'B155 warm config syscall origins; traced elapsed is not a performance comparison'};seq=0
  def request(operation,count=2):
   global seq
   seq+=1
   message={k:case[k] for k in ['sourceSha256','fixtureSha256','expectedOutputSha256']}
   message.update(protocolVersion=2,requestId=seq,operation=operation,caseId=case['id'],lane='warm',iterations=count)
   protocol.write_frame(worker.stdin,message);response=protocol.read_frame(worker.stdout)
   assert response['status']==('Closed' if operation=='quit' else 'Equivalent' if operation=='verify' else 'Completed'),response
   if operation!='quit':assert response['actualOutputSha256']==case['expectedOutputSha256'] and response['actualFilesSha256']==case['expectedFilesSha256']
   return response
  try:
   command=[str(python),'-I','-S','-X','utf8',str(images/'cpython-worker.py'),'--catalog',str(catalog)] if label=='CPython' else [str(dotnet),str(images/label/'Lokad.Lython.Benchmarks.dll'),'--compare','worker','--catalog',str(catalog)]
   worker=subprocess.Popen(command,cwd=fixture,env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=errors)
   record['identity']=protocol.read_frame(worker.stdout);assert record['identity']['catalogSha256']==manifest
   if label!='CPython':assert next(v for v in record['identity']['libraries'] if Path(v['path']).name=='Lokad.Lython.dll')['sha256'].lower()==identity['coreSHA256'][label]
   record['verificationBefore']=request('verify');end=time.monotonic()+1
   while time.monotonic()<end:request('batch',32)
   time.sleep(.2)
   trace=subprocess.Popen(['sudo','-n','strace','-f','-ttt','-T','-s','80','-e','trace=%file,read,write,pread64,pwrite64,ftruncate,fsync,fdatasync,flock,fcntl,futex,clock_nanosleep,lseek,getdents64','-o',str(out/(label+'.strace')),'-p',str(worker.pid)],stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
   for attempt in range(200):
    tracer=int(next(l for l in Path(f'/proc/{worker.pid}/status').read_text().splitlines() if l.startswith('TracerPid:')).split()[1])
    if tracer:break
    assert trace.poll() is None
    time.sleep(.01)
   assert tracer
   record['tracerPid']=tracer;record['response']=request('batch',10)
   subprocess.run(['sudo','-n','kill','-INT',str(tracer)],check=True);trace.wait(timeout=5);tracer=0
   record['tracerExitCode']=trace.returncode;record['tracerStderr']=trace.stderr.read().decode();assert trace.returncode in (0,130,-2)
   record['verificationAfter']=request('verify');record['closed']=request('quit');assert worker.wait(timeout=5)==0
   errors.seek(0);assert not errors.read()
  finally:
   if tracer:subprocess.run(['sudo','-n','kill','-INT',str(tracer)],check=False)
   if trace is not None and trace.poll() is None:trace.wait(timeout=5)
   if worker is not None and worker.poll() is None:
    try:record['closedInFinally']=request('quit');worker.wait(timeout=5)
    except Exception:worker.kill();worker.wait()
   if worker is not None:worker.stdin.close();worker.stdout.close()
   (out/(label+'-state.json')).write_text(json.dumps(record,indent=2)+'\n');errors.close();assert fixture.parent==Path(tempfile.gettempdir()) and fixture.name.startswith('lython-comparison-files-');shutil.rmtree(fixture)
  record['workerJoinedAndFixtureRemoved']=True;records.append(record);(out/'observations.json').write_text(json.dumps(records,indent=2)+'\n')
 subprocess.run([str(dotnet),str(images/'candidate/Lokad.Lython.Benchmarks.dll'),'--compare','check-machine','--out',str(out/'machine-after.json')],env=env,check=True)
 assert json.loads((out/'machine-after.json').read_text())['gate']['quiet']
print('All three traced workers joined and fixtures removed; no performance comparison',flush=True)
