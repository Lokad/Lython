from pathlib import Path
import hashlib,json,subprocess,tarfile
root=Path('/home/lokad/lython-bench-checks/retained-cpython-b409-20261107')
r=json.loads((root/'confirmation/confirmation.json').read_text());assert r['complete'] and len(r['jobs'])==23
assert all(j['workersJoinedAndFixturesRemoved'] for j in r['jobs'])
assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker'],capture_output=True).returncode==1
assert json.loads((root/'confirmation/machine-after.json').read_text())['gate']['quiet']
with tarfile.open(root/'observations.tgz','w:gz') as t:
 for n in ['confirmation','identity.json','representative.json','collect.py','cpython-worker.py','pre-measurement-plan.md','setup-contract.json','source.patch','focused.json','retirement.json','original-failure.json','contract.md','started.utc','run.sh','archive-owned.py']:t.add((root/n).resolve(),arcname=n)
with tarfile.open(root/'observations.tgz') as t:assert json.loads(t.extractfile('confirmation/confirmation.json').read())['complete']
result=dict(complete=True,jobs=23,elapsedSeconds=r['elapsedSeconds'],allWorkersJoined=True,guestFixtureDirectories=[],quietBeforeAndAfter=True,archivedAndVerifiedUnderLease=True,archiveSHA256=hashlib.sha256((root/'observations.tgz').read_bytes()).hexdigest())
(root/'archive-receipt.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
