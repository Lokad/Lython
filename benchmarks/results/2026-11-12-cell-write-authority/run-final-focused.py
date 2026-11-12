from pathlib import Path
import hashlib,json,os,subprocess
r=Path('.git/agent-notes/review-20261031/cell-write-authority-r260').resolve();dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe');env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
source={p.as_posix():hashlib.sha256(p.read_bytes()).hexdigest()for p in Path('src/Lokad.Lython').rglob('*.cs')if not {'bin','obj'}&set(p.parts)};(r/'focused-source-identity.json').write_text(json.dumps(source,indent=2)+'\n',encoding='utf-8')
filters={'internal':'(FullyQualifiedName~Native|FullyQualifiedName~Closure|FullyQualifiedName~Executable|FullyQualifiedName~Function|FullyQualifiedName~Stack|FullyQualifiedName~Recurs)&Category!=Stress','public':'(FullyQualifiedName~Nonlocal|FullyQualifiedName~Closure|FullyQualifiedName~Generator|FullyQualifiedName~Lambda|FullyQualifiedName~Annotation|FullyQualifiedName~Cell|FullyQualifiedName~Native|FullyQualifiedName~Namespace|FullyQualifiedName~MirroredFunction|FullyQualifiedName~FramedFunction|FullyQualifiedName~Class|FullyQualifiedName~FunctionDefault|FullyQualifiedName~Unbound)&Category!=Stress'}
for framework in ['net11.0','net10.0']:
 for kind in ['internal','public']:
  project='tests/Lokad.Lython.Tests/Lokad.Lython.Tests.csproj'if kind=='internal'else'tests/Lokad.Lython.PublicApi.Tests/Lokad.Lython.PublicApi.Tests.csproj';name='final-'+kind+framework[3:5];log=r/(name+'.log');assert not log.exists()
  cmd=[str(dotnet),'test',project,'-c','Release','-f',framework,'--no-build','--filter',filters[kind],'--logger','trx;LogFileName='+name+'.trx','--results-directory',str(r/'tests')]
  with log.open('wb')as f:p=subprocess.run(cmd,env=env,stdout=f,stderr=subprocess.STDOUT,timeout=180)
  assert p.returncode==0,(name,log.read_text(encoding='utf-8')[-14000:])
  for n,h in source.items():assert hashlib.sha256(Path(n).read_bytes()).hexdigest()==h,n
  print(name,log.read_text(encoding='utf-8').split('Passed!')[-1].strip(),flush=True)
