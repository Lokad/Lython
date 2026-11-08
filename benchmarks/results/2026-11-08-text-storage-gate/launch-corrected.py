from pathlib import Path
import datetime,hashlib,json,shlex,subprocess,tarfile
root=Path(__file__).resolve().parent
reclaims=[('text-cost-b419-20261107','text-cost-r63'),('returned-record-b422-20261107','returned-record-calls-r69')]
for vm,local in reclaims:
 p=root.parent/local;receipt=json.loads((p/'archive-receipt.json').read_text())
 assert receipt['complete'] and hashlib.sha256((p/'observations.tgz').read_bytes()).hexdigest()==receipt['archiveSHA256']
payload=(root/'payload.tgz').read_bytes()
remote=r'''from pathlib import Path
import ast,fcntl,hashlib,io,json,os,shutil,subprocess,sys,tarfile
base=Path('/home/lokad/lython-bench-checks');root=base/'text-allocation-b434-20261108'
reference=base/'json-float-entry-b430-20261108/candidate'
with (base/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert not root.exists() and not list(Path('/dev/shm').glob('lython-comparison-files-*'))
 assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[c]ollector.dll|[p]erf record'],capture_output=True).returncode==1
 payload=sys.stdin.buffer.read(SIZE+1);assert len(payload)==SIZE and hashlib.sha256(payload).hexdigest()=='DIGEST'
 reclaimed=[dict(priorFailedSetup='B431 failed before root creation or collector startup:free281833472 required282652672. Original source/log retained. Removed only archive-verified redundant extracts/core links. Corrected staging borrows identical already-frozen core instead of extracting a duplicate.',paths=['text-cost-b419-20261107/profiles','returned-record-b422-20261107/profiles','linear-keyword-b425-20261107/candidate/Lokad.Lython.dll','linear-keyword-b425-20261107/current-core.dll','csv-call-debt-b426-20261108/current/Lokad.Lython.dll'])]
 for name in reclaimed[0]['paths']:assert not (base/name).exists()
 with tarfile.open(fileobj=io.BytesIO(payload),mode='r:gz') as a:
  required=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())+256*1024*1024+8*1024*1024
  assert shutil.disk_usage(base).free>required,(shutil.disk_usage(base).free,required)
  root.mkdir();a.extractall(root,filter='data')
 (root/'reclaimed-extracts.json').write_text(json.dumps(reclaimed,indent=2)+'\n')
 shutil.copytree(reference,root/'candidate',copy_function=os.link)
 os.link(reference/'Lokad.Lython.dll',root/'current-core.dll')
 (root/'cpython-worker.py').symlink_to(base/'prepared-call-layout-b384-20261107/cpython-worker.py')
 identity=json.loads((root/'identity.json').read_text())
 assert hashlib.sha256((root/'candidate/Lokad.Lython.dll').read_bytes()).hexdigest()==identity['coreSHA256']
 for name,digest in identity['auxiliaryFiles'].items():assert hashlib.sha256((root/'candidate'/name).read_bytes()).hexdigest()==digest
 for name,digest in identity['collectorFiles'].items():
  target=root/'collector'/name
  if not target.exists():os.link(root/'candidate'/name,target)
  assert hashlib.sha256(target.read_bytes()).hexdigest()==digest
 fixture=root/'guest-fixtures';fixture.mkdir()
 subprocess.run(['sudo','-n','mount','-t','tmpfs','-o','size=128m,mode=0700,uid=lokad,gid=lokad','lython-b434-fixtures',str(fixture)],check=True)
 alias=Path('/tmp/lython-b434');assert not alias.exists() and not alias.is_symlink();alias.symlink_to(fixture,target_is_directory=True)
 assert len(str(alias)+'/dotnet-diagnostic-9999999-99999999-socket')<108
 for name in ['profile.py','archive-owned.py']:ast.parse((root/name).read_text())
 subprocess.run(['/bin/sh','-n',str(root/'run.sh')],check=True)
 setup=dict(freeBytes=shutil.disk_usage(base).free,allCoreAuxiliaryAndCollectorHashesVerified=True,ownerSeconds=120,stopSeconds=30,marginSeconds=30)
 assert setup['freeBytes']>256*1024*1024+8*1024*1024
 (root/'setup.json').write_text(json.dumps(setup,indent=2)+'\n');print(json.dumps(setup),flush=True)
subprocess.run(['sudo','-n','systemd-run','--unit=lython-b434-text-allocation-20261108','--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=120','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
'''.replace('SIZE',str(len(payload))).replace('DIGEST',hashlib.sha256(payload).hexdigest())
(root/'launch-command.py').write_text(remote,encoding='utf-8',newline='\n');compile(remote,'launch','exec')
ssh=['C:/Windows/System32/OpenSSH/ssh.exe','-i','C:/Users/JoannesVermorel/.ssh/id_lython-bench.pem','-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73']
stage='/home/lokad/lython-bench-checks/text-allocation-b434-launch.py'
code='from pathlib import Path; import sys; p=Path('+repr(stage)+'); assert not p.exists(); p.write_bytes(sys.stdin.buffer.read())'
r=subprocess.run(ssh+['python3 -c '+shlex.quote(code)],input=remote.encode('utf-8'),capture_output=True);assert r.returncode==0,r.stderr
r=subprocess.run(ssh+['python3 '+stage],input=payload,capture_output=True)
(root/'launch.log').write_bytes(r.stdout+b'\n'+r.stderr);print((r.stdout+r.stderr).decode('utf-8'),flush=True)
if r.returncode:raise SystemExit(r.returncode)
now=datetime.datetime.now(datetime.timezone.utc)
(root/'observe-not-before.json').write_text(json.dumps(dict(acknowledgedUTC=now.isoformat(),doNotObserveBeforeUTC=(now+datetime.timedelta(seconds=180)).isoformat(),ownerSeconds=120,stopSeconds=30,marginSeconds=30),indent=2)+'\n',encoding='utf-8')
print('Observe after '+(now+datetime.timedelta(seconds=180)).isoformat(),flush=True)
