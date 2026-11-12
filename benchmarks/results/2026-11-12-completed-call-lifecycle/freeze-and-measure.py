from pathlib import Path
import hashlib,json,os,shutil,subprocess,tarfile,xml.etree.ElementTree as ET
r=Path('.git/agent-notes/review-20261031/completed-call-lifecycle-r259').resolve();prior=r.parent/'receiver-operations-r256';before=prior/'refined/image'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();source=json.loads((r/'final-source-identity.json').read_text(encoding='utf-8'))
for n,h in source.items():assert sha(Path(n))==h,n
old=json.loads((prior/'refined/diagnostic-identity.json').read_text(encoding='utf-8'))['sourceHashes'];changed=[n for n,h in source.items()if h!=old[n]];assert len(changed)==5,changed
checks={}
for label in ['final-internal11','final-public11','final-internal10','final-public10']:
 c=next(x for x in ET.parse(r/'tests'/(label+'.trx')).iter()if x.tag.endswith('Counters'));assert c.attrib['failed']=='0'and c.attrib['notExecuted']=='0';checks[label]=c.attrib
image=r/'candidate';shutil.copytree(before,image,copy_function=os.link);(image/'Lokad.Lython.dll').unlink();shutil.copyfile('src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',image/'Lokad.Lython.dll');shutil.copyfile('src/Lokad.Lython/bin/Release/net10.0/Lokad.Lython.dll',r/'candidate-net10.dll')
meta=dict(beforeRevision='82ed92d422a947726fdc802108cd0ffebbaa5472',qualification='43139754244terminalGREEN cached Windows/Ubuntu actual11/10originalstress/installedconsumers',beforeCoreSHA256=sha(before/'Lokad.Lython.dll'),candidateCoreSHA256=sha(image/'Lokad.Lython.dll'),candidateNet10SHA256=sha(r/'candidate-net10.dll'),sourceHashes=source,changedProductionFiles=changed,focusedChecks=checks,measurement='Existing seven complete scripts bothAPIs and separate compile.64warmups/5rotated rounds8jobs; sharedALCs/CoreLib/GC, sameworkerfec79/adapter0.4, physicalallocation and goldens. Desktopelapsed diagnostic only.')
(r/'diagnostic-identity.json').write_text(json.dumps(meta,indent=2)+'\n',encoding='utf-8')
with tarfile.open(r/'sources-candidate.tar.xz','w:xz')as a:
 for n,h in source.items():a.add(n,arcname=n)
shutil.copyfile(prior/'catalog.json',r/'catalog.json');tool=r/'tool';tool.mkdir();shutil.copyfile(prior/'tool/complete-jobs.csproj',tool/'complete-jobs.csproj');s=(prior/'tool/Program.cs').read_text(encoding='utf-8').replace('var warmups = 256;','var warmups = 64;').replace('var batch = 16;','var batch = 8;').replace('R256 shared canonical receiver/truth services','R259 direct completed-call lifecycle')
(tool/'Program.cs').write_text(s,encoding='utf-8')
dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe');env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
with(r/'build-driver.log').open('wb')as f:p=subprocess.run([str(dotnet),'build',str(tool/'complete-jobs.csproj'),'-c','Release','--nologo','--tl:off'],env=env,stdout=f,stderr=subprocess.STDOUT)
assert p.returncode==0
for phase in ['warm-sync','warm-async','compile']:
 folder=r/phase;folder.mkdir();shutil.copyfile(prior/phase/'hash-witness.json',folder/'hash-witness.json')
 cmd=[str(dotnet),str(tool/'bin/Release/net11.0/complete-jobs.dll'),str(before/'Lokad.Lython.Benchmarks.dll'),str(image/'Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(folder/'hash-witness.json'),'forward','compile'if phase=='compile'else'warm','async'if phase=='warm-async'else'sync']
 with(folder/'measurements.json').open('wb')as f,(folder/'stderr.log').open('wb')as err:p=subprocess.run(cmd,env=env,stdout=f,stderr=err)
 assert p.returncode==0,(phase,(folder/'stderr.log').read_text(encoding='utf-8'))
 j=json.loads((folder/'measurements.json').read_text(encoding='utf-8-sig'));assert len(j['observations'])==7 and j['allGoldensPassed']and j['allBalancesZero']and j['ownedFixturesRemoved'];print(phase,'seven complete scripts goldens/files/balances/cleanup pass.',flush=True)
shutil.copyfile(prior/'summarize.py',r/'summarize.py')
