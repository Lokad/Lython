from pathlib import Path
import hashlib,json,shutil,subprocess
root=Path(__file__).resolve().parent;failed=root/'failed-b433';failed.mkdir()
for name in ['launch-command.py','launch.log','payload.tgz','terminal-status.json','observe-not-before.json','profile.py']:
 shutil.copyfile(root/name,failed/name)
ssh=['C:/Windows/System32/OpenSSH/ssh.exe','-i','C:/Users/JoannesVermorel/.ssh/id_lython-bench.pem','-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73']
script='''from pathlib import Path
import fcntl,hashlib,json,os,subprocess,tarfile
base=Path('/home/lokad/lython-bench-checks');root=base/'text-allocation-b433-20261108'
with (base/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert (root/'exit-code.txt').read_text().strip()=='1'
 assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[c]ollector.dll'],capture_output=True).returncode==1
 assert not list((root/'guest-fixtures').iterdir())
 subprocess.run(['sudo','-n','umount',str(root/'guest-fixtures')],check=True)
 assert os.stat(root/'guest-fixtures').st_dev==os.stat(root).st_dev
 (root/'failure-journal.txt').write_bytes(subprocess.check_output(['journalctl','-u','lython-b433-text-allocation-20261108.service','--no-pager']))
 assert '108 characters' in (root/'text/collector.stderr').read_text() and not (root/'text/allocation-ticks.jsonl').exists()
 with tarfile.open(root/'failure-observations.tgz','w:gz') as a:
  for p in root.rglob('*'):
   if p.is_file() and p.name!='failure-observations.tgz' and not {'candidate','guest-fixtures'} & set(p.relative_to(root).parts):a.add(p.resolve(),arcname=str(p.relative_to(root)))
 receipt=dict(complete=True,failedBeforeCapture=True,allUsersJoined=True,ownedFixturesRemovedAndUnmounted=True,archiveSHA256=hashlib.sha256((root/'failure-observations.tgz').read_bytes()).hexdigest(),cause='Linux diagnostic socket path exceeds108characters under the long owned TMPDIR; no collector attachment or allocation events. Correct with a short owned TMPDIR alias to the identical RAM fixture mount.')
 (root/'failure-archive-receipt.json').write_text(json.dumps(receipt,indent=2)+'\\n');print(json.dumps(receipt))
'''
r=subprocess.run(ssh+['python3 -'],input=script,text=True,encoding='utf-8',capture_output=True);(failed/'archive.log').write_text(r.stdout+'\n'+r.stderr,encoding='utf-8');assert r.returncode==0,r.stderr
for name in ['failure-observations.tgz','failure-archive-receipt.json']:
 r=subprocess.run(['C:/Windows/System32/OpenSSH/scp.exe','-i',ssh[2],'-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73:/home/lokad/lython-bench-checks/text-allocation-b433-20261108/'+name,str(failed/name)],capture_output=True);assert r.returncode==0,r.stderr
assert hashlib.sha256((failed/'failure-observations.tgz').read_bytes()).hexdigest()==json.loads((failed/'failure-archive-receipt.json').read_text())['archiveSHA256']
p=root/'prepare.py';s=p.read_text().replace('text-allocation-b433-20261108','text-allocation-b434-20261108')
line="compile(source,'profile.py','exec');"
assert line in s
s=s.replace(line,"source=source.replace(\"str(root/'guest-fixtures')\", \"'/tmp/lython-b434'\")\n"+line)
s=s.replace(" subprocess.run(['sudo','-n','umount',str(root/'guest-fixtures')],check=True)"," alias=Path('/tmp/lython-b434');assert alias.is_symlink() and alias.resolve()==(root/'guest-fixtures').resolve();alias.unlink()\n subprocess.run(['sudo','-n','umount',str(root/'guest-fixtures')],check=True)")
compile(s,'prepare','exec');p.write_text(s,encoding='utf-8',newline='\n')
p=root/'launch-corrected.py';s=p.read_text().replace('text-allocation-b433','text-allocation-b434').replace('lython-b433','lython-b434')
line=" for name in ['profile.py','archive-owned.py']:"
assert line in s
s=s.replace(line," alias=Path('/tmp/lython-b434');assert not alias.exists() and not alias.is_symlink();alias.symlink_to(fixture,target_is_directory=True)\n assert len(str(alias)+'/dotnet-diagnostic-9999999-99999999-socket')<108\n"+line)
compile(s,'launch','exec');p.write_text(s,encoding='utf-8',newline='\n')
p=root/'finish.py';s=p.read_text().replace('text-allocation-b433','text-allocation-b434').replace('lython-b433','lython-b434');p.write_text(s,encoding='utf-8',newline='\n')
(root/'terminal-status.json').rename(root/'b433-terminal-status.json')
print('B433 failure archived/locally verified; no capture. Short owned fixture alias corrects Linux diagnostic socket representation bound; source/runtime/protocol/goldens unchanged.')
