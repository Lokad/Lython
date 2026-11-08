from pathlib import Path
import hashlib,json,tarfile

root=Path(__file__).resolve().parent
old=root.parent/'json-float-entry-r74'
collector=root.parent/'activation-allocation-d36/collector'
identity=json.loads((old/'vm-identity.json').read_text())
identity.update(sourceRevision='33b6c19128cbfe4ae961875229417237355cd206',coreSHA256=identity['coreSHA256']['candidate'],purpose='R75 current sampled allocation traffic; not CPU, live heap or exact object counts')
identity['collectorFiles']={}
for name in ['collector.dll','collector.deps.json','collector.runtimeconfig.json','Microsoft.Diagnostics.NETCore.Client.dll','Microsoft.Diagnostics.Tracing.TraceEvent.dll','Microsoft.Diagnostics.FastSerialization.dll']:
    data=(collector/'bin/Release/net11.0'/name).read_bytes()
    identity['collectorFiles'][name]=hashlib.sha256(data).hexdigest()
    if name.startswith('Microsoft.'):
        assert hashlib.sha256((old/'images/candidate'/name).read_bytes()).hexdigest()==identity['collectorFiles'][name]
(root/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
catalog=json.loads((old/'representative.json').read_text())
catalog['cases']=[c for c in catalog['cases'] if c['id']=='scripts.text-summary-helpers.large']
assert len(catalog['cases'])==1
(root/'catalog.json').write_text(json.dumps(catalog,indent=2)+'\n',encoding='utf-8')
plan='''# R75 current text allocation attribution

One complete maintained text-summary helper script, source33b6c191/corebb6e72b5,
unchanged fixtures/goldens/worker and normal workstation GC/tiering. Question:
does boxed exact-integer traffic or text/list construction warrant a coherent
scalar/operand or construction redesign after current call/sequence changes?
Reuse the existing external managed EventPipe allocation-tick collector unchanged.
Six guest seconds warmup, then1024 complete warm jobs, one capture. Approximate
tick attribution is not exact object counts, retained memory, CPU attribution or
normal elapsed evidence. Keep all threads/events, event losses and object sizes;
select the named execution worker by actual positive CPU delta and capture time.
No forced GC, heap tracing, root inventory, counters in production or dependency.
Verify outputs/retained funding before and after; join collector/worker, remove
owned RAM fixtures and archive persistently under the VM lease.120+30+30s bound.
Stop after usable attribution or explicit missing evidence. Select a substantial
phase with identity/alias/cancellation/admission semantics before any prototype;
do not automatically promote boxed values or revive withdrawn shared windows.
'''
(root/'selected-plan.md').write_text(plan,encoding='utf-8')
source=(root.parent/'activation-allocation-d36/profile.py').read_text()
source=source.replace('activation-allocation-b261-20261105','text-allocation-b434-20261108').replace("base=Path('/home/lokad/lython-bench-checks/runtime-cpython-b260-20261105/candidate')","base=root/'candidate'")
source=source.replace("env['TMPDIR']='/dev/shm';tempfile.tempdir='/dev/shm'","env['TMPDIR']=str(root/'guest-fixtures');tempfile.tempdir=str(root/'guest-fixtures');assert os.stat(root/'guest-fixtures').st_dev!=os.stat('/tmp').st_dev")
source=source.replace('captureInvocations=1024','captureInvocations=1024')
source=source.replace("guest=max(after,key=lambda tid:after[tid]['cpuTicks']-before.get(tid,dict(cpuTicks=0))['cpuTicks'])","candidates=[tid for tid in after if after[tid]['name'].startswith('Lython execut') and after[tid]['cpuTicks']>before.get(tid,dict(cpuTicks=0))['cpuTicks']];assert candidates,after\n            guest=max(candidates,key=lambda tid:after[tid]['cpuTicks']-before.get(tid,dict(cpuTicks=0))['cpuTicks'])")
source=source.replace("assert not ready['serverGc']", "assert next(v for v in ready['libraries'] if Path(v['path']).name=='Lokad.Lython.Benchmarks.dll')['sha256'].lower()==identity['auxiliaryFiles']['Lokad.Lython.Benchmarks.dll']\n            (folder/'ready.json').write_text(json.dumps(ready,indent=2)+'\\n')\n            assert not ready['serverGc']")
source=source.replace("str(root/'guest-fixtures')", "'/tmp/lython-b434'")
compile(source,'profile.py','exec');(root/'profile.py').write_text(source,encoding='utf-8',newline='\n')
archive='''from pathlib import Path
import fcntl,hashlib,json,os,subprocess,tarfile,time
root=Path('/home/lokad/lython-bench-checks/text-allocation-b434-20261108')
dotnet='/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet'
with (root.parent/'comparison-vm.lock').open('a') as lease:
 fcntl.flock(lease,fcntl.LOCK_EX|fcntl.LOCK_NB)
 time.sleep(5)
 for phase in ['before','after']:
  if phase=='after':
   subprocess.run(['python3',str(root/'profile.py')],check=True)
   time.sleep(5)
  subprocess.run([dotnet,str(root/'candidate/Lokad.Lython.Benchmarks.dll'),'--compare','check-machine','--out',str(root/('machine-'+phase+'.json'))],check=True)
  assert json.loads((root/('machine-'+phase+'.json')).read_text())['gate']['quiet']
 records=json.loads((root/'records.json').read_text());assert len(records)==1
 assert all(r['workerClosedAndJoined'] and r['collectorJoined'] and r['collector']['eventsLost']==0 for r in records)
 assert not list((root/'guest-fixtures').iterdir())
 assert subprocess.run(['pgrep','-af','[d]otnet.* --compare worker|[c]ollector.dll'],capture_output=True).returncode==1
 alias=Path('/tmp/lython-b434');assert alias.is_symlink() and alias.resolve()==(root/'guest-fixtures').resolve();alias.unlink()
 subprocess.run(['sudo','-n','umount',str(root/'guest-fixtures')],check=True)
 assert os.stat(root/'guest-fixtures').st_dev==os.stat(root).st_dev
 names=['text','records.json','reclaimed-extracts.json','identity.json','catalog.json','profile.py','selected-plan.md','machine-before.json','machine-after.json','setup.json','run.sh','archive-owned.py','started.utc','collector/Program.cs','collector/collector.csproj','collector/collector.dll','collector/collector.deps.json','collector/collector.runtimeconfig.json','current-core.dll','cpython-worker.py','original-failed-setup']
 with tarfile.open(root/'observations.tgz','w:gz') as t:
  for name in names:t.add((root/name).resolve(),arcname=name)
 with tarfile.open(root/'observations.tgz') as t:assert len(json.loads(t.extractfile('records.json').read()))==1
 receipt=dict(complete=True,jobs=1,allWorkersJoined=True,collectorJoined=True,guestFixtureDirectories=[],quietBeforeAndAfter=True,archivedAndVerifiedUnderLease=True,archiveSHA256=hashlib.sha256((root/'observations.tgz').read_bytes()).hexdigest())
 (root/'archive-receipt.json').write_text(json.dumps(receipt,indent=2)+'\\n');print(json.dumps(receipt),flush=True)
'''
compile(archive,'archive-owned.py','exec');(root/'archive-owned.py').write_text(archive,encoding='utf-8',newline='\n')
(root/'run.sh').write_text((old/'run.sh').read_text().replace('json-float-entry-b430-20261108','text-allocation-b434-20261108').replace('collect.py','archive-owned.py'),encoding='utf-8',newline='\n')
with tarfile.open(root/'payload.tgz','w:gz') as t:
    for name in ['identity.json','catalog.json','profile.py','selected-plan.md','archive-owned.py','run.sh']:t.add(root/name,arcname=name)
    t.add(root/'launch-command.py',arcname='original-failed-setup/launch.py')
    t.add(root/'launch.log',arcname='original-failed-setup/launch.log')
    for name in ['Program.cs','collector.csproj']:t.add(collector/name,arcname='collector/'+name)
    for name in ['collector.dll','collector.deps.json','collector.runtimeconfig.json']:t.add(collector/'bin/Release/net11.0'/name,arcname='collector/'+name)
print('Prepared bounded current capture', (root/'payload.tgz').stat().st_size)
