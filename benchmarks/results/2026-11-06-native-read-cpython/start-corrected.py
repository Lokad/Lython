from pathlib import Path
import hashlib,json,shutil,subprocess
root=Path('/home/lokad/lython-bench-checks/native-read-checkpoint-b338-20261106')
unit='lython-b338-native-read-checkpoint-20261106'
status=subprocess.run(['systemctl','show',unit,'--property=LoadState,MainPID'],capture_output=True,text=True)
assert 'LoadState=not-found' in status.stdout and 'MainPID=0' in status.stdout,status.stdout
assert not (root/'confirmation').exists()
assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[c]omplete-jobs.dll'],capture_output=True).returncode==1
identity=json.loads((root/'identity.json').read_text())
ready=json.loads((root/'startup-path-correction.json').read_text())['ready']
assert next(a for a in ready['libraries'] if Path(a['path']).name=='Lokad.Lython.dll')['sha256']==identity['coreSHA256']['candidate']
for label in ['candidate','reference']:
    assert hashlib.sha256((root/label/'Lokad.Lython.dll').read_bytes()).hexdigest()==identity['coreSHA256'][label]
assert shutil.disk_usage(root).free>24*1024*1024
(root/'launch-correction.txt').write_text('The old combined extract/setup launcher refused the already prepared directory before starting any service. This standalone launcher starts exactly one corrected collection after Ready verified the actual loaded candidate core.\n',encoding='utf-8')
subprocess.run(['sudo','-n','systemd-run','--unit='+unit,'--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=460','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
