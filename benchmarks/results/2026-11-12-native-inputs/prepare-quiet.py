from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b597';q.mkdir();old=r.parent/'word-materialization-r250/b595'
sha=lambda b:hashlib.sha256(b).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n).read_bytes())==h,n
name='native-inputs-b597-20261112';unit='lython-b597-native-inputs-20261112'
identity=json.loads((old/'identity.json').read_text(encoding='utf-8'))
identity.update(sourceRevision='R250/R251 coherent native storage/input candidate against qualified8f8628a4. Private UTF8 duplicate-word probing retained; captured helper packing/clearing and appended-cell index convention removed. Readonly stack input view borrows actual parameter prefix and definition-owned cells by reference. Ordinary object call/return ABI, actual binder, owners and tokens unchanged. 1642internal/3810public per actual11/10 pass. Complete six-script source pair both APIs/separatecompile passes goldens/balances/cleanup; text allocation20–22%lower, compileflat. R250B595asyncSearch/CSV adversity retained; B596pairedprofile does not settle it. No current-source normal-profile CPU claim before this bounded group.',coreSHA256={k:meta['images']['before'if k.startswith('reference')else'candidate']['Lokad.Lython.dll']for k in ['reference','reference-control','candidate','candidate-control']},catalogSHA256=sha((r/'catalog.json').read_bytes()))
(q/'identity.json').write_text(json.dumps(identity,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
(q/'source-identity.json').write_text(json.dumps(dict(beforeRevision='8f8628a4',baselineQualification='458be840/43087005596Windows/Ubuntu actual11/10stress/packageGREENcached',sourceSHA256=meta['sourceHashes'],changedProductionFiles=meta['changedProductionFiles'],candidateQualification='10904focused checks actual11/10; six complete unchanged scripts through both APIs and separate compilation. No full prototype delivery.',prototypeUncommitted=True),indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'catalog.json',q/'representative.json');shutil.copyfile(r/'images/candidate/Lokad.Lython.dll',q/'current-core.dll')
for filename in ['collect.py','archive-owned.py','run.sh','launch.py','retrieve.py','summarize.py','remote-launch.py']:
 s=(old/filename).read_text(encoding='utf-8').replace('word-materialization-b595-20261111',name).replace('lython-b595-word-materialization-20261111',unit).replace('word-materialization-b595-launch.py','native-inputs-b597-launch.py').replace('lython-b595-fixtures','lython-b597-fixtures').replace('B595','B597')
 if filename=='collect.py':
  a=s.index("purpose='");b=s.index("',\n                  warmupSeconds",a)
  s=s[:a]+"purpose='Coherent R250/R251 native storage/input source pair against exact qualified8f862. Both complete text scripts through both APIs; search/CSV both APIs, JSON Run and reportAsync controls. B595normal adversity and B596instrumented uncertainty retained. Fixed common worker/adapter, mediated RAM I/O/checks/prior-result cleanup included. No CPython comparison or isolated R251 attribution"+s[b:]
 (q/filename).write_text(s,encoding='utf-8',newline='\n')
 if filename.endswith('.py')and filename!='remote-launch.py':ast.parse(s)
with tarfile.open(q/'payload.tgz','w:gz')as a:
 for n in ['current-core.dll','representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh']:a.add(q/n,arcname=n)
with tarfile.open(q/'payload.tgz')as a:expanded=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())
def canonical(name):
 p=Path(name);b=p.read_bytes();assert b==subprocess.check_output(['git','show','HEAD:'+p.as_posix()]);return p,b
retire=[]
p,b=canonical('benchmarks/results/2026-11-11-native-word-probing/quiet/observations.tgz')
with tarfile.open(p)as a:core=a.extractfile('current-core.dll').read()
assert sha(core)!=identity['coreSHA256']['candidate']and sha(core)!=identity['coreSHA256']['reference']
retire.append(dict(kind='all-aliases',knownPath='word-control-profile-b596-20261111/candidate/Lokad.Lython.dll',bytes=len(core),sha256=sha(core),canonicalStorage=p.as_posix(),canonicalArchiveSHA256=sha(b),canonicalEntry='current-core.dll',reason='Superseded R250 prototype image; both B595/B596 terminal/cached. Exact source/core/all adverse results committed364d9852/31eb7485. Fresh idle/path/UID/inode/all-alias proof. Current selected reference and new R250/R251 image protected; optional adapter/worker unchanged.'))
for remote,path in [('word-control-profile-b596-20261111/observations.tgz','benchmarks/results/2026-11-11-native-word-probing/control-profile/observations.tgz'),('word-control-profile-b596-20261111/catalog.json','benchmarks/results/2026-11-11-native-word-probing/control-profile/catalog.json')]:
 p,b=canonical(path);retire.append(dict(kind='exact',knownPath=remote,bytes=len(b),sha256=sha(b),canonicalStorage=p.as_posix(),reason='Unused terminal duplicate; exact committed Git bytes verified. Source/core/maps/adverse evidence retained; new collection supplies its own catalog. Fresh path/UID/inode/all-alias proof.'))
config=dict(root='/home/lokad/lython-bench-checks/'+name,unit=unit,payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha((q/'payload.tgz').read_bytes()),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
assert "len(raw['jobs'])==10"in(q/'archive-owned.py').read_text(encoding='utf-8')
assert 'native-inputs-b597-launch.py'in(q/'launch.py').read_text(encoding='utf-8')
assert 'RuntimeMaxSec=330'in(q/'remote-launch.py').read_text(encoding='utf-8')
print('Existing ten short jobs prepared,330sowner/390sobservation bound. Combined source pair, no isolated attribution. No VM mutation; fresh inventory required. Superseded prototype and duplicate proofs reference exact committed Git bytes.')
