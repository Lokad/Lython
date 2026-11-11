from pathlib import Path
import hashlib,json,os,subprocess
r=Path(__file__).resolve().parent;dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe')
env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
filters={
 'internal':'(FullyQualifiedName~Dictionary|FullyQualifiedName~Native|FullyQualifiedName~Executable|FullyQualifiedName~Stack|FullyQualifiedName~Recurs|FullyQualifiedName~Call|FullyQualifiedName~Function|FullyQualifiedName~CasedSplit|FullyQualifiedName~Closure|FullyQualifiedName~Record)&Category!=Stress',
 'public':'(FullyQualifiedName~NativeWordBinding|FullyQualifiedName~ReturnedIteration|FullyQualifiedName~CasedWhitespace|FullyQualifiedName~Dictionary|FullyQualifiedName~Closur|FullyQualifiedName~Nonlocal|FullyQualifiedName~FramedFunction|FullyQualifiedName~CallInput|FullyQualifiedName~FunctionDefault|FullyQualifiedName~ReceiverFunction|FullyQualifiedName~PreparedCall|FullyQualifiedName~Exception|FullyQualifiedName~Generator|FullyQualifiedName~Comprehension|FullyQualifiedName~Return|FullyQualifiedName~Cancellation|FullyQualifiedName~Stack|FullyQualifiedName~Recurs|FullyQualifiedName~SharedMemory|FullyQualifiedName~Json)&Category!=Stress'}
source={p.as_posix():hashlib.sha256(p.read_bytes()).hexdigest()for p in Path('src/Lokad.Lython').rglob('*.cs')if not {'bin','obj'}&set(p.parts)}
(r/'focused-source-identity.json').write_text(json.dumps(source,indent=2)+'\n',encoding='utf-8')
for framework in ['net11.0','net10.0']:
 for kind in ['internal','public']:
  project='tests/Lokad.Lython.Tests/Lokad.Lython.Tests.csproj'if kind=='internal'else'tests/Lokad.Lython.PublicApi.Tests/Lokad.Lython.PublicApi.Tests.csproj'
  name='stable-'+kind+framework[3:5];log=r/(name+'.log');assert not log.exists()
  command=[str(dotnet),'test',project,'-c','Release','-f',framework,'--filter',filters[kind],'--logger','trx;LogFileName='+name+'.trx','--results-directory',str(r/'tests')]
  with log.open('wb')as f:done=subprocess.run(command,env=env,stdout=f,stderr=subprocess.STDOUT,timeout=180)
  assert done.returncode==0,(name,log.read_text(encoding='utf-8')[-15000:])
  for n,h in source.items():assert hashlib.sha256(Path(n).read_bytes()).hexdigest()==h,n
  print(name,log.read_text(encoding='utf-8').split('Passed!')[-1].strip(),flush=True)
