from pathlib import Path
import hashlib,json,os,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;previous=r.parent.parent/'conditional-helper-r266/normalized';before=previous/'candidate';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
baseline=json.loads((r.parent/'baseline.json').read_text(encoding='utf-8'));assert sha(before/'Lokad.Lython.dll')==baseline['coreSHA256']
source={p.as_posix():sha(p)for p in Path('src/Lokad.Lython').rglob('*.cs')if not any(v in ['obj','bin']for v in p.parts)}
old=baseline['sourceHashes'];removed=sorted(set(old)-set(source));added=sorted(set(source)-set(old));changed=sorted(n for n in set(old)&set(source)if old[n]!=source[n])
assert len(removed)==3 and len(added)==4,(removed,added)
checks=json.loads((r/'focused-checks.json').read_text(encoding='utf-8'));assert len(checks)==4 and all(v['failed']=='0'for v in checks.values());assert sum(int(v['passed'])for v in checks.values())==1366
image=r/'candidate';image.mkdir()
for p in before.iterdir():
 if p.is_file():
  if p.name=='Lokad.Lython.dll':shutil.copyfile('src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',image/p.name)
  else:os.link(p,image/p.name)
shutil.copyfile('src/Lokad.Lython/bin/Release/net10.0/Lokad.Lython.dll',r/'candidate-net10.dll')
with tarfile.open(r/'sources-candidate.tar.xz','w:xz')as t:
 for n in source:t.add(n,arcname=n)
(r/'diagnostic-identity.json').write_text(json.dumps(dict(beforeRevision=baseline['productionRevision'],sourceHashes=source,changedProductionFiles=changed,removedProductionFiles=removed,addedProductionFiles=added,beforeCoreSHA256=sha(before/'Lokad.Lython.dll'),candidateCoreSHA256=sha(image/'Lokad.Lython.dll'),candidateNet10SHA256=sha(r/'candidate-net10.dll'),focusedChecks=checks,
 hypothesis='Refined one existing instruction prefix, shared owned invocation preparation for callbacks/opcodes, plus concrete managed span borrow; bounded inline helper slots export live state once at actual context/protocol boundary. Remove compiler native body tree, recursive Guard/Eval/shape transport, obsolete native binding hooks; preserve actual builtin kernels/cased split/closure cells. No generic accessor framework, arena/window offsets, unsafe code, tracing, policies or new allowance. Firstformcallback allocation+40% and original desktop adversity preserved; callbacks now borrow existing bound workspace until actual context requirement. Small managed inline slots retain existing managed stack protection; heap caches/results/export overlap use existing allowance.',
 qualification='Uncommitted coherent prototype;1366focused checks actual11/10 pass including partial formatting then actual host IO success/cancellation, retained result/cleanup baseline, protocols, defaults, closures, generators, refusal and stack. No full delivery or CPU claim.'),indent=2)+'\n',encoding='utf-8')
tool=r/'tool';tool.mkdir();shutil.copyfile(previous/'tool/complete-jobs.csproj',tool/'complete-jobs.csproj');program=(previous/'tool/Program.cs').read_text(encoding='utf-8').replace('cases.Length != 5','cases.Length != 6').replace('R266 compiler-only negated branch normalization; five complete scripts','R268 shared ordered prefix and callback preparation; six complete scripts');(tool/'Program.cs').write_text(program,encoding='utf-8')
catalog=json.loads((previous/'catalog.json').read_text(encoding='utf-8'));catalog['cases']+=json.loads((r.parent/'callback-control/catalog.json').read_text(encoding='utf-8'))['cases'];(r/'catalog.json').write_text(json.dumps(catalog,indent=2)+'\n',encoding='utf-8');shutil.copyfile(previous/'summarize.py',r/'summarize.py')
env=os.environ.copy();env['DOTNET_ROOT']='C:/Users/JoannesVermorel/.dotnet11';env['PATH']=env['DOTNET_ROOT']+';C:/Python313;'+env['PATH'];dotnet=env['DOTNET_ROOT']+'/dotnet.exe'
with(r/'build-driver.log').open('wb')as f:p=subprocess.run([dotnet,'build',str(tool/'complete-jobs.csproj'),'-c','Release','--nologo','--tl:off'],env=env,stdout=f,stderr=subprocess.STDOUT)
assert p.returncode==0
for phase in ['warm-sync','warm-async','compile']:
 d=r/phase;d.mkdir();shutil.copyfile(previous/'warm-sync/hash-witness.json',d/'hash-witness.json')
 cmd=[dotnet,str(tool/'bin/Release/net11.0/complete-jobs.dll'),str(before/'Lokad.Lython.Benchmarks.dll'),str(image/'Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(d/'hash-witness.json'),'forward','compile'if phase=='compile'else'warm','async'if phase=='warm-async'else'sync']
 with(d/'measurements.json').open('wb')as f,(d/'stderr.log').open('wb')as err:p=subprocess.run(cmd,env=env,stdout=f,stderr=err)
 assert p.returncode==0,(phase,(d/'stderr.log').read_text(encoding='utf-8'))
 j=json.loads((d/'measurements.json').read_text(encoding='utf-8-sig'));assert len(j['observations'])==6 and j['allGoldensPassed']and j['allBalancesZero']and j['ownedFixturesRemoved'];print(phase,'six whole scripts outputs/allowance/cleanup pass',flush=True)
subprocess.run(['python','-X','utf8',str(r/'summarize.py')],check=True)
