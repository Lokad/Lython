from pathlib import Path
import hashlib,json,shutil,subprocess
root=Path('/home/lokad/lython-bench-checks/native-read-prefix-b336-20261106')
status=subprocess.check_output(['systemctl','show','lython-b336-native-read-prefix-20261106','--property=LoadState,MainPID'],text=True,encoding='utf-8')
assert 'LoadState=not-found' in status and 'MainPID=0' in status,status
assert not (root/'confirmation').exists()
identity=json.loads((root/'identity.json').read_text(encoding='utf-8'))
for label in ['candidate','reference']:
    folder=root/label
    assert hashlib.sha256((folder/'Lokad.Lython.dll').read_bytes()).hexdigest()==identity['coreSHA256'][label]
    for name,digest in identity['auxiliaryFiles'].items():assert hashlib.sha256((folder/name).read_bytes()).hexdigest()==digest
record=dict(initialSetupFailedBeforeLaunch='setup.py minimum24MiB free assertion; both unpacked DLLs need11.2MB in addition to compressed transfer',sourceAndWorkersReverified=True,serviceBefore=status,freeBytesBeforeSoleLaunch=shutil.disk_usage(root).free)
(root/'setup-recovery.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
assert record['freeBytesBeforeSoleLaunch']>24*1024*1024,record
subprocess.run(['sudo','-n','systemd-run','--unit=lython-b336-native-read-prefix-20261106','--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=130','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
print(json.dumps(record))
