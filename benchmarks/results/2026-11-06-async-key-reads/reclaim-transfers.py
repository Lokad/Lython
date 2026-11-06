from pathlib import Path
import hashlib,json,shutil,subprocess
root=Path('/home/lokad/lython-bench-checks')
items=[('b334-payload.tgz','b15ed9d4e922287a8b49eea788fb314147844e30967ed73c87ba722a65499cd0','lython-b334-key-suspension-20261106'),
       ('b333-payload.tgz','ee49a9e087a3892b6a71cb63d911a318bc8c415460f9b30f63fda6768f02fb76','lython-b333-word-discovery-20261106')]
record=dict(beforeFreeBytes=shutil.disk_usage(root).free,removed=[])
for name,digest,unit in items:
    path=root/name
    status=subprocess.check_output(['systemctl','show',unit,'--property=ActiveState,MainPID,Result'],text=True)
    assert 'ActiveState=inactive' in status and 'MainPID=0' in status and 'Result=success' in status,status
    assert not path.is_symlink() and path.resolve()==Path('/home/lokad/lython-bench-checks')/name
    assert hashlib.sha256(path.read_bytes()).hexdigest()==digest,(name,'hash differs')
    record['removed'].append(dict(path=str(path),sha256=digest,bytes=path.stat().st_size,terminalService=status))
for item in record['removed']:Path(item['path']).unlink()
record['afterFreeBytes']=shutil.disk_usage(root).free
print(json.dumps(record,indent=2))
