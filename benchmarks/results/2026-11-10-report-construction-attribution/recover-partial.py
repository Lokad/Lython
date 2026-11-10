from pathlib import Path
import hashlib,json,subprocess,tarfile
r=Path(__file__).parent
ssh=['C:/Windows/System32/OpenSSH/ssh.exe','-i','C:/Users/JoannesVermorel/.ssh/id_lython-bench.pem','-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73']
script=r'''from pathlib import Path
import fcntl,hashlib,json,os,shutil,stat,subprocess,tarfile
r=Path('/home/lokad/lython-bench-checks/report-assessment-b543-20261110');fixture=Path('/dev/shm/lython-b543')
with(r.parent/'comparison-vm.lock').open('a')as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 service=subprocess.check_output(['systemctl','show','lython-b543-report-assessment-20261110.service','--property=ActiveState,MainPID,Result,ExecMainStatus'],text=True)
 assert 'MainPID=0'in service and'ActiveState=failed'in service
 assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[p]erf record'],capture_output=True).returncode==1
 assert not(r/'partial-recovery.json').exists()
 journal=subprocess.check_output(['sudo','-n','journalctl','-u','lython-b543-report-assessment-20261110.service','--no-pager'],text=True)
 (r/'failed-journal.txt').write_text(journal)
 analyzer=(r/'analyze-cpu-guest.py').read_text()
 needle='guest_tids = set(record["guestTids"])'
 assert needle in analyzer
 analyzer=analyzer.replace(needle,'guest_tids = set(json.loads((folder / "analysis-targets.json").read_text())["tids"])')
 (r/'analyze-partial.py').write_text(analyzer)
 pids=[];mapRetirements=[];captures=[]
 for name in ['report-warm','report-compile']:
  folder=r/'profiles'/name;record=json.loads((folder/'record.json').read_text());pid=record['identity']['processId'];pids.append(pid)
  assert not Path('/proc/'+str(pid)).exists()
  for prefix in ['perf','perfinfo']:
   temporary=Path('/tmp/'+prefix+'-'+str(pid)+'.map')
   if temporary.exists():
    assert not temporary.is_symlink()and stat.S_ISREG(temporary.lstat().st_mode)and temporary.stat().st_uid==os.getuid()
    dest=folder/(prefix+'.map');shutil.copyfile(temporary,dest)
    digest=hashlib.sha256(temporary.read_bytes()).hexdigest();assert hashlib.sha256(dest.read_bytes()).hexdigest()==digest
    temporary.unlink();mapRetirements.append(dict(path=str(temporary),sha256=digest,preservedAs=str(dest.relative_to(r))))
  targets=[t for t in record['threadCpuSeconds']if t['seconds']>.25 and(t['name'].startswith('Lython executio')if name.endswith('warm')else t['name']=='.NET TP Worker')]
  assert targets and sum(t['seconds']for t in targets)>5
  (folder/'analysis-targets.json').write_text(json.dumps(dict(tids=[t['tid']for t in targets],source='Recorded actual CPU/thread names; compilation continued across thread-pool threads, not process main. Original record retained unchanged.',threads=targets),indent=2)+'\n')
  subprocess.run(['python3',str(r/'analyze-partial.py'),str(folder)],check=True,timeout=30)
  subprocess.run(['sudo','-n','chown','lokad:lokad',str(folder/'perf.data')],check=True)
  captures.append(dict(name=name,exitCode=record['exitCode'],normalClose=record.get('closed',{}).get('status')=='Closed',verificationAfter=record.get('verificationAfter',{}).get('status'),
   capturedSeconds=record['response']['elapsedTicks']/record['response']['clockFrequency'],completedInvocations=record['response']['completedInvocations'],targets=targets))
 endpoints=[]
 assert fixture.is_mount()
 for path in fixture.iterdir():
  s=path.lstat();assert path.resolve().is_relative_to(fixture)and s.st_uid==os.getuid()and(stat.S_ISFIFO(s.st_mode)or stat.S_ISSOCK(s.st_mode))
  assert any(str(pid)in path.name for pid in pids)and path.name.startswith(('clr-debug-pipe-','dotnet-diagnostic-'))
  endpoints.append(dict(path=str(path),mode=s.st_mode,bytes=s.st_size));path.unlink()
 assert not list(fixture.iterdir())and not list(Path('/dev/shm').glob('lython-comparison-files-*'))
 subprocess.run(['sudo','-n','umount',str(fixture)],check=True);fixture.rmdir()
 recovery=dict(complete=False,terminalService=service,allWorkersJoined=True,guestFixturesRemoved=True,originalFailure='Collector selected process main for compilation, but actual CPU was on three thread-pool threads; failure before normal compile-worker close.',
  missing=['config-warm','config-compile','normal compile-worker close/post-verification','final quiet gate'],captures=captures,mapRetirements=mapRetirements,diagnosticEndpointsRetiredAfterJoin=endpoints,rerun=False,sourceUnchanged=True)
 (r/'partial-recovery.json').write_text(json.dumps(recovery,indent=2)+'\n')
 with tarfile.open(r/'observations-partial.tgz','w:gz',dereference=True)as a:
  for path in sorted(r.rglob('*')):
   if path.is_file()and path.name!='observations-partial.tgz'and'candidate'not in path.relative_to(r).parts:a.add(path,arcname=path.relative_to(r).as_posix())
 assert shutil.disk_usage(r).free>240*1024*1024
 receipt=dict(complete=False,allWorkersJoined=True,guestFixturesRemoved=True,archiveSHA256=hashlib.sha256((r/'observations-partial.tgz').read_bytes()).hexdigest(),archivedAndVerifiedUnderLease=True,
  performanceQualified=False,missing=recovery['missing'],freeBytesAfterArchive=shutil.disk_usage(r).free)
 with tarfile.open(r/'observations-partial.tgz')as a:assert not json.loads(a.extractfile('partial-recovery.json').read())['complete']
 (r/'partial-archive-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt),flush=True)
'''
(r/'partial-recovery-command.py').write_text(script,encoding='utf-8',newline='\n')
done=subprocess.run(ssh+['python3 -'],input=script,text=True,encoding='utf-8',capture_output=True)
(r/'partial-recovery.log').write_text(done.stdout+'\n'+done.stderr,encoding='utf-8',newline='\n')
assert done.returncode==0,done.stderr
print(done.stdout,flush=True)
scp=['C:/Windows/System32/OpenSSH/scp.exe','-i',ssh[2],'-o','BatchMode=yes','-o','ConnectTimeout=15']
remote='/home/lokad/lython-bench-checks/report-assessment-b543-20261110'
for name in ['partial-archive-receipt.json','observations-partial.tgz']:
 done=subprocess.run(scp+['lokad@20.166.121.73:'+remote+'/'+name,str(r/name)],capture_output=True,text=True,encoding='utf-8');assert done.returncode==0,done.stderr
receipt=json.loads((r/'partial-archive-receipt.json').read_text(encoding='utf-8'))
assert hashlib.sha256((r/'observations-partial.tgz').read_bytes()).hexdigest()==receipt['archiveSHA256']
out=r/'retrieved';out.mkdir()
with tarfile.open(r/'observations-partial.tgz')as a:
 members=a.getmembers();regular=[m for m in members if m.isfile()or m.isdir()]
 assert all(m.isfifo()and m.size==0 for m in members if m not in regular)
 a.extractall(out,members=regular,filter='data')
 (r/'extraction.json').write_text(json.dumps(dict(originalArchivePreserved=True,skippedOnlyLinuxFifos=[m.name for m in members if m not in regular]),indent=2)+'\n',encoding='utf-8',newline='\n')
print('Partial failed condition preserved, exact archive SHA verified; no users or RAM fixtures remain.',flush=True)
