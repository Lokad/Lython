from pathlib import Path
import hashlib,json,shutil,subprocess,tarfile
r=Path('/home/lokad/lython-bench-checks/cell-authority-b606-20261112')
raw=json.loads((r/'confirmation/confirmation.json').read_text());assert raw['complete']and len(raw['jobs'])==4 and all(j['workersJoinedAndFixturesRemoved']for j in raw['jobs'])
assert not(r/'guest-fixtures').exists()and subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]ython3.13.*cpython-worker.py'],capture_output=True).returncode==1
assert all(json.loads((r/'confirmation'/name).read_text())['gate']['quiet']for name in ['machine-before.json','machine-after.json'])
with tarfile.open(r/'observations.tgz','w:gz',dereference=True)as a:
 for path in sorted(r.rglob('*')):
  if path.is_file()and path.name!='observations.tgz'and not {'candidate','reference'}&set(path.relative_to(r).parts):a.add(path,arcname=path.relative_to(r).as_posix())
assert shutil.disk_usage(r).free>240*1024*1024
with tarfile.open(r/'observations.tgz')as a:assert json.loads(a.extractfile('confirmation/confirmation.json').read())['complete']
receipt=dict(complete=True,jobs=4,elapsedSeconds=raw['elapsedSeconds'],allWorkersJoined=True,guestFixturesRemoved=True,quietBeforeAndAfter=True,archivedAndVerifiedUnderLease=True,archiveSHA256=hashlib.sha256((r/'observations.tgz').read_bytes()).hexdigest(),freeBytesAfterArchive=shutil.disk_usage(r).free,performanceQualified=False)
(r/'archive-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt))
