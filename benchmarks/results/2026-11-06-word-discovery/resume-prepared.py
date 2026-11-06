from pathlib import Path
import hashlib,json,shutil,subprocess

root=Path('/home/lokad/lython-bench-checks/word-discovery-b332-20261106')
status=subprocess.check_output(['systemctl','show','lython-b332-word-discovery-20261106','--property=LoadState,ActiveState,MainPID'],text=True)
assert 'LoadState=not-found' in status and 'MainPID=0' in status,status
assert not (root/'confirmation').exists()
payload=root.parent/'b332-payload.tgz'
expected='bff14ef466f66e85283d9c4f004e74fb6c9b15df6604efdaf6d55f910acebfb7'
assert hashlib.sha256(payload.read_bytes()).hexdigest()==expected
before=shutil.disk_usage(root).free
assert payload.resolve()==Path('/home/lokad/lython-bench-checks/b332-payload.tgz')
payload.unlink()  # Redundant transfer only; verified identical local archive retained.
identity=json.loads((root/'identity.json').read_text())
for label in ('candidate','reference'):
    folder=root/label
    assert hashlib.sha256((folder/'Lokad.Lython.dll').read_bytes()).hexdigest()==identity['coreSHA256'][label]
    for name,digest in identity['auxiliaryFiles'].items():
        assert hashlib.sha256((folder/name).read_bytes()).hexdigest()==digest
after=shutil.disk_usage(root).free
record=dict(initialSetupStoppedBeforeService=True,initialFreeBytes=before,minimumFreeBytes=24*1024*1024,
            removedOnlyVerifiedRedundantTransfer=str(payload),retainedLocalTransferSHA256=expected,
            correctedFreeBytes=after,sourceAndWorkersReverified=True,serviceBefore=status)
(root/'setup-correction.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
assert after>24*1024*1024,record
subprocess.run(['sudo','-n','systemd-run','--unit=lython-b332-word-discovery-20261106','--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=130','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
print(json.dumps(record))
