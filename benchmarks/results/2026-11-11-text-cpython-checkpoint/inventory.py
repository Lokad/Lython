from pathlib import Path
import json, shlex, subprocess

r = Path(__file__).parent
exact = json.loads((r / 'b568/exact-copies.json').read_text(encoding='utf-8'))
script = '''from pathlib import Path
import fcntl,hashlib,json,os,shutil,stat,subprocess
base=Path('/home/lokad/lython-bench-checks').resolve()
proofs=PROOFS
with (base/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
 users=subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[c]omplete-jobs.dll|[p]ython3.*cpython-worker.py'],capture_output=True)
 assert users.returncode in (0,1)
 others=[line for line in users.stdout.decode().splitlines() if int(line.split()[0])!=os.getpid()]
 assert not others, others
 archives=[]
 for p in proofs:
  f=base/p['path'];s=f.lstat()
  assert f.resolve(strict=True).is_relative_to(base) and stat.S_ISREG(s.st_mode) and s.st_uid==os.getuid() and s.st_nlink==1
  assert s.st_size==p['bytes'] and hashlib.sha256(f.read_bytes()).hexdigest()==p['sha256']
  archives.append(dict(p,allocatedBytes=s.st_blocks*512,links=s.st_nlink))
 for f,h in [('json-string-b565-20261111/reference/Lokad.Lython.dll','c27160d53d66489976b181fb425b2f46c927fa85156433b80dc6f5477a3e5506'),('json-cpython-b566-20261111/candidate/Lokad.Lython.dll','9dadd59bd75528186c66a9a95e2efcf411e611e0ccaa718ff38c4facdb191e33')]:
  assert hashlib.sha256((base/f).read_bytes()).hexdigest()==h
 print(json.dumps(dict(freshIdleLease=True,freeBytes=shutil.disk_usage(base).free,archives=archives,protectedImagesUnchanged=True)))
'''.replace('PROOFS', repr(exact))
ssh = ['C:/Windows/System32/OpenSSH/ssh.exe', '-i', 'C:/Users/JoannesVermorel/.ssh/id_lython-bench.pem', '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=15', 'lokad@20.166.121.73']
done = subprocess.run(ssh + ['python3 -c ' + shlex.quote(script)], capture_output=True)
(r / 'vm-idle-inventory.stdout').write_bytes(done.stdout)
(r / 'vm-idle-inventory.stderr').write_bytes(done.stderr)
assert done.returncode == 0, done.stderr.decode('utf-8')
data = json.loads(done.stdout)
(r / 'vm-idle-inventory.json').write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
print(json.dumps(dict(freeBytes=data['freeBytes'], reclaimableBytes=sum(p['allocatedBytes'] for p in data['archives']), freshIdleLease=data['freshIdleLease'])))
