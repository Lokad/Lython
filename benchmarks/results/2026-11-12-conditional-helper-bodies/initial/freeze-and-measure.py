from pathlib import Path
import hashlib,json,os,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;previous=r.parent/'text-output-batches-r263/refined';before=previous/'candidate'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
old=json.loads((previous/'diagnostic-identity.json').read_text(encoding='utf-8'))['sourceHashes'];source={n:sha(Path(n))for n in old};changed=[n for n in old if old[n]!=source[n]];assert len(changed)==3,changed
checks=json.loads((r/'focused-checks.json').read_text(encoding='utf-8'));assert len(checks)==4 and all(v['failed']=='0'for v in checks.values())
image=r/'candidate';image.mkdir()
for p in before.iterdir():
 if p.is_file():
  if p.name=='Lokad.Lython.dll':shutil.copyfile('src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',image/p.name)
  else:os.link(p,image/p.name)
shutil.copyfile('src/Lokad.Lython/bin/Release/net10.0/Lokad.Lython.dll',r/'candidate-net10.dll')
with tarfile.open(r/'sources-candidate.tar.xz','w:xz')as t:
 for n in source:t.add(n,arcname=n)
(r/'diagnostic-identity.json').write_text(json.dumps(dict(beforeRevision='983bab966f13d4575b514eb20dd237a12159bc65 productionunchanged796c3218',sourceHashes=source,changedProductionFiles=changed,beforeCoreSHA256=sha(before/'Lokad.Lython.dll'),candidateCoreSHA256=sha(image/'Lokad.Lython.dll'),candidateNet10SHA256=sha(r/'candidate-net10.dll'),focusedChecks=checks,
 hypothesis='Eligible whole unary/conditional helpers omit ordinarybinding/work/dispatch viaexisting nativeplan/shared operator evaluator. No newdispatcher/owner/options. Currenteffectbarrier protects laterglobalreads acrossadmissionreentry;2independentCPgoldens andactualpostadmissioncustomhostI/O pass. NoCPUforecast or publiccontractchangeintended.',
 qualification='994focusedchecksactual11/10pass, nonincrementalRelease solution/probebuild0warnings/errors. Uncommittedprototype; qualifiedoutputcheckpoint796c remainsseparate. No fullnewsource qualification claim.'),indent=2)+'\n',encoding='utf-8')
tool=r/'tool';tool.mkdir();shutil.copyfile(previous/'tool/complete-jobs.csproj',tool/'complete-jobs.csproj')
t=(previous/'tool/Program.cs').read_text(encoding='utf-8').replace('cases.Length != 4','cases.Length != 5').replace('Require complete report UTF8/UTF16, CSV and text controls.','Require complete guarded/native search, CSV, captured text and report.').replace('R262/R263 shared final encodings and bounded text output batches; seven complete scripts','R266 complete unary conditional helper bodies; five complete scripts')
(tool/'Program.cs').write_text(t,encoding='utf-8')
shutil.copyfile(r.parent/'call-boundary-r264/catalog.json',r/'catalog.json')
shutil.copyfile(previous/'summarize.py',r/'summarize.py')
env=os.environ.copy();env['DOTNET_ROOT']='C:/Users/JoannesVermorel/.dotnet11';env['PATH']=env['DOTNET_ROOT']+';C:/Python313;'+env['PATH'];dotnet=env['DOTNET_ROOT']+'/dotnet.exe'
with(r/'build-driver.log').open('wb')as f:p=subprocess.run([dotnet,'build',str(tool/'complete-jobs.csproj'),'-c','Release','--nologo','--tl:off'],env=env,stdout=f,stderr=subprocess.STDOUT)
assert p.returncode==0
for phase in ['warm-sync','warm-async','compile']:
 d=r/phase;d.mkdir();shutil.copyfile(previous/'warm-sync/hash-witness.json',d/'hash-witness.json')
 cmd=[dotnet,str(tool/'bin/Release/net11.0/complete-jobs.dll'),str(before/'Lokad.Lython.Benchmarks.dll'),str(image/'Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(d/'hash-witness.json'),'forward','compile'if phase=='compile'else'warm','async'if phase=='warm-async'else'sync']
 with(d/'measurements.json').open('wb')as f,(d/'stderr.log').open('wb')as err:p=subprocess.run(cmd,env=env,stdout=f,stderr=err)
 assert p.returncode==0,(phase,(d/'stderr.log').read_text(encoding='utf-8'))
 j=json.loads((d/'measurements.json').read_text(encoding='utf-8-sig'));assert len(j['observations'])==5 and j['allGoldensPassed']and j['allBalancesZero']and j['ownedFixturesRemoved'];print(phase,'fivecomplete scriptsfullgoldens/balances/cleanup pass',flush=True)
subprocess.run(['python','-X','utf8',str(r/'summarize.py')],check=True)
