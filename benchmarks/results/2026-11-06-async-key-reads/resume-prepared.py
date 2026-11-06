from pathlib import Path
import hashlib,json,shutil,subprocess
root=Path('/home/lokad/lython-bench-checks/key-suspension-b335-20261106')
status=subprocess.check_output(['systemctl','show','lython-b335-key-suspension-20261106','--property=LoadState,MainPID'],text=True)
assert 'LoadState=not-found' in status and 'MainPID=0' in status,status
assert not (root/'confirmation').exists()
items=[('b330-payload.tgz','5c85e294b30f64c78e91c52eeaca27328fda041420a0c1957551cbcbdf1c05e5','lython-b330-tuple-backing-20261106'),
       ('b331-payload.tgz','9b060f5d839a87887e7aaef86c3e14a2fd0e5f9f85954b66247b719411ae5d68','lython-b331-construction-checkpoint-20261106'),
       ('b335-payload.tgz','30735988217bea3ae272971321744061c291d4cb62cdc2a9cb19714b2b600f5d',None)]
record=dict(initialSetupFailedBeforeLaunch='setup.py minimum24MiB free assertion',observedFreeBytesBeforeCorrection=shutil.disk_usage(root).free,serviceBefore=status,removedOnlyRedundantVerifiedTransfers=[])
for name,digest,unit in items:
    path=root.parent/name
    if unit:
        prior=subprocess.check_output(['systemctl','show',unit,'--property=ActiveState,MainPID,Result'],text=True)
        assert 'ActiveState=inactive' in prior and 'MainPID=0' in prior and 'Result=success' in prior,prior
    assert not path.is_symlink() and path.resolve()==Path('/home/lokad/lython-bench-checks')/name
    assert hashlib.sha256(path.read_bytes()).hexdigest()==digest,(name,'hash differs')
    record['removedOnlyRedundantVerifiedTransfers'].append(dict(path=str(path),sha256=digest,bytes=path.stat().st_size))
for item in record['removedOnlyRedundantVerifiedTransfers']:Path(item['path']).unlink()
identity=json.loads((root/'identity.json').read_text())
for label in ['candidate','reference']:
    folder=root/label
    assert hashlib.sha256((folder/'Lokad.Lython.dll').read_bytes()).hexdigest()==identity['coreSHA256'][label]
    for name,digest in identity['auxiliaryFiles'].items():assert hashlib.sha256((folder/name).read_bytes()).hexdigest()==digest
record['sourceAndWorkerIdentitiesReverified']=True
record['correctedFreeBytes']=shutil.disk_usage(root).free
(root/'setup-recovery.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
assert record['correctedFreeBytes']>24*1024*1024,record
subprocess.run(['sudo','-n','systemd-run','--unit=lython-b335-key-suspension-20261106','--property=User=lokad','--property=WorkingDirectory='+str(root),'--property=RuntimeMaxSec=130','--property=TimeoutStopSec=30','--property=KillMode=control-group','/bin/sh',str(root/'run.sh')],check=True)
print(json.dumps(record))
