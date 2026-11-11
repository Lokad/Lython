from pathlib import Path
import datetime,hashlib,json,subprocess,tarfile
r=Path(__file__).parent;assert not (r/'terminal-status.json').exists()
bound=json.loads((r/'observe-not-before.json').read_text(encoding='utf-8'))
assert datetime.datetime.now(datetime.timezone.utc)>=datetime.datetime.fromisoformat(bound['doNotObserveBeforeUTC'])
ssh=['C:/Windows/System32/OpenSSH/ssh.exe','-i','C:/Users/JoannesVermorel/.ssh/id_lython-bench.pem','-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73']
remote='/home/lokad/lython-bench-checks/native-inputs-b597-20261112'
script='''from pathlib import Path
import fcntl,json,subprocess
root=Path('/home/lokad/lython-bench-checks/native-inputs-b597-20261112')
lease=(root.parent/'comparison-vm.lock').open('a');fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
service=subprocess.run(['systemctl','show','lython-b597-native-inputs-20261112.service','--property=ActiveState,SubState,MainPID,Result,ExecMainStatus'],text=True,capture_output=True)
print(json.dumps(dict(service=service.stdout,serviceError=service.stderr,exitCode=(root/'exit-code.txt').read_text().strip() if (root/'exit-code.txt').exists() else None,
 receipt=json.loads((root/'archive-receipt.json').read_text()) if (root/'archive-receipt.json').exists() else None,
 remainingWorkers=subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[p]ython3.*cpython-worker.py'],capture_output=True,text=True).stdout,
 remainingFixtures=[str(p) for p in Path('/dev/shm').glob('lython-comparison-files-*')],remainingTrace=(root/'guest-fixtures').exists())))
'''
done=subprocess.run(ssh+['python3 -'],input=script,text=True,encoding='utf-8',capture_output=True);assert done.returncode==0,done.stderr
(r/('observation-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'.json')).write_text(done.stdout,encoding='utf-8');status=json.loads(done.stdout);print(status,flush=True)
assert status['exitCode']=='0' and status['receipt']['complete'] and status['receipt']['allWorkersJoined']
assert status['receipt']['archivedAndVerifiedUnderLease'] and status['receipt']['jobs']==10 and status['receipt']['elapsedSeconds']<330
assert 'MainPID=0' in status['service'] and 'ActiveState=active' not in status['service']
assert not status['remainingWorkers'] and not status['remainingFixtures'] and not status['remainingTrace']
(r/'terminal-status.json').write_text(done.stdout,encoding='utf-8')
scp=['C:/Windows/System32/OpenSSH/scp.exe','-i',ssh[2],'-o','BatchMode=yes','-o','ConnectTimeout=15']
for name in ['archive-receipt.json','observations.tgz']:
    done=subprocess.run(scp+['lokad@20.166.121.73:'+remote+'/'+name,str(r/name)],capture_output=True,text=True,encoding='utf-8');assert done.returncode==0,done.stderr
receipt=json.loads((r/'archive-receipt.json').read_text(encoding='utf-8'))
assert hashlib.sha256((r/'observations.tgz').read_bytes()).hexdigest()==receipt['archiveSHA256']
out=r/'retrieved';out.mkdir()
with tarfile.open(r/'observations.tgz') as t:
    members=t.getmembers(); regular=[m for m in members if m.isfile() or m.isdir()]
    skipped=[dict(name=m.name,type=m.type.decode('ascii'),size=m.size) for m in members if m not in regular]
    assert all(m.isfifo() and m.size==0 for m in members if m not in regular)
    t.extractall(out,members=regular,filter='data')
    (r/'extraction.json').write_text(json.dumps(dict(originalArchivePreserved=True,skippedOnlyLinuxFifos=skipped),indent=2)+'\n',encoding='utf-8',newline='\n')
print('B597 five-job raw observations/cores independently SHA-verified; terminal cached, no remaining VM users or RAM fixtures.',flush=True)
