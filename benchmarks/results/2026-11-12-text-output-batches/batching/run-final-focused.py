from pathlib import Path
import os,subprocess,json,hashlib,xml.etree.ElementTree as ET
r=Path(__file__).parent.resolve();dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe');env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
source=json.loads((r/'focused-source-identity.json').read_text(encoding='utf-8'))
with(r/'build-focused-final.log').open('wb')as f: p=subprocess.run([str(dotnet),'build','Lokad.Lython.slnx','-c','Release','--no-incremental','--nologo','--tl:off'],env=env,stdout=f,stderr=subprocess.STDOUT)
assert p.returncode==0
filters={'internal':'(FullyQualifiedName~Codec|FullyQualifiedName~Encoding|FullyQualifiedName~TextFile|FullyQualifiedName~Csv|FullyQualifiedName~Json|FullyQualifiedName~FileHandle|FullyQualifiedName~FileOutput|FullyQualifiedName~RuntimeStorageSubsystem|FullyQualifiedName~StringBuilderAdmission|FullyQualifiedName~ReentrantAdmissionOwnership|FullyQualifiedName~HostOperation)&Category!=Stress','public':'(FullyQualifiedName~Codec|FullyQualifiedName~Encoding|FullyQualifiedName~TextFile|FullyQualifiedName~EncodedFileStaging|FullyQualifiedName~NativeProducer|FullyQualifiedName~FileOutput|FullyQualifiedName~Csv|FullyQualifiedName~Json|FullyQualifiedName~OwnedByteFile|FullyQualifiedName~AdmissionFailureCommand|FullyQualifiedName~HostCapability|FullyQualifiedName~FileCompatibility|FullyQualifiedName~FileStream)&Category!=Stress'}
checks={}
for framework in ['net11.0','net10.0']:
 for kind in ['internal','public']:
  name='final-'+kind+framework[3:5];project='tests/Lokad.Lython.Tests/Lokad.Lython.Tests.csproj'if kind=='internal'else'tests/Lokad.Lython.PublicApi.Tests/Lokad.Lython.PublicApi.Tests.csproj'
  cmd=[str(dotnet),'test',project,'-c','Release','-f',framework,'--no-build','--filter',filters[kind],'--logger','trx;LogFileName='+name+'.trx','--results-directory',str(r/'tests')]
  with(r/(name+'.log')).open('wb')as f:p=subprocess.run(cmd,env=env,stdout=f,stderr=subprocess.STDOUT,timeout=180)
  assert p.returncode==0,(name,(r/(name+'.log')).read_text(encoding='utf-8')[-14000:])
  c=next(x for x in ET.parse(r/'tests'/(name+'.trx')).iter()if x.tag.endswith('Counters'));assert c.attrib['failed']=='0'and c.attrib['notExecuted']=='0';checks[name]=c.attrib
  for n,h in source.items():assert hashlib.sha256(Path(n).read_bytes()).hexdigest()==h,n
  print(name,c.attrib,flush=True)
(r/'focused-checks.json').write_text(json.dumps(checks,indent=2)+'\n',encoding='utf-8')
