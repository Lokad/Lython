from pathlib import Path
import ast,fcntl,hashlib,io,json,os,shutil,stat,subprocess,sys,tarfile
config=CONFIG
inventoryOnly=INVENTORY_ONLY
base=Path('/home/lokad/lython-bench-checks').resolve();root=Path(config['root']);source=base/'captured-sequence-b583-20261111/candidate'
assert root.parent==base and not root.exists()
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
protected={base/name:h for name,h in {'captured-sequence-b583-20261111/candidate/Lokad.Lython.dll': 'ccdd645dc6d2a541dbea0aaf170e33381666563d2579cd6f596fbebce427aeaa'}.items()}
with(base/'comparison-vm.lock').open('a')as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
 users=subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[c]omplete-jobs.dll|[p]ython3.*cpython-worker.py'],capture_output=True)
 assert users.returncode in(0,1) and not[line for line in users.stdout.decode().splitlines()if int(line.split()[0])!=os.getpid()]
 for p,h in protected.items():assert digest(p)==h,p
 for name,h in config['identity']['auxiliaryFiles'].items():assert digest(source/name)==h,name
 protocol=base/'prepared-call-layout-b384-20261107/cpython-worker.py'
 assert digest(protocol)=='02ca606af6b937a5bec8da744da36e6dbea45ef14ee7207732f34da052ad32f1'
 before=shutil.disk_usage(base).free;proofs=[];reclaim=0
 if before<=config['requiredFreeBytes']:
  for spec in config['retire']:
   first=base/spec['knownPath'];s=first.lstat()
   assert first.resolve(strict=True)==first and first.is_relative_to(base) and stat.S_ISREG(s.st_mode) and s.st_uid==os.getuid()
   assert s.st_size==spec['bytes']and digest(first)==spec['sha256'],(spec['knownPath'],s.st_size)
   aliases=[]
   for folder,dirs,files in os.walk(base):
    for name in files:
     p=Path(folder)/name;q=p.lstat()
     if(q.st_dev,q.st_ino)!=(s.st_dev,s.st_ino):continue
     assert p.resolve(strict=True)==p and p.is_relative_to(base)and stat.S_ISREG(q.st_mode)and q.st_uid==os.getuid()
     aliases.append(p.relative_to(base).as_posix())
   assert len(aliases)==s.st_nlink and spec['knownPath']in aliases and spec['sha256']not in protected.values()
   assert spec['kind']!='exact'or s.st_nlink==1
   proof=dict(spec);proof.update(paths=aliases,device=s.st_dev,inode=s.st_ino,allocatedBytes=s.st_blocks*512,allAliasesVerified=True)
   proofs.append(proof);reclaim+=s.st_blocks*512
  assert before+reclaim>config['requiredFreeBytes'],(before,reclaim,config['requiredFreeBytes'])
 if inventoryOnly:
  print(json.dumps(dict(freshIdleLease=True,freeBytes=before,requiredFreeBytes=config['requiredFreeBytes'],reclaimableBytes=reclaim,proofs=proofs,protectedImagesUnchanged=True)));sys.exit(0)
 payload=sys.stdin.buffer.read(config['payloadBytes']+1)
 assert len(payload)==config['payloadBytes']and hashlib.sha256(payload).hexdigest()==config['payloadSHA256']
 for proof in proofs:
  for name in proof['paths']:
   p=base/name;s=p.lstat();assert(s.st_dev,s.st_ino)==(proof['device'],proof['inode'])and s.st_size==proof['bytes']and s.st_uid==os.getuid();p.unlink()
 for p,h in protected.items():assert digest(p)==h
 assert shutil.disk_usage(base).free>config['requiredFreeBytes']
 with tarfile.open(fileobj=io.BytesIO(payload),mode='r:gz')as t:root.mkdir();t.extractall(root,filter='data')
 (root/'space-recovery.json').write_text(json.dumps(dict(beforeFreeBytes=before,afterExtractionFreeBytes=shutil.disk_usage(base).free,requiredFreeBytes=config['requiredFreeBytes'],files=proofs,canonicalStorage='Exact committed canonical benchmark archive entries verified locally; current selected reference protected; withdrawn prototype originals preserved in committed canonical Git; each retirement has fresh path/UID/inode/all-alias proof',freshIdleLease=True,osFloorMiB=240,evidenceHeadroomMiB=12,protectedImagesUnchanged=True),indent=2)+'\n')
 for role in ['reference','candidate']:shutil.copytree(source,root/role,copy_function=os.link)
 for role,name in [('reference','baseline-core.dll'),('candidate','current-core.dll')]:
  (root/role/'Lokad.Lython.dll').unlink();os.link(base/'captured-sequence-b583-20261111/candidate/Lokad.Lython.dll' if role=='reference' else root/name,root/role/'Lokad.Lython.dll')
 for role in ['reference','candidate']:assert digest(root/role/'Lokad.Lython.dll')==config['identity']['coreSHA256'][role]
 shutil.copyfile(protocol,root/'cpython-worker.py')
 assert digest(root/'representative.json')==config['identity']['catalogSHA256']
 for name in ['collect.py','archive-owned.py']:ast.parse((root/name).read_text())
 subprocess.run(['/bin/sh','-n',str(root/'run.sh')],check=True)
 fixture=root/'guest-fixtures';fixture.mkdir()
 subprocess.run(['sudo','-n','mount','-t','tmpfs','-o','size=128m,mode=0700,uid=lokad,gid=lokad','lython-b588-fixtures',str(fixture)],check=True)
 setup=dict(freeBytes=shutil.disk_usage(base).free,sourceAndImagesVerified=True,jobs=6,ownerSeconds=210,stopSeconds=30,marginSeconds=30)
 assert setup['freeBytes']>252*1024*1024
 (root/'setup.json').write_text(json.dumps(setup,indent=2)+'\n');print(json.dumps(setup),flush=True)
try:
 subprocess.run(['sudo','-n','systemd-run','--unit='+config['unit'],'--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=210','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
except BaseException:
 subprocess.run(['sudo','-n','umount',str(fixture)],check=True);fixture.rmdir();raise
live=subprocess.check_output(['systemctl','show',config['unit']+'.service','--property=ActiveState,MainPID,RuntimeMaxUSec'],text=True)
assert'ActiveState=active'in live and'MainPID=0'not in live;print(live,flush=True)
