from pathlib import Path
import fcntl,hashlib,json,os,shutil,stat,subprocess
base=Path('/home/lokad/lython-bench-checks')
spec=[{'path': '/home/lokad/lython-bench-checks/call-inputs-b519-20261109/observations.tgz', 'sha256': '6b910357fc12f0d48e7c0a4b991ff14941cae07d5cc67edc83fee660119dc0f5', 'size': 4041848, 'preservedLocal': 'D:/code/Lython/benchmarks/results/2026-11-09-call-input-preparation/original-observations.tgz', 'tracked': True}, {'path': '/home/lokad/lython-bench-checks/call-inputs-b520-20261109/observations.tgz', 'sha256': '1a23c400bc3c32ed30415e9e85208de9b5523336c8e2fc30d90c46dfaa058e9f', 'size': 4043168, 'preservedLocal': 'D:/code/Lython/benchmarks/results/2026-11-09-call-input-preparation/refined-observations.tgz', 'tracked': True}, {'path': '/home/lokad/lython-bench-checks/record-fields-b478-20261108/observations.tgz', 'sha256': '0afdda174721c6db68f52c19886c11a42a38061683e987d9f2de7d690e8bb3f5', 'size': 2115179, 'preservedLocal': 'D:/code/Lython/benchmarks/results/2026-11-08-record-field-resolution/observations.tgz', 'tracked': True}, {'path': '/home/lokad/lython-bench-checks/compile-phase-b488-20261109/observations.tgz', 'sha256': 'a20bbdb53bcd2ab94c0909a8816118b03bed5220dbb6b76572ac73c1444a3465', 'size': 477328, 'preservedLocal': 'D:/code/Lython/benchmarks/results/2026-11-09-compile-phase/observations.tgz', 'tracked': True}]
with (base/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert not (base/'bound-native-entry-b521-20261110').exists()
 assert not list(Path('/dev/shm').glob('lython-comparison-files-*')) and not Path('/dev/shm/lython-b512').exists()
 assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[o]ps-profile.py'],capture_output=True).returncode==1
 free=shutil.disk_usage(base).free
 if free>271130624:
  print(json.dumps(dict(skipped=True,beforeFreeBytes=free,requiredFreeBytes=271130624,reason='Fresh space sufficient; no mutation')));__import__('sys').exit(0)
 rows=[];unavailable=[]
 for entry in spec:
  p=Path(entry['path'])
  if not p.exists():unavailable.append(entry['path']);continue
  assert p.resolve(strict=True)==p and p.parent.parent==base and p.name=='observations.tgz'
  info=p.lstat();assert stat.S_ISREG(info.st_mode) and info.st_nlink==1 and info.st_uid==os.getuid() and info.st_gid==os.getgid()
  assert info.st_size==entry['size'] and hashlib.sha256(p.read_bytes()).hexdigest()==entry['sha256']
  receipt=json.loads((p.parent/'archive-receipt.json').read_text());receipt=receipt.get('receipt',receipt);assert receipt['complete'] and receipt['allWorkersJoined'] and receipt['archiveSHA256']==entry['sha256']
  parents=[]
  for parent in p.parents:
   meta=parent.lstat();assert stat.S_ISDIR(meta.st_mode) and not parent.is_symlink()
   parents.append(dict(path=str(parent),uid=meta.st_uid,gid=meta.st_gid,mode=oct(meta.st_mode)))
  rows.append(dict(**entry,device=info.st_dev,inode=info.st_ino,links=info.st_nlink,uid=info.st_uid,gid=info.st_gid,mode=oct(info.st_mode),parents=parents,allAliases=[str(p)]))
  if free+sum(Path(e['path']).stat().st_blocks*512 for e in rows)>271130624:break
 assert free+sum(Path(e['path']).stat().st_blocks*512 for e in rows)>271130624, ('Exact copies cannot fund unchanged payload/floors; no mutation',dict(freeBytes=free,requiredBytes=271130624,selectedBytes=sum(Path(e['path']).stat().st_blocks*512 for e in rows),unavailable=unavailable))
 plan=dict(requiredFreeBytes=271130624,freshIdleLease=True,beforeFreeBytes=free,osFloorMiB=240,evidenceHeadroomMiB=12,files=rows,unavailable=unavailable,operation='Remove only redundant remote compressed archives. Their raw trees, receipts, frozen workers and byte-identical tracked local archives survive.')
 target=base/'bound-native-entry-b521-space-plan.json'
 with target.open('x') as f:f.write(json.dumps(plan,indent=2)+'\n');f.flush();os.fsync(f.fileno())
 for entry in rows:
  p=Path(entry['path']);info=p.lstat()
  assert info.st_dev==entry['device'] and info.st_ino==entry['inode'] and info.st_nlink==1 and hashlib.sha256(p.read_bytes()).hexdigest()==entry['sha256']
  p.unlink()
 plan['afterFreeBytes']=shutil.disk_usage(base).free
 assert plan['afterFreeBytes']>271130624 and plan['afterFreeBytes']>free and plan['afterFreeBytes']>(240+12)*1024*1024
 (base/'bound-native-entry-b521-space-receipt.json').write_text(json.dumps(plan,indent=2)+'\n')
 print(json.dumps(plan))
