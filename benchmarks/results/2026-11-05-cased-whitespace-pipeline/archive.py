from pathlib import Path
import subprocess,json,hashlib,tarfile,fcntl
root=Path('/home/lokad/lython-bench-checks/cased-whitespace-b239-20261105')
status=subprocess.check_output(['systemctl','show','lython-b239-cased-whitespace-20261105','--property=ActiveState,Result,MainPID'],text=True)
assert 'ActiveState=inactive' in status and 'MainPID=0' in status and 'Result=success' in status,status
assert (root/'exit-code.txt').read_text(encoding='utf-8').strip()=='0'
(root/'final-service-status.txt').write_text(status,encoding='utf-8')
r=json.loads((root/'comparison.json').read_text(encoding='utf-8'))
assert len(r['observations'])==6 and all(r[k] for k in ['allGoldensPassed','allBalancesZero','ownedFixturesRemoved','imageIsolationVerified'])
for n in ['before','after']:assert json.loads((root/f'machine-{n}.json').read_text(encoding='utf-8'))['gate']['quiet']
with open(root.parent/'comparison-vm.lock','w') as lock:
 fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
 assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[c]omplete-jobs.dll'],capture_output=True).returncode==1
 with tarfile.open(root/'observations.tgz','w:gz') as t:
  for f in root.iterdir():
   if f.is_file() and f.name not in ['observations.tgz','payload.tgz']:t.add(f,arcname=f.name)
  for f in (root/'jobs').iterdir():
   if f.is_file():t.add(f,arcname='jobs/'+f.name)
print(hashlib.sha256((root/'observations.tgz').read_bytes()).hexdigest())
