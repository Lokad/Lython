from pathlib import Path
import ast,hashlib,io,json,subprocess,tarfile

root=Path(__file__).resolve().parent;repo=Path('D:/code/Lython')
old=root.parent/'bound-native-calls-r214/b564';out=root/'b565';out.mkdir()
write=lambda p,s:p.write_text(s,encoding='utf-8',newline='\n')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
catalog=json.loads((root/'catalog.json').read_text(encoding='utf-8'))
ids=['scripts.report-render.large','diagnostic.report-render.escaped-unicode.large','scripts.text-summary-helpers.large']
catalog['cases']=[next(c for c in catalog['cases']if c['id']==name)for name in ids]
write(out/'representative.json',json.dumps(catalog,ensure_ascii=False,indent=2)+'\n')
before=root/'images/before/Lokad.Lython.dll';candidate=root/'images/candidate/Lokad.Lython.dll'
base=json.loads((root.parent/'cpython-construction-r209/b561/identity.json').read_text(encoding='utf-8'))
identity=dict(sourceRevision='R217 complete JSON string decoding: fresh forced b8b05ccc/qualified69644713 reference versus exact two-file JSON prototype. 192internal/811public JSON checks per actual11/10 pass; desktop adversity preserved. Identical fixed worker/host0.4 auxiliaries; no final retention or CPU claim.',
    coreSHA256={n:sha(before if n.startswith('reference')else candidate)for n in ['reference','reference-control','candidate','candidate-control']},
    auxiliaryFiles=base['auxiliaryFiles'],catalogSHA256=sha(out/'representative.json'),performanceQualified=False)
write(out/'identity.json',json.dumps(identity,indent=2)+'\n')
for name in ['collect.py','archive-owned.py','run.sh','retrieve.py','summarize.py']:
    s=(old/name).read_text(encoding='utf-8').replace('bound-borrow-b564-20261110','json-string-b565-20261111').replace('lython-b564-bound-borrow-20261110','lython-b565-json-string-20261111')
    s=s.replace('len(jobs)==2','len(jobs)==3').replace("len(raw['jobs'])==2","len(raw['jobs'])==3").replace('jobs=2','jobs=3').replace("['jobs']==2","['jobs']==3").replace('B564 two-job','B565 three-job').replace("['elapsedSeconds']<120","['elapsedSeconds']<150")
    if name=='collect.py':
        lines=s.splitlines();i=next(i for i,line in enumerate(lines)if line.startswith('jobs='));lines[i]='jobs='+repr([[name,'warm']for name in ids]);s='\n'.join(lines)+'\n'
        s=s.replace('time.monotonic()-started < 80','time.monotonic()-started < 110')
        a=s.index("purpose='");b=s.index("',\n",a)
        s=s[:a]+"purpose='R217 complete JSON string decoder removes backend parse/unescape, rewrite/remapping and final-copy chain. Plain report, same report with default escaped Unicode input, and ordinary text control. No allocation or CPU forecast; desktop adverse observations preserved"+s[b:]
    if name.endswith('.py'):ast.parse(s)
    write(out/name,s)
write(out/'contract.md','''# R217 complete JSON string decoder — quiet confirmation

Three existing complete file-backed workloads: plain report, the identical report
with Python-default escaped Unicode input, and unchanged ordinary text control.
Fresh b8b05ccc/qualified696 reference versus JSON-only candidate; four independent
source roles,5s warmup,7rotated400msrounds,normalGC/tiering. Warm sync only; desktop
async/compile/allocation and every adverse result remain separate. No fixed gain
threshold; decide retain/refine/withdraw from simplification, semantics and debt.
150s owner/30stop/30margin. Full mediated RAM I/O, per-invocation output validation
and prior-result cleanup included; compile/reset/separate verification/protocol/
startup/final cached teardown excluded. No CPython or embedding-tail claim.
''')

def committed(path):
    relative=path.relative_to(repo).as_posix();blob=path.read_bytes()
    assert blob==subprocess.check_output(['git','show','HEAD:'+relative],cwd=repo)
    return blob,relative
quiet=repo/'benchmarks/results/2026-11-10-bound-native-arguments/quiet/observations.tgz'
profile=repo/'benchmarks/results/2026-11-10-bound-native-arguments/profile/observations.tgz'
blob,canonical=committed(quiet)
with tarfile.open(fileobj=io.BytesIO(blob))as archive:
    member='current-core.dll';core=archive.extractfile(member).read()
