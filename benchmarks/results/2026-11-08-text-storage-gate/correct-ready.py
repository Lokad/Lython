from pathlib import Path
import hashlib,json,shutil,subprocess
root=Path(__file__).resolve().parent
failed=root/'failed-b432';failed.mkdir()
for name in ['launch-command.py','launch.log','payload.tgz','terminal-status.json','observe-not-before.json','profile.py']:
 shutil.copyfile(root/name,failed/name)
ssh=['C:/Windows/System32/OpenSSH/ssh.exe','-i','C:/Users/JoannesVermorel/.ssh/id_lython-bench.pem','-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73']
script='''from pathlib import Path
import fcntl,hashlib,json,os,subprocess,tarfile
base=Path('/home/lokad/lython-bench-checks');root=base/'text-allocation-b432-20261108'
with (base/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert (root/'exit-code.txt').read_text().strip()=='1'
 assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[c]ollector.dll'],capture_output=True).returncode==1
 assert not list((root/'guest-fixtures').iterdir())
 subprocess.run(['sudo','-n','umount',str(root/'guest-fixtures')],check=True)
 assert os.stat(root/'guest-fixtures').st_dev==os.stat(root).st_dev
 logs=subprocess.check_output(['journalctl','-u','lython-b432-text-allocation-20261108.service','--no-pager'])
 (root/'failure-journal.txt').write_bytes(logs)
 assert b'StopIteration' in logs and not (root/'text/allocation-ticks.jsonl').exists()
 with tarfile.open(root/'failure-observations.tgz','w:gz') as a:
  for p in root.rglob('*'):
   if p.is_file() and p.name!='failure-observations.tgz' and not {'candidate','guest-fixtures'} & set(p.relative_to(root).parts):a.add(p.resolve(),arcname=str(p.relative_to(root)))
 receipt=dict(complete=True,failedBeforeGuestRequestsOrCollectorStartup=True,allUsersJoined=True,ownedFixturesRemovedAndUnmounted=True,archiveSHA256=hashlib.sha256((root/'failure-observations.tgz').read_bytes()).hexdigest(),cause='Ready advertises loaded core, worker and CoreLib, not unloaded optional backend libraries. All staged auxiliaries were independently SHA-verified.')
 (root/'failure-archive-receipt.json').write_text(json.dumps(receipt,indent=2)+'\\n');print(json.dumps(receipt))
'''
r=subprocess.run(ssh+['python3 -'],input=script,text=True,encoding='utf-8',capture_output=True);(failed/'archive.log').write_text(r.stdout+'\n'+r.stderr,encoding='utf-8');assert r.returncode==0,r.stderr
for name in ['failure-observations.tgz','failure-archive-receipt.json']:
 r=subprocess.run(['C:/Windows/System32/OpenSSH/scp.exe','-i',ssh[2],'-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73:/home/lokad/lython-bench-checks/text-allocation-b432-20261108/'+name,str(failed/name)],capture_output=True);assert r.returncode==0,r.stderr
assert hashlib.sha256((failed/'failure-observations.tgz').read_bytes()).hexdigest()==json.loads((failed/'failure-archive-receipt.json').read_text())['archiveSHA256']
p=root/'prepare.py';s=p.read_text().replace('text-allocation-b432-20261108','text-allocation-b433-20261108')
lines=s.splitlines();lines=[line for line in lines if not line.startswith('source=source.replace("assert not ready')]
insert=next(i for i,line in enumerate(lines)if line.startswith("compile(source,'profile.py'"))
lines.insert(insert,"source=source.replace(\"assert not ready['serverGc']\", \"assert next(v for v in ready['libraries'] if Path(v['path']).name=='Lokad.Lython.Benchmarks.dll')['sha256'].lower()==identity['auxiliaryFiles']['Lokad.Lython.Benchmarks.dll']\\n            (folder/'ready.json').write_text(json.dumps(ready,indent=2)+'\\\\n')\\n            assert not ready['serverGc']\")")
s='\n'.join(lines)+'\n';compile(s,'prepare','exec');p.write_text(s,encoding='utf-8',newline='\n')
p=root/'launch-corrected.py';s=p.read_text().replace('text-allocation-b432','text-allocation-b433').replace('lython-b432','lython-b433');compile(s,'launch','exec');p.write_text(s,encoding='utf-8',newline='\n')
p=root/'finish.py';s=p.read_text().replace('text-allocation-b432','text-allocation-b433').replace('lython-b432','lython-b433');p.write_text(s,encoding='utf-8',newline='\n')
(root/'terminal-status.json').rename(root/'b432-terminal-status.json')
print('B432 failure archived, locally verified, all users joined/fixtures unmounted. Ready check corrected to loaded worker/core; staged auxiliaries retain exact hashes.')
