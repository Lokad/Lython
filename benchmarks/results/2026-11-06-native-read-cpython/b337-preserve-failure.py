from pathlib import Path
import hashlib, importlib.util, json, os, subprocess, tarfile
root = Path('/home/lokad/lython-bench-checks/native-read-checkpoint-b337-20261106')
unit = 'lython-b337-native-read-checkpoint-20261106'
status = subprocess.check_output(['systemctl','show',unit,'--property=LoadState,ActiveState,MainPID,Result'],text=True)
assert 'MainPID=0' in status and 'ActiveState=failed' in status, status
(root/'final-service-status.txt').write_text(status,encoding='utf-8')
(root/'journal.txt').write_text(subprocess.check_output(['journalctl','-u',unit,'--no-pager'],text=True),encoding='utf-8')
spec=importlib.util.spec_from_file_location('protocol',root/'cpython-worker.py')
protocol=importlib.util.module_from_spec(spec);spec.loader.exec_module(protocol)
env=os.environ.copy();env['DOTNET_ROOT']='/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148'
with subprocess.Popen([env['DOTNET_ROOT']+'/dotnet',str(root/'candidate/Lokad.Lython.Benchmarks.dll'),'--compare','worker','--catalog',str(root/'representative.json')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,env=env) as p:
    ready=protocol.read_frame(p.stdout)
    (root/'startup-path-diagnostic.json').write_text(json.dumps(ready,indent=2)+'\n',encoding='utf-8')
    protocol.write_frame(p.stdin,dict(protocolVersion=2,requestId=1,operation='quit'))
    closed=protocol.read_frame(p.stdout)
    assert closed['status']=='Closed',closed
    assert p.wait(timeout=10)==0
    assert not p.stderr.read()
assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[c]omplete-jobs.dll'],capture_output=True).returncode==1
with tarfile.open(root/'failed-observations.tgz','w:gz') as archive:
    for item in root.iterdir():
        if item.name in ['candidate','reference','core','__pycache__'] or item.suffix=='.tgz':continue
        archive.add(item,arcname=item.name)
receipt=dict(complete=False,jobs=0,reason='Worker core identity mismatch before first timing; original failed collection retained',allWorkersJoined=True,guestFixtureDirectories=[],archiveSHA256=hashlib.sha256((root/'failed-observations.tgz').read_bytes()).hexdigest())
(root/'failed-archive-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
print(json.dumps(receipt))
