from pathlib import Path
import os,subprocess,json,shutil
r=Path('.git/agent-notes/review-20261031/receiver-operations-r256').resolve();s=r/'refined';dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe')
failed=s/'first-missing-witness';failed.mkdir()
for n in ['measurements.json','stderr.log']:shutil.move(s/'warm-sync'/n,failed/n)
env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
for phase in ['warm-sync','warm-async','compile']:
 folder=s/phase;folder.mkdir(exist_ok=True);shutil.copyfile(r/phase/'hash-witness.json',folder/'hash-witness.json')
 cmd=[str(dotnet),str(r/'tool/bin/Release/net11.0/complete-jobs.dll'),str(r/'images/before/Lokad.Lython.Benchmarks.dll'),str(s/'image/Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(folder/'hash-witness.json'),'forward','compile'if phase=='compile'else'warm','async'if phase=='warm-async'else'sync']
 with(folder/'measurements.json').open('wb')as f,(folder/'stderr.log').open('wb')as err:p=subprocess.run(cmd,env=env,stdout=f,stderr=err)
 assert p.returncode==0,(phase,(folder/'stderr.log').read_text(encoding='utf-8'))
 j=json.loads((folder/'measurements.json').read_text(encoding='utf-8-sig'));assert len(j['observations'])==7 and j['allGoldensPassed']and j['allBalancesZero']and j['ownedFixturesRemoved']
 print(phase,'seven full-script goldens/files/balances/cleanup pass.',flush=True)
shutil.copyfile(r/'summarize.py',s/'summarize.py')
