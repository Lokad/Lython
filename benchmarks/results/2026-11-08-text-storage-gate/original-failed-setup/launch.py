from pathlib import Path
import ast,fcntl,hashlib,io,json,os,shutil,subprocess,sys,tarfile
base=Path('/home/lokad/lython-bench-checks');root=base/'text-allocation-b431-20261108'
reference=base/'json-float-entry-b430-20261108/candidate'
with (base/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert not root.exists() and not list(Path('/dev/shm').glob('lython-comparison-files-*'))
 assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[c]ollector.dll|[p]erf record'],capture_output=True).returncode==1
 payload=sys.stdin.buffer.read(1915352+1);assert len(payload)==1915352 and hashlib.sha256(payload).hexdigest()=='6d8c8be41b945cae20766f5ad5330277d6ddaaf78ff391a89d27dd7b01decfb9'
 reclaimed=[]
 for folder,digest in [('text-cost-b419-20261107','d409bd005b1b2e3ffb90b885e1d869949ce42868b4e3ea81f47a16ce05d307cd'),('returned-record-b422-20261107','d98a8657a9ffcf9cfe48c368afc6985f0a009c758f26e2fad1db91fc975f30f0')]:
  prior=base/folder;target=prior/'profiles';receipt=json.loads((prior/'archive-receipt.json').read_text())
  assert receipt['complete'] and receipt['archiveSHA256']==digest and hashlib.sha256((prior/'observations.tgz').read_bytes()).hexdigest()==digest
  assert target.is_dir() and not target.is_symlink() and target.resolve().is_relative_to(prior.resolve())
  with tarfile.open(prior/'observations.tgz') as a:
   files={m.name.removeprefix('perf/'):m for m in a.getmembers() if m.isfile() and m.name.startswith('perf/')}
   actual={str(p.relative_to(target)):p for p in target.rglob('*') if p.is_file()}
   assert actual.keys()==files.keys()
   for name,p in actual.items():assert not p.is_symlink() and hashlib.sha256(p.read_bytes()).digest()==hashlib.sha256(a.extractfile(files[name]).read()).digest()
  before=shutil.disk_usage(base).free;shutil.rmtree(target)
  reclaimed.append(dict(path=str(target),archiveSHA256=digest,beforeFreeBytes=before,afterFreeBytes=shutil.disk_usage(base).free,reason='Redundant extracted profile; immutable VM and independently verified local archives retained.'))
 prior=base/'linear-keyword-b425-20261107';digest='2e649929304de94c1b4c191d95f7013afc982c752beb506edb4f6bfc7821256d'
 receipt=json.loads((prior/'archive-receipt.json').read_text());assert receipt['complete'] and receipt['archiveSHA256']==digest and hashlib.sha256((prior/'observations.tgz').read_bytes()).hexdigest()==digest
 original=json.loads((prior/'identity.json').read_text())['coreSHA256']['candidate']
 targets=[prior/'candidate/Lokad.Lython.dll',prior/'current-core.dll',base/'csv-call-debt-b426-20261108/current/Lokad.Lython.dll']
 for p in targets:assert p.is_file() and not p.is_symlink() and p.resolve().is_relative_to(base.resolve()) and hashlib.sha256(p.read_bytes()).hexdigest()==original
 before=shutil.disk_usage(base).free
 for p in targets:p.unlink()
 reclaimed.append(dict(paths=[str(p)for p in targets],archiveSHA256=digest,beforeFreeBytes=before,afterFreeBytes=shutil.disk_usage(base).free,reason='Redundant retired prototype core links; immutable archives retained.'))
 with tarfile.open(fileobj=io.BytesIO(payload),mode='r:gz') as a:
  required=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())+256*1024*1024+8*1024*1024
  assert shutil.disk_usage(base).free>required,(shutil.disk_usage(base).free,required)
  root.mkdir();a.extractall(root,filter='data')
 (root/'reclaimed-extracts.json').write_text(json.dumps(reclaimed,indent=2)+'\n')
 shutil.copytree(reference,root/'candidate',copy_function=os.link)
 (root/'cpython-worker.py').symlink_to(base/'prepared-call-layout-b384-20261107/cpython-worker.py')
 identity=json.loads((root/'identity.json').read_text())
 assert hashlib.sha256((root/'candidate/Lokad.Lython.dll').read_bytes()).hexdigest()==identity['coreSHA256']
 for name,digest in identity['auxiliaryFiles'].items():assert hashlib.sha256((root/'candidate'/name).read_bytes()).hexdigest()==digest
 for name,digest in identity['collectorFiles'].items():
  target=root/'collector'/name
  if not target.exists():os.link(root/'candidate'/name,target)
  assert hashlib.sha256(target.read_bytes()).hexdigest()==digest
 fixture=root/'guest-fixtures';fixture.mkdir()
 subprocess.run(['sudo','-n','mount','-t','tmpfs','-o','size=128m,mode=0700,uid=lokad,gid=lokad','lython-b431-fixtures',str(fixture)],check=True)
 for name in ['profile.py','archive-owned.py']:ast.parse((root/name).read_text())
 subprocess.run(['/bin/sh','-n',str(root/'run.sh')],check=True)
 setup=dict(freeBytes=shutil.disk_usage(base).free,allCoreAuxiliaryAndCollectorHashesVerified=True,ownerSeconds=120,stopSeconds=30,marginSeconds=30)
 assert setup['freeBytes']>256*1024*1024+8*1024*1024
 (root/'setup.json').write_text(json.dumps(setup,indent=2)+'\n');print(json.dumps(setup),flush=True)
subprocess.run(['sudo','-n','systemd-run','--unit=lython-b431-text-allocation-20261108','--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=120','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
