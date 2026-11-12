from pathlib import Path
import hashlib,json,os,shutil,subprocess,tarfile,xml.etree.ElementTree as ET
r=Path('.git/agent-notes/review-20261031/cell-write-authority-r260').resolve();old=r.parent/'completed-call-lifecycle-r259';before=old/'candidate';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(before/'Lokad.Lython.dll')=='0e715d19c6f99ecbee0c01c4dee3e4e86ebfe87ae8ca26687baa1b11bc7f39e0'
source=json.loads((r/'focused-source-identity.json').read_text(encoding='utf-8'))
for n,h in source.items():assert sha(Path(n))==h,n
prior=json.loads((old/'diagnostic-identity.json').read_text(encoding='utf-8'))['sourceHashes'];changed=[n for n,h in source.items()if h!=prior[n]];assert len(changed)==5,changed
checks={}
for label in ['final-internal11','final-public11','final-internal10','final-public10']:
 c=next(x for x in ET.parse(r/'tests'/(label+'.trx')).iter()if x.tag.endswith('Counters'));assert c.attrib['failed']=='0'and c.attrib['notExecuted']=='0';checks[label]=c.attrib
image=r/'candidate';shutil.copytree(before,image,copy_function=os.link);(image/'Lokad.Lython.dll').unlink();shutil.copyfile('src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',image/'Lokad.Lython.dll');shutil.copyfile('src/Lokad.Lython/bin/Release/net10.0/Lokad.Lython.dll',r/'candidate-net10.dll')
(r/'diagnostic-identity.json').write_text(json.dumps(dict(beforeRevision='7114021edb1e850380e885eba32953042cc211fb',baselineQualification='43148511620terminalGREENcached Windows/Ubuntu actual11/10originalstress/installedconsumers',sourceHashes=source,changedProductionFiles=changed,beforeCoreSHA256=sha(before/'Lokad.Lython.dll'),candidateCoreSHA256=sha(image/'Lokad.Lython.dll'),candidateNet10SHA256=sha(r/'candidate-net10.dll'),focusedChecks=checks,correctness='Two independentlyconfirmed baselinebugs: nonlocal comprehension shadow corruptsclosurecell, loweredclassupdates/deletes mirroredgeneratorslot notvisibleonresume. ThreeCPythonprobes matchcorrectedsource; testsbothAPIs checkoutput/budgetcleanup.',sourceSimplification='One cellValue setter synchronizes active ownedslot with cachedindex and existing conditionalnamespacepolicy; deleteStoreThroughOwner andduplicate localwrites. Preserveactiveguard/resume refresh; mirroredresume usesexistingnamespacesyncwithcooperativechecks.',measurement='Threecompletecaptured/ordinarytext andreportcontrol, bothAPIs,64warmups/5rotated rounds8jobs. Physicalallocation andgoldens/balances; desktopelapsed diagnostic only, compilationnotchanged.'),indent=2)+'\n',encoding='utf-8')
with tarfile.open(r/'sources-candidate.tar.xz','w:xz')as a:
 for n in source:a.add(n,arcname=n)
catalog=json.loads((old/'catalog.json').read_text(encoding='utf-8'));cases={'scripts.text-summary-helpers.nonlocal-counter.large','scripts.text-summary-helpers.large','scripts.report-render.large'};catalog['cases']=[c for c in catalog['cases']if c['id']in cases];assert len(catalog['cases'])==3;(r/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
tool=r/'tool';tool.mkdir();shutil.copyfile(old/'tool/complete-jobs.csproj',tool/'complete-jobs.csproj');s=(old/'tool/Program.cs').read_text(encoding='utf-8').replace('cases.Length != 7','cases.Length != 3').replace('Require six unchanged controls and the guarded helper script.','Require two complete text scripts and report control.').replace('R259 direct completed-call lifecycle','R260 cell-write authority and lexical/generator fixes');(tool/'Program.cs').write_text(s,encoding='utf-8')
dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe');env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
with(r/'build-driver.log').open('wb')as f:p=subprocess.run([str(dotnet),'build',str(tool/'complete-jobs.csproj'),'-c','Release','--nologo','--tl:off'],env=env,stdout=f,stderr=subprocess.STDOUT)
assert p.returncode==0
for phase in ['warm-sync','warm-async']:
 folder=r/phase;folder.mkdir();shutil.copyfile(old/phase/'hash-witness.json',folder/'hash-witness.json')
 cmd=[str(dotnet),str(tool/'bin/Release/net11.0/complete-jobs.dll'),str(before/'Lokad.Lython.Benchmarks.dll'),str(image/'Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(folder/'hash-witness.json'),'forward','warm','async'if phase=='warm-async'else'sync']
 with(folder/'measurements.json').open('wb')as f,(folder/'stderr.log').open('wb')as err:p=subprocess.run(cmd,env=env,stdout=f,stderr=err)
 assert p.returncode==0,(phase,(folder/'stderr.log').read_text(encoding='utf-8'))
 j=json.loads((folder/'measurements.json').read_text(encoding='utf-8-sig'));assert len(j['observations'])==3 and j['allGoldensPassed']and j['allBalancesZero']and j['ownedFixturesRemoved'];print(phase,'three complete scripts fullgoldens/files/balances/cleanup pass.',flush=True)
shutil.copyfile(old/'summarize.py',r/'summarize.py')
