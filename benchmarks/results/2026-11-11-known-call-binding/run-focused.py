from pathlib import Path
import os,subprocess,time
r=Path(__file__).parent;dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe')
env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
filters={
 'internal':'(FullyQualifiedName~PreparedFunctionBindingBoundaryTests|FullyQualifiedName~SimpleFunctionExecutionBoundaryTests|FullyQualifiedName~BoundMethodInvocationBoundaryTests|FullyQualifiedName~PositionalCallTransportTests|FullyQualifiedName~NativeClosureSequenceTests|FullyQualifiedName~ParserCancellationTests|FullyQualifiedName~StaticAnalyzer|FullyQualifiedName~CallBinding)&Category!=Stress',
 'public':'(FullyQualifiedName~Function|FullyQualifiedName~CallInput|FullyQualifiedName~Closur|FullyQualifiedName~Nonlocal|FullyQualifiedName~ClassCell|FullyQualifiedName~Annotation|FullyQualifiedName~PreparedFunctionBinding|FullyQualifiedName~Cancellation|FullyQualifiedName~Framed|FullyQualifiedName~Callable|FullyQualifiedName~Argument)&Category!=Stress'}
for framework in ['net11.0','net10.0']:
 for kind in ['internal','public']:
  project='tests/Lokad.Lython.Tests/Lokad.Lython.Tests.csproj'if kind=='internal'else'tests/Lokad.Lython.PublicApi.Tests/Lokad.Lython.PublicApi.Tests.csproj'
  name='stable-'+kind+framework[3:5];log=r/(name+'.log');assert not log.exists()
  command=[str(dotnet),'test',project,'-c','Release','-f',framework,'--filter',filters[kind],'--logger','trx;LogFileName='+name+'.trx','--results-directory',str(r/'tests')]
  with log.open('wb')as f:done=subprocess.run(command,env=env,stdout=f,stderr=subprocess.STDOUT,timeout=180)
  assert done.returncode==0,(name,log.read_text(encoding='utf-8')[-10000:])
  print(name,log.read_text(encoding='utf-8').split('Passed!')[-1].strip(),flush=True)
