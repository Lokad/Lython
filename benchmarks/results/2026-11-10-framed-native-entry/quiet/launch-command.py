from pathlib import Path
import ast,fcntl,hashlib,io,json,os,shutil,subprocess,sys,tarfile
base=Path('/home/lokad/lython-bench-checks');root=base/'bound-native-entry-b521-20261110';reference=base/'string-index-storage-b518-20261109/candidate'
with(base/'comparison-vm.lock').open('a')as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB);assert not root.exists() and not list(Path('/dev/shm').glob('lython-comparison-files-*'))
 assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[c]ollector.dll|[p]erf record'],capture_output=True).returncode==1
 payload=sys.stdin.buffer.read(1963392+1);assert len(payload)==1963392 and hashlib.sha256(payload).hexdigest()=='ca3663b6aed48a0b3bdb75c4ced15efe6637a16472a6569f967bf542fa9a0d11'
 with tarfile.open(fileobj=io.BytesIO(payload),mode='r:gz')as a:
  required=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())+(240+12)*1024*1024+65536
  assert shutil.disk_usage(base).free>required,(shutil.disk_usage(base).free,required)
  root.mkdir();a.extractall(root,filter='data')
 previous=base/'shared-comparison-b514-20261109'
 for n in ['common-worker.dll','common-adapter.dll','common-worker.deps.json','common-worker.runtimeconfig.json']:os.link(previous/n,root/n)
 identity=json.loads((root/'identity.json').read_text());assert set(identity['coreSHA256'])=={'reference','reference-control','candidate','candidate-control'}
 assert hashlib.sha256((root/'representative.json').read_bytes()).hexdigest()==identity['catalogSHA256']
 os.link(base/'string-index-storage-b518-20261109/current-core.dll',root/'baseline-core.dll')
 assert hashlib.sha256((root/'baseline-core.dll').read_bytes()).hexdigest()==identity['coreSHA256']['reference']
 assert hashlib.sha256((root/'current-core.dll').read_bytes()).hexdigest()==identity['coreSHA256']['candidate']
 for role in ['reference','candidate']:
  shutil.copytree(reference,root/role,copy_function=os.link)
 for role,name in [('reference','baseline-core.dll'),('candidate','current-core.dll')]:
  (root/role/'Lokad.Lython.dll').unlink();os.link(root/name,root/role/'Lokad.Lython.dll')
 for role in ['reference','candidate']:
  for target,name in [('Lokad.Lython.Benchmarks.dll','common-worker.dll'),('Lokad.Lython.Utf8Regex.dll','common-adapter.dll'),('Lokad.Lython.Benchmarks.deps.json','common-worker.deps.json'),('Lokad.Lython.Benchmarks.runtimeconfig.json','common-worker.runtimeconfig.json')]:
   (root/role/target).unlink();os.link(root/name,root/role/target)
 for role in ['reference','candidate']:
  assert hashlib.sha256((root/role/'Lokad.Lython.dll').read_bytes()).hexdigest()==identity['coreSHA256'][role]
  for n,h in identity['auxiliaryFiles'].items():assert hashlib.sha256((root/role/n).read_bytes()).hexdigest()==h
 protocol=base/'prepared-call-layout-b384-20261107/cpython-worker.py'
 assert hashlib.sha256(protocol.read_bytes()).hexdigest()=='02ca606af6b937a5bec8da744da36e6dbea45ef14ee7207732f34da052ad32f1'
 (root/'cpython-worker.py').symlink_to(protocol)
 for n in ['collect.py','archive-owned.py']:ast.parse((root/n).read_text())
 assert 'assert len(jobs)==5 'in(root/'collect.py').read_text();subprocess.run(['/bin/sh','-n',str(root/'run.sh')],check=True)
 fixture=root/'guest-fixtures';fixture.mkdir();subprocess.run(['sudo','-n','mount','-t','tmpfs','-o','size=128m,mode=0700,uid=lokad,gid=lokad','lython-b521-fixtures',str(fixture)],check=True)
 try:
  setup=dict(freeBytes=shutil.disk_usage(base).free,coreAndEveryAuxiliaryVerified=True,allConfiguredJobsValidated=True,roles=4,ownerSeconds=210,stopSeconds=30,marginSeconds=30,osFreeFloorMiB=240,evidenceHeadroomMiB=12)
  assert setup['freeBytes']>(240+12)*1024*1024
  (root/'setup.json').write_text(json.dumps(setup,indent=2)+'\n');print(json.dumps(setup),flush=True)
  fcntl.flock(lease,fcntl.LOCK_UN)
  subprocess.run(['sudo','-n','systemd-run','--unit=lython-b521-bound-native-entry-20261110','--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=210','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
  live=subprocess.check_output(['systemctl','show','lython-b521-bound-native-entry-20261110.service','--property=ActiveState,MainPID,RuntimeMaxUSec'],text=True)
  print(json.dumps(dict(liveUnit=live)),flush=True)
 except BaseException:
  subprocess.run(['sudo','-n','umount',str(fixture)],check=True);fixture.rmdir();raise
