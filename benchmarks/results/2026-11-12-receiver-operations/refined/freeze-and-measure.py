from pathlib import Path
import hashlib,json,os,shutil,subprocess,tarfile,xml.etree.ElementTree as ET
r=Path('.git/agent-notes/review-20261031/receiver-operations-r256').resolve(); s=r/'refined'
assert not(s/'diagnostic-identity.json').exists()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
source=json.loads((s/'focused-source-identity.json').read_text(encoding='utf-8'))
for n,h in source.items():assert sha(Path(n))==h,n
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
changed=[n for n,h in source.items()if h!=meta['sourceHashes'][n]]
assert changed==['src/Lokad.Lython/Runtime/LythonRuntime.Executable.NativeCalls.cs'],changed
shutil.copytree(r/'images/candidate',s/'image')
shutil.copyfile('src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',s/'image/Lokad.Lython.dll')
shutil.copyfile('src/Lokad.Lython/bin/Release/net10.0/Lokad.Lython.dll',s/'candidate-net10.dll')
meta['sourceHashes']=source;meta['refinement']='One operation switch in the shape fact, replacing two; all first-form adversity/source retained.'
meta['images']['candidate']={p.name:sha(p)for p in(s/'image').iterdir()if p.is_file()};meta['net10SHA256']=sha(s/'candidate-net10.dll')
for label in meta['focusedChecks']:
 c=next(x for x in ET.parse(s/'tests'/(label+'.trx')).iter()if x.tag.endswith('Counters'))
 assert c.attrib['failed']=='0'and c.attrib['notExecuted']=='0';meta['focusedChecks'][label]=c.attrib
(s/'diagnostic-identity.json').write_text(json.dumps(meta,indent=2)+'\n',encoding='utf-8')
with tarfile.open(s/'sources-candidate.tar.xz','w:xz')as a:
 for n,h in source.items():a.add(n,arcname=n)
dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe');env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
for phase in ['warm-sync','warm-async','compile']:
 folder=s/phase;folder.mkdir()
 cmd=[str(dotnet),str(r/'tool/bin/Release/net11.0/complete-jobs.dll'),str(r/'images/before/Lokad.Lython.Benchmarks.dll'),str(s/'image/Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(folder/'hash-witness.json'),'forward','compile'if phase=='compile'else'warm','async'if phase=='warm-async'else'sync']
 with(folder/'measurements.json').open('wb')as f,(folder/'stderr.log').open('wb')as err:p=subprocess.run(cmd,env=env,stdout=f,stderr=err)
 assert p.returncode==0,(phase,(folder/'stderr.log').read_text(encoding='utf-8'))
 j=json.loads((folder/'measurements.json').read_text(encoding='utf-8-sig'));assert len(j['observations'])==7 and j['allGoldensPassed']and j['allBalancesZero']and j['ownedFixturesRemoved']
 print(phase,'seven full-script goldens/files/balances/cleanup pass.',flush=True)