retire=[dict(knownPath='bound-borrow-b564-20261110/candidate/Lokad.Lython.dll',canonical=canonical,member=member,bytes=len(core),sha256=hashlib.sha256(core).hexdigest())]
assert retire[0]['sha256']=='2f9b100e5411f9e0580a65160088b45117593965e3586d0f1dee3ec5492c6561'
exact=[dict(path='bound-borrow-b564-20261110/observations.tgz',canonical=canonical,bytes=len(blob),sha256=hashlib.sha256(blob).hexdigest())]
blob,canonical=committed(profile)
exact.append(dict(path='bound-calls-b563-20261110/observations.tgz',canonical=canonical,bytes=len(blob),sha256=hashlib.sha256(blob).hexdigest()))
with tarfile.open(fileobj=io.BytesIO(blob))as archive:
    for name in ['perf.map','perf-stacks.txt','perf-leaves.txt','perf.data']:
        member='profiles/class-report-warm/'+name;data=archive.extractfile(member).read()
        exact.append(dict(path='bound-calls-b563-20261110/'+member,canonical=canonical,member=member,bytes=len(data),sha256=hashlib.sha256(data).hexdigest()))
write(out/'superseded-cores.json',json.dumps(retire,indent=2)+'\n')
write(out/'exact-copies.json',json.dumps(exact,indent=2)+'\n')
with tarfile.open(out/'payload.tgz','w:gz')as archive:
    for name in ['representative.json','identity.json','collect.py','archive-owned.py','run.sh','contract.md']:archive.add(out/name,arcname=name)
    archive.add(before,arcname='baseline-core.dll');archive.add(candidate,arcname='current-core.dll')
with tarfile.open(out/'payload.tgz')as archive:footprint=sum(((m.size+4095)//4096)*4096 for m in archive.getmembers()if m.isfile())+128*1024
size=(out/'payload.tgz').stat().st_size
write(out/'footprint.json',json.dumps(dict(payloadBytes=size,footprintBytes=footprint,requiredFreeBytes=(240+12)*1024*1024+max(footprint,3*size)),indent=2)+'\n')
s=(old/'launch.py').read_text(encoding='utf-8').replace('bound-borrow-b564-20261110','json-string-b565-20261111').replace('lython-b564-bound-borrow-20261110','lython-b565-json-string-20261111').replace('bound-borrow-b564-launch.py','json-string-b565-launch.py').replace('lython-b564-fixtures','lython-b565-fixtures')
s=s.replace("exact=json.loads((r/'exact-copy.json').read_text(encoding='utf-8'))","exactCopies=json.loads((r/'exact-copies.json').read_text(encoding='utf-8'))")
s=s.replace("[('current-core.dll','candidate')]","[('baseline-core.dll','reference'),('current-core.dll','candidate')]")
s=s.replace("assert hashlib.sha256((source/'Lokad.Lython.dll').read_bytes()).hexdigest()==config['expectedCores']['reference']","assert hashlib.sha256((source/'Lokad.Lython.dll').read_bytes()).hexdigest()=='0e2bc55bac5ebfafd9d30014e34e2afb3e9776bbe6b5d83a53cc710c0d99c492'")
a=s.index("  exact=config['exact'];");b=s.index("  assert before+reclaim",a)
s=s[:a]+'''  for exact in config['exactCopies']:
   path=base/exact['path'];st=path.lstat()
   assert path.resolve(strict=True).is_relative_to(base)and stat.S_ISREG(st.st_mode)and st.st_uid==os.getuid()and st.st_nlink==1
   assert st.st_size==exact['bytes']and hashlib.sha256(path.read_bytes()).hexdigest()==exact['sha256']
   reclaim+=st.st_blocks*512;retired.append(dict(exact,paths=[exact['path']],allocatedBytes=st.st_blocks*512,allHardlinkAliasesVerified=True))
'''+s[b:]
s=s.replace(" with tarfile.open(fileobj=io.BytesIO(payload),mode='r:gz')as a:root.mkdir();a.extractall(root,filter='data')"," beforeExtractionFreeBytes=shutil.disk_usage(base).free\n with tarfile.open(fileobj=io.BytesIO(payload),mode='r:gz')as a:root.mkdir();a.extractall(root,filter='data')")
s=s.replace('afterRecoveryFreeBytes=shutil.disk_usage(base).free','afterRecoveryFreeBytes=beforeExtractionFreeBytes,postExtractionFreeBytes=shutil.disk_usage(base).free')
s=s.replace(" os.link(source/'Lokad.Lython.dll',root/'baseline-core.dll')\n",'')
s=s.replace('jobs=2,ownerSeconds=120','jobs=3,ownerSeconds=150').replace('RuntimeMaxSec=120','RuntimeMaxSec=150').replace('ownerSeconds=120','ownerSeconds=150').replace('seconds=180','seconds=210')
ast.parse(s);write(out/'launch.py',s)
print('Prepared bounded three-job confirmation; no VM mutation. Required bytes',json.loads((out/'footprint.json').read_text())['requiredFreeBytes'],flush=True)
