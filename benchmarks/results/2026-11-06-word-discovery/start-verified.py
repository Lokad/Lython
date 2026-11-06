from pathlib import Path
import hashlib,json,shutil,subprocess
root=Path('/home/lokad/lython-bench-checks/word-discovery-b332-20261106')
status=subprocess.check_output(['systemctl','show','lython-b332-word-discovery-20261106','--property=LoadState,MainPID'],text=True)
assert 'LoadState=not-found' in status and 'MainPID=0' in status,status
assert not (root/'confirmation').exists()
identity=json.loads((root/'identity.json').read_text())
for label in ('candidate','reference'):
    folder=root/label
    assert hashlib.sha256((folder/'Lokad.Lython.dll').read_bytes()).hexdigest()==identity['coreSHA256'][label]
    for name,digest in identity['auxiliaryFiles'].items():
        assert hashlib.sha256((folder/name).read_bytes()).hexdigest()==digest
free=shutil.disk_usage(root).free
assert free>24*1024*1024,free
(root/'launch-disk-check.json').write_text(json.dumps(dict(freeBytes=free,earlierStoppedSetupPreserved='setup-correction.json',noEarlierService=True),indent=2)+'\n',encoding='utf-8')
subprocess.run(['sudo','-n','systemd-run','--unit=lython-b332-word-discovery-20261106','--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=130','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
print(json.dumps(dict(launched=True,freeBytes=free)))
