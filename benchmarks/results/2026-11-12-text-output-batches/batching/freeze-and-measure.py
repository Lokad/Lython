from pathlib import Path
import hashlib,json,os,shutil,subprocess,tarfile
r=Path(__file__).parent.resolve();before=r.parent/'cell-write-authority-r260/candidate';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();source=json.loads((r/'focused-source-identity.json').read_text(encoding='utf-8'))
for n,h in source.items():assert sha(Path(n))==h,n
checks=json.loads((r/'focused-checks.json').read_text(encoding='utf-8'));assert sum(int(c['passed'])for c in checks.values())==4574
old=json.loads((r.parent/'cell-write-authority-r260/diagnostic-identity.json').read_text(encoding='utf-8'))['sourceHashes'];changed=[n for n,h in source.items()if old[n]!=h];assert len(changed)==10,changed
image=r/'candidate';assert not image.exists();shutil.copytree(before,image,copy_function=os.link);(image/'Lokad.Lython.dll').unlink();shutil.copyfile('src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',image/'Lokad.Lython.dll');shutil.copyfile('src/Lokad.Lython/bin/Release/net10.0/Lokad.Lython.dll',r/'candidate-net10.dll')
(r/'diagnostic-identity.json').write_text(json.dumps(dict(beforeRevision='f4f44caf2bea091e9f1f117abc33b37b2b429f5a',qualification='43155450349terminalGREENcached Windows/Ubuntu actual11/10originalstress/installedconsumers',sourceHashes=source,changedProductionFiles=changed,beforeCoreSHA256=sha(before/'Lokad.Lython.dll'),candidateCoreSHA256=sha(image/'Lokad.Lython.dll'),candidateNet10SHA256=sha(r/'candidate-net10.dll'),focusedChecks=checks,comparison='R262/R263 complete report UTF8/UTF16, CSV/text bothAPIs and separatecompile. Same worker/CoreLib/GC; desktopclocks diagnostic. OriginalR262wholefileform andadversitypreserved.',contract='Shared final-encoding kernels; workingbatch admittedbeforefirstwriteI/O, fullinputcodecvalidationbeforeeffects, boundedbytebatcheswithretainedfilecapacityandexplicitflushrelease, genuineasync ordinary/exposed/writelines/CSV/JSON producerborrows throughawaitedcleanup. No outputcap, hiddenallowance, fuel, tracing ornewowner. BOM/payloadlogicalprefixfailure retiresfile.',limitations='Writes maypublishwhenstagingfills, ratherthanonlyexplicitflush/close. Physicalcapacitymayexceednominal64KiBthroughgeometricheadroombutisboundedunder128KiB. Nativeproducer/inputstoragekeepsitsownfunding; no generalguestheapreclamation orphysicalRSSclaim.'),indent=2)+'\n',encoding='utf-8')
with tarfile.open(r/'sources-candidate.tar.xz','w:xz')as a:
 for n in source:a.add(n,arcname=n)
dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe');env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
with(r/'build-driver.log').open('wb')as f:p=subprocess.run([str(dotnet),'build',str(r/'tool/complete-jobs.csproj'),'-c','Release','-f','net11.0','--nologo','--tl:off'],env=env,stdout=f,stderr=subprocess.STDOUT)
assert p.returncode==0
for phase in ['warm-sync','warm-async','compile']:
 d=r/phase;cmd=[str(dotnet),str(r/'tool/bin/Release/net11.0/complete-jobs.dll'),str(before/'Lokad.Lython.Benchmarks.dll'),str(image/'Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(d/'hash-witness.json'),'forward','compile'if phase=='compile'else'warm','async'if phase=='warm-async'else'sync']
 with(d/'measurements.json').open('wb')as f,(d/'stderr.log').open('wb')as err:p=subprocess.run(cmd,env=env,stdout=f,stderr=err)
 assert p.returncode==0,(phase,(d/'stderr.log').read_text(encoding='utf-8'))
 j=json.loads((d/'measurements.json').read_text(encoding='utf-8-sig'));assert len(j['observations'])==4 and j['allGoldensPassed']and j['allBalancesZero']and j['ownedFixturesRemoved'];print(phase,'fourcomplete scriptsfullgoldens/balances/cleanup pass',flush=True)
subprocess.run(['python','-X','utf8',str(r/'summarize.py')],check=True)
