from pathlib import Path
import fcntl,json,os,subprocess
root=Path('/home/lokad/lython-bench-checks/method-frame-b327-20261106').resolve()
assert (root/'exit-code.txt').read_text().strip()=='0'
with (root.parent/'comparison-vm.lock').open('a') as lease:
    fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
    assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record'],capture_output=True).returncode==1
    for name in ('method-csv',):
        data=root/'perf'/name/'perf.data'
        assert not data.is_symlink() and data.resolve().is_relative_to(root)
        record=json.loads((data.parent/'record.json').read_text());assert record['exitCode']==0
        if data.stat().st_uid==0:
            subprocess.run(['sudo','-n','chown',f'{os.getuid()}:{os.getgid()}',str(data)],check=True)
        assert data.stat().st_uid==os.getuid()
    print('Owned perf.data exports readable; raw contents and completed capture unchanged.')
