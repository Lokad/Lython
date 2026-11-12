from pathlib import Path
import hashlib,json,os,shutil,subprocess,tarfile
r=Path('.git/agent-notes/review-20261031/native-counting-r257').resolve();old=r.parent/'receiver-operations-r256';before=old/'refined/image'
assert not(r/'diagnostic-identity.json').exists()
source={p.as_posix():hashlib.sha256(p.read_bytes()).hexdigest()for p in Path('src/Lokad.Lython').rglob('*.cs')if not {'bin','obj'}&set(p.parts)}
prior=json.loads((old/'refined/diagnostic-identity.json').read_text(encoding='utf-8'))['sourceHashes'];assert sorted(n for n,h in source.items()if h!=prior[n])==['src/Lokad.Lython/Runtime/LythonRuntime.Executable.DictionaryAccumulation.cs','src/Lokad.Lython/Runtime/PyDict.Accumulation.cs']
image=r/'candidate';shutil.copytree(before,image,copy_function=os.link);(image/'Lokad.Lython.dll').unlink();shutil.copyfile('src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',image/'Lokad.Lython.dll')
(r/'diagnostic-identity.json').write_text(json.dumps(dict(beforeRevision='82ed92d422a947726fdc802108cd0ffebbaa5472',sourceHashes=source,beforeCore=hashlib.sha256((before/'Lokad.Lython.dll').read_bytes()).hexdigest(),candidateCore=hashlib.sha256((image/'Lokad.Lython.dll').read_bytes()).hexdigest(),limits='Three complete scripts,64warmups/5rotated rounds8jobs. Physical allocation and output/balance proof; desktop timing diagnostic only. Compiler and external baseline unchanged.'),indent=2)+'\n',encoding='utf-8')
with tarfile.open(r/'sources-candidate.tar.xz','w:xz')as a:
 for n in source:a.add(n,arcname=n)
catalog=json.loads((old/'catalog.json').read_text(encoding='utf-8'));chosen={'scripts.text-summary-helpers.nonlocal-counter.large','scripts.text-summary-helpers.large','scripts.report-render.large'};catalog['cases']=[c for c in catalog['cases']if c['id']in chosen];assert len(catalog['cases'])==3;(r/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
tool=r/'tool';tool.mkdir();shutil.copyfile(old/'tool/complete-jobs.csproj',tool/'complete-jobs.csproj');t=(old/'tool/Program.cs').read_text(encoding='utf-8').replace('cases.Length != 7','cases.Length != 3').replace('Require six unchanged controls and the guarded helper script.','Require two complete text scripts and report control.').replace('var warmups = 256;','var warmups = 64;').replace('var batch = 16;','var batch = 8;').replace('R256 shared canonical receiver/truth services','R257 primitive native increment transport')
(tool/'Program.cs').write_text(t,encoding='utf-8')
dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe');env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
with(r/'build-driver.log').open('wb')as f:p=subprocess.run([str(dotnet),'build',str(tool/'complete-jobs.csproj'),'-c','Release','--nologo','--tl:off'],env=env,stdout=f,stderr=subprocess.STDOUT)
assert p.returncode==0
for phase in ['warm-sync','warm-async']:
 folder=r/phase;folder.mkdir();shutil.copyfile(old/phase/'hash-witness.json',folder/'hash-witness.json')
 cmd=[str(dotnet),str(tool/'bin/Release/net11.0/complete-jobs.dll'),str(before/'Lokad.Lython.Benchmarks.dll'),str(image/'Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(folder/'hash-witness.json'),'forward','warm','async'if phase=='warm-async'else'sync']
 with(folder/'measurements.json').open('wb')as f,(folder/'stderr.log').open('wb')as err:p=subprocess.run(cmd,env=env,stdout=f,stderr=err)
 assert p.returncode==0,(phase,(folder/'stderr.log').read_text(encoding='utf-8'))
 j=json.loads((folder/'measurements.json').read_text(encoding='utf-8-sig'));assert len(j['observations'])==3 and j['allGoldensPassed']and j['allBalancesZero']and j['ownedFixturesRemoved'];print(phase,'three complete scripts pass full goldens,balances,cleanup.',flush=True)
shutil.copyfile(old/'summarize.py',r/'summarize.py')
