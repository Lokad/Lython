from pathlib import Path
import json,os,subprocess
r=Path(__file__).parent;old=r.parent/'integer-utf8-r45'
env=os.environ.copy();env['DOTNET_ROOT']='C:/Users/JoannesVermorel/.dotnet11';env['DOTNET_HOST_PATH']=env['DOTNET_ROOT']+'/dotnet.exe';env['PATH']=env['DOTNET_ROOT']+os.pathsep+env['PATH']
for phase,api in [('warm','sync'),('warm','async')]:
 folder=r/(phase+'-'+api);folder.mkdir();witness=folder/'hash-witness.json'
 witness.write_text(json.dumps(dict(source="return [hash('Lython-é😀'), hash((1, 2.0, True))]\n"),ensure_ascii=False)+'\n',encoding='utf-8',newline='\n')
 with(folder/'measurements.json').open('w',encoding='utf-8')as out,(folder/'stderr.txt').open('w',encoding='utf-8')as err:
  done=subprocess.run([env['DOTNET_HOST_PATH'],str(old/'tool/out/complete-jobs.dll'),str(r/'images/before/Lokad.Lython.Benchmarks.dll'),str(r/'images/candidate/Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(witness),'reverse',phase,api],env=env,stdout=out,stderr=err)
 assert done.returncode==0,(folder/'stderr.txt').read_text(encoding='utf-8')[-3000:]
 data=json.loads((folder/'measurements.json').read_text(encoding='utf-8'));assert all(data[k]for k in ['allGoldensPassed','allBalancesZero','ownedFixturesRemoved','imageIsolationVerified'])
 print(phase,api,'all five golden/ownership/isolation/cleanup gates pass.',flush=True)
