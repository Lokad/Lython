from pathlib import Path
import hashlib,json,subprocess,tarfile,shutil
r=Path('/home/lokad/lython-bench-checks/word-control-profile-b596-20261111');ram=r/'guest-fixtures';profiles=ram/'profiles'
assert ram.is_mount()and set(ram.iterdir())=={profiles}
assert all(json.loads((r/n).read_text())['gate']['quiet']for n in ['machine-before.json','machine-after.json'])
assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[p]ython3.*cpython-worker.py'],capture_output=True).returncode==1
seconds=0
for name in ['reference','candidate']:
 p=profiles/name;record=json.loads((p/'record.json').read_text())
 assert record['exitCode']==0 and record['closed']['status']=='Closed'
 assert all(record[n]['status']=='Equivalent'for n in ['verificationBefore','verificationAfter'])
 assert(p/'worker.stderr').stat().st_size==0 and record['redundantTemporaryMapsRetiredAfterJoin']
 assert(p/'perf.map').stat().st_size>0
 elapsed=record['response']['elapsedTicks']/record['response']['clockFrequency'];assert elapsed<10;seconds+=elapsed
assert seconds<16 and shutil.disk_usage(r).free>240*1024*1024
files={p.relative_to(r).as_posix():p for p in sorted(r.rglob('*'))if p.is_file()and p.name!='observations.tgz'and not{'candidate','reference','guest-fixtures'}&set(p.relative_to(r).parts)}
files.update({'profiles/'+p.relative_to(profiles).as_posix():p for p in sorted(profiles.rglob('*'))if p.is_file()})
manifest={n:hashlib.sha256(p.read_bytes()).hexdigest()for n,p in files.items()}
(r/'profile-archive-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
with tarfile.open(r/'observations.tgz','w:gz',dereference=True)as a:
 for n,p in files.items():a.add(p,arcname=n)
 a.add(r/'profile-archive-manifest.json',arcname='profile-archive-manifest.json')
with tarfile.open(r/'observations.tgz')as a:
 for n,h in manifest.items():assert hashlib.sha256(a.extractfile(n).read()).hexdigest()==h,n
assert shutil.disk_usage(r).free>240*1024*1024
# Evidence is now persistent and independently readable under the owned lease.
# All users have ended. Only this explicit owned tmpfs is discarded.
subprocess.run(['sudo','-n','umount',str(ram)],check=True);assert not ram.is_mount()and not list(ram.iterdir());ram.rmdir()
receipt=dict(complete=True,allWorkersJoined=True,guestFixturesRemoved=True,quietBeforeAndAfter=True,archivedAndVerifiedUnderLease=True,capturedSeconds=seconds,profileFiles=len(manifest),archiveSHA256=hashlib.sha256((r/'observations.tgz').read_bytes()).hexdigest(),freeBytesAfterArchive=shutil.disk_usage(r).free,performanceQualified=False)
(r/'archive-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt))
