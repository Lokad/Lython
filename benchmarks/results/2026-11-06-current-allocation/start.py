from pathlib import Path
import fcntl,hashlib,shutil,subprocess,tarfile
payload=Path('/home/lokad/lython-bench-checks/b345-payload.tgz')
assert hashlib.sha256(payload.read_bytes()).hexdigest()=='46110c1d7121aa1d153849a57cb482008356e551dee9cbbac60bf9290b30d9d3'
assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[c]omplete-jobs.dll'],capture_output=True).returncode==1
with (payload.parent/'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
    with tarfile.open(payload) as archive:
        expanded=sum(x.size for x in archive.getmembers())
        free=shutil.disk_usage(payload.parent).free
        assert free>expanded+24*1024*1024,(free,expanded)
        root=payload.parent/'current-allocation-b345-20261106';assert not root.exists();root.mkdir()
        archive.extractall(root,filter='data')
        assert b'\r' not in (root/'run.sh').read_bytes()
        subprocess.run(['/bin/sh','-n',str(root/'run.sh')],check=True)
subprocess.run(['sudo','-n','systemd-run','--unit=lython-b345-current-allocation-20261106','--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=90','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
