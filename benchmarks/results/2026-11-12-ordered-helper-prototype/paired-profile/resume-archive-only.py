from pathlib import Path
import hashlib,json,subprocess,tarfile
r=Path(__file__).parent;ssh=['C:/Windows/System32/OpenSSH/ssh.exe','-i','C:/Users/JoannesVermorel/.ssh/id_lython-bench.pem','-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73'];remote='/home/lokad/lython-bench-checks/ordered-helper-profile-b619-20261112'

status=json.loads((r/'original-archive-terminal.json').read_text(encoding='utf-8'));out=r/'retrieved'
assert all(json.loads((out/n).read_text(encoding='utf-8'))['gate']['quiet']for n in ['machine-before.json','machine-after.json'])
identity=json.loads((r/'identity.json').read_text(encoding='utf-8'));seconds=0
for role in ['reference','candidate']:
 p=out/'profiles'/('native-search-'+role);rec=json.loads((p/'record.json').read_text(encoding='utf-8'));assert rec['exitCode']==0 and rec['closed']['status']=='Closed'and all(rec[n]['status']=='Equivalent'for n in ['verificationBefore','verificationAfter'])
 assert not(p/'worker.stderr').stat().st_size and rec['redundantTemporaryMapsRetiredAfterJoin'];core=next(x for x in rec['identity']['libraries']if Path(x['path']).name=='Lokad.Lython.dll');assert core['sha256'].lower()==identity['coreSHA256'][role]
 seconds+=rec['response']['elapsedTicks']/rec['response']['clockFrequency']
assert seconds<16
compact=r/'observations.tgz';payload=compact.read_bytes();h=hashlib.sha256(payload).hexdigest()
config=dict(original=status,compactBytes=len(payload),compactSHA256=h,capturedSeconds=seconds)
script='''from pathlib import Path
import fcntl,hashlib,json,os,shutil,subprocess,sys,tarfile
c=CONFIG;r=Path('/home/lokad/lython-bench-checks/ordered-helper-profile-b619-20261112')
with(r.parent/'comparison-vm.lock').open('a')as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 assert not subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record|[p]ython3 [^ ]*/cpython-worker[.]py'],capture_output=True,text=True).stdout
 assert not(r/'guest-fixtures').exists()
 p=r/'observations.tgz';s=p.lstat();assert p.resolve()==p and p.parent==r and s.st_uid==os.getuid()and s.st_nlink==1 and s.st_ino==c['original']['inode']and s.st_size==c['original']['archiveBytes']
 assert hashlib.sha256(p.read_bytes()).hexdigest()==c['original']['archiveSHA256']
 b=sys.stdin.buffer.read(c['compactBytes']+1);assert len(b)==c['compactBytes']and hashlib.sha256(b).hexdigest()==c['compactSHA256']
 assert shutil.disk_usage(r).free+s.st_size-len(b)>240*1024*1024
 # The original archive is independently verified in persistent local storage;
 # compact bytes preserve every non-redundant original entry, not new samples.
 p.unlink();p.write_bytes(b);assert hashlib.sha256(p.read_bytes()).hexdigest()==c['compactSHA256']
 with tarfile.open(p)as t:
  assert not any('reference'in Path(m.name).parts or'candidate'in Path(m.name).parts for m in t.getmembers())
  for role in ['reference','candidate']:assert json.loads(t.extractfile('profiles/native-search-'+role+'/record.json').read())['exitCode']==0
 receipt=dict(complete=True,allWorkersJoined=True,guestFixturesRemoved=True,quietBeforeAndAfter=True,archivedAndVerifiedUnderLease=True,capturedSeconds=c['capturedSeconds'],archiveSHA256=c['compactSHA256'],freeBytes=shutil.disk_usage(r).free,originalServiceFailed=True,originalArchiveSHA256=c['original']['archiveSHA256'],archivalCorrectionOnly=True,reason='Original archive included redundant reference image and failed post-write floor. Every original sampling/identity/verification/close/quiet gate passed. Corrected packaging only; original archive/members/failure retained locally, no sampling replay.',performanceQualified=False)
 assert receipt['freeBytes']>240*1024*1024
 (r/'archive-receipt.json').write_text(json.dumps(receipt,indent=2)+'\\n');print(json.dumps(receipt))
'''.replace('c=CONFIG','c='+repr(config))
# Supply code separately so stdin remains the exact compact archive payload.
import shlex
done=subprocess.run(ssh+['python3 -c '+shlex.quote(script)],input=payload,capture_output=True);assert done.returncode==0,done.stderr.decode('utf-8');(r/'archive-receipt.json').write_bytes(done.stdout);(r/'terminal-status.json').write_text(json.dumps(dict(original=status,correctedArchive=json.loads(done.stdout)),indent=2)+'\n',encoding='utf-8');print(done.stdout.decode('utf-8'))
for role in ['reference','candidate']:
 p=out/'profiles'/('native-search-'+role)/'guest-cpu-leaves.json';a=json.loads(p.read_text(encoding='utf-8'));print(role,'samples',a['samples'],'unknown',a['unknown'],'CPU',a['cpuSeconds'])
 for name,n in a['topLeafCpuSamples'][:15]:print(n,name[:235])
