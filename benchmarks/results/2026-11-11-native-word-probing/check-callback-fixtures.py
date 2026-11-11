from pathlib import Path
import os,subprocess
r=Path(__file__).resolve().parent;dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe')
env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
for framework in ['net11.0','net10.0']:
 name='callback-fixtures-'+framework
 command=[str(dotnet),'test','tests/Lokad.Lython.PublicApi.Tests/Lokad.Lython.PublicApi.Tests.csproj','-c','Release','-f',framework,'--filter','(FullyQualifiedName~NativeWordBinding|FullyQualifiedName~ActiveCancellationStopsRepeatedCasedHistogram)&Category!=Stress','--logger','trx;LogFileName='+name+'.trx','--results-directory',str(r/'tests')]
 with(r/(name+'.log')).open('xb')as f:done=subprocess.run(command,env=env,stdout=f,stderr=subprocess.STDOUT,timeout=120)
 assert done.returncode==0,(r/(name+'.log')).read_text(encoding='utf-8')[-12000:]
 print(name,(r/(name+'.log')).read_text(encoding='utf-8').split('Passed!')[-1].strip(),flush=True)
