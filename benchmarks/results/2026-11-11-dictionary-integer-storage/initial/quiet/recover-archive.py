from pathlib import Path
import subprocess
r=Path(__file__).parent
assert not(r/'archive-recovery.json').exists()
ssh=['C:/Windows/System32/OpenSSH/ssh.exe','-i','C:/Users/JoannesVermorel/.ssh/id_lython-bench.pem','-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73']
script=r'''from pathlib import Path
import datetime,fcntl,json,subprocess
r=Path('/home/lokad/lython-bench-checks/dictionary-values-b588-20261111')
with(r.parent/'comparison-vm.lock').open('a')as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert not(r/'archive-receipt.json').exists()
 assert(r/'exit-code.txt').read_text().strip()=='1'
 raw=json.loads((r/'confirmation/confirmation.json').read_text())
 assert raw['complete']and len(raw['jobs'])==6 and all(j['workersJoinedAndFixturesRemoved']for j in raw['jobs'])
 assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[p]ython3.*cpython-worker.py'],capture_output=True).returncode==1
 assert not(r/'guest-fixtures').exists()
 original=(r/'archive-owned.py').read_bytes()
 (r/'archive-owned-under-service.py').write_bytes(original)
 corrected=original.decode().replace("len(raw['jobs'])==5","len(raw['jobs'])==6").replace('jobs=5,elapsedSeconds','jobs=6,elapsedSeconds')
 assert corrected!=original.decode()
 (r/'archive-owned.py').write_text(corrected)
 recovery=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),freshExclusiveLease=True,collectionRepeated=False,originalServiceExitCode=1,reason='All six observations complete and joined; original archive assertion incorrectly expected five. Correct only actual six-job count; preserve original helper/service failure. Raw records were persistent throughout RAM cleanup.')
 (r/'archive-recovery.json').write_text(json.dumps(recovery,indent=2)+'\n')
 (r/'failed-service-journal.txt').write_text(subprocess.check_output(['sudo','-n','journalctl','-u','lython-b588-dictionary-values-20261111.service','--no-pager','-n','40'],text=True))
 subprocess.run(['python3',str(r/'archive-owned.py')],check=True)
 receipt=json.loads((r/'archive-receipt.json').read_text());receipt.update(recoveredAfterServiceExit=True,originalServiceExitCode=1)
 (r/'archive-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
 print(json.dumps(recovery))
'''
done=subprocess.run(ssh+['python3 -'],input=script,text=True,encoding='utf-8',capture_output=True)
(r/'archive-recovery.stdout').write_text(done.stdout,encoding='utf-8');(r/'archive-recovery.stderr').write_text(done.stderr,encoding='utf-8')
assert done.returncode==0,done.stderr
# Keep the failed first retriever unchanged; the recovered receipt keeps exit1.
s=(r/'retrieve.py').read_text(encoding='utf-8')
s=s.replace("assert status['exitCode']=='0' and status['receipt']['complete']", "assert status['exitCode']=='1' and status['receipt']['recoveredAfterServiceExit'] and status['receipt']['originalServiceExitCode']==1 and status['receipt']['complete']")
(r/'retrieve-recovered.py').write_text(s,encoding='utf-8')
print(done.stdout)
