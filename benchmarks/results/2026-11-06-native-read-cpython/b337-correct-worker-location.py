from pathlib import Path
import hashlib, importlib.util, json, os, shutil, subprocess
old=Path('/home/lokad/lython-bench-checks/native-read-checkpoint-b337-20261106')
root=old.parent/'native-read-checkpoint-b338-20261106'
assert not root.exists()
status=subprocess.check_output(['systemctl','show','lython-b337-native-read-checkpoint-20261106','--property=MainPID,ActiveState'],text=True)
assert 'MainPID=0' in status and 'ActiveState=failed' in status
assert json.loads((old/'failed-archive-receipt.json').read_text())['jobs']==0
assert not list(Path('/dev/shm').glob('lython-comparison-files-*'))
assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[c]omplete-jobs.dll'],capture_output=True).returncode==1
assert shutil.disk_usage(old).free > 32*1024*1024
root.mkdir()
for item in old.iterdir():
    if item.suffix not in ['.py','.json','.md','.sh','.patch','.tgz']:continue
    if item.name.startswith(('failed-','preserve-','correct-','startup-')):continue
    if item.suffix=='.tgz' and item.name!='source-snapshot.tgz':continue
    if item.suffix=='.tgz':shutil.copyfile(item,root/item.name)
    else:(root/item.name).write_text(item.read_text(encoding='utf-8').replace(str(old),str(root)).replace('lython-b337-native-read-checkpoint-20261106','lython-b338-native-read-checkpoint-20261106'),encoding='utf-8',newline='\n')
(root/'core').symlink_to(old/'core',target_is_directory=True)
(root/'reference').symlink_to(old/'reference',target_is_directory=True)
(root/'candidate').mkdir()
for item in (old/'candidate').iterdir():
    target=root/'candidate'/item.name
    if item.name in ['Lokad.Lython.Benchmarks.dll','Lokad.Lython.Benchmarks.deps.json','Lokad.Lython.Benchmarks.runtimeconfig.json']:
        shutil.copyfile(item,target)
    else:target.symlink_to(item.resolve(),target_is_directory=item.is_dir())
spec=importlib.util.spec_from_file_location('protocol',root/'cpython-worker.py')
protocol=importlib.util.module_from_spec(spec);spec.loader.exec_module(protocol)
env=os.environ.copy();env['DOTNET_ROOT']='/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148'
with subprocess.Popen([env['DOTNET_ROOT']+'/dotnet',str(root/'candidate/Lokad.Lython.Benchmarks.dll'),'--compare','worker','--catalog',str(root/'representative.json')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,env=env) as p:
    ready=protocol.read_frame(p.stdout)
    protocol.write_frame(p.stdin,dict(protocolVersion=2,requestId=1,operation='quit'))
    assert protocol.read_frame(p.stdout)['status']=='Closed'
    assert p.wait(timeout=10)==0 and not p.stderr.read()
identity=json.loads((root/'identity.json').read_text())
core=next(a for a in ready['libraries'] if Path(a['path']).name=='Lokad.Lython.dll')
assert core['sha256']==identity['coreSHA256']['candidate'],core
for label in ['candidate','reference']:
    for name,digest in identity['auxiliaryFiles'].items():assert hashlib.sha256((root/label/name).read_bytes()).hexdigest()==digest
(root/'startup-path-correction.json').write_text(json.dumps(dict(cause='Symlinked main worker DLL resolved beside B336 core; copy only main assembly and host manifests to preserve intended dependency directory',ready=ready,freeBytesBeforeLaunch=shutil.disk_usage(root).free),indent=2)+'\n',encoding='utf-8')
assert shutil.disk_usage(root).free>24*1024*1024
subprocess.run(['python3',str(root/'launch.py')],check=True)
