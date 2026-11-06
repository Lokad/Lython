from pathlib import Path
import fcntl,hashlib,shutil,subprocess,tarfile
payload=Path('/home/lokad/lython-bench-checks/b342-payload.tgz')
assert hashlib.sha256(payload.read_bytes()).hexdigest()=='a20690fcc4dcceb101613b06dc5e3a8a85a6a0879b20538e4162c1447f8d6364'
assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[c]omplete-jobs.dll'],capture_output=True).returncode==1
with (payload.parent/'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
    with tarfile.open(payload) as archive:
        expanded=sum(x.size for x in archive.getmembers())
        free=shutil.disk_usage(payload.parent).free
        assert free>expanded+32*1024*1024,(free,expanded)
        root=payload.parent/'constructor-frames-b342-20261106';assert not root.exists();root.mkdir()
        archive.extractall(root,filter='data')
    subprocess.run(['python3',str(root/'setup.py')],check=True)
subprocess.run(['sudo','-n','systemd-run','--unit=lython-b342-constructor-frames-20261106','--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=160','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
