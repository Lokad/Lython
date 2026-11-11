from pathlib import Path
import hashlib,importlib.util,json,os,shutil,subprocess,tempfile
r=Path(__file__).resolve().parent;out=r/'jit';out.mkdir(exist_ok=False)
spec=importlib.util.spec_from_file_location('protocol',Path('benchmarks/cpython-worker.py').resolve());protocol=importlib.util.module_from_spec(spec);spec.loader.exec_module(protocol)
case=next(c for c in json.loads((r/'catalog.json').read_text(encoding='utf-8'))['cases'] if c['id']=='scripts.report-render.large')
dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe');ids=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
source=r/'candidate-source'
for name in ids['sourceHashes']:
 p=source/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(Path(name),p)
record=[]
for role in ['before','candidate']:
 env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+os.pathsep+env['PATH']
 env['DOTNET_JitDisasm']='*ReadNativeValue* *TryReadRecordValue* *TryReadNativeStringValue* *MaterializeNativeValues*';env['DOTNET_JitStdOutFile']=str(out/(role+'.txt'))
 with tempfile.TemporaryDirectory(prefix='lython-comparison-files-')as fixture,(out/(role+'.stderr')).open('wb')as stderr:
  cmd=[str(dotnet),str(r/'images'/role/'Lokad.Lython.Benchmarks.dll'),'--compare','worker','--catalog',str(r/'catalog.json')]
  p=subprocess.Popen(cmd,cwd=fixture,env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=stderr)
  responses=[]
  try:
   ready=protocol.read_frame(p.stdout);assert ready['status']=='Ready',ready
   core=next(x for x in ready['libraries']if Path(x['path']).name=='Lokad.Lython.dll')
   assert core['sha256'].lower()==ids['images'][role]['Lokad.Lython.dll']
   for number,operation,count in [(1,'verify',2),(2,'batch',256)]:
    request={k:case[k]for k in ['sourceSha256','fixtureSha256','expectedOutputSha256']}
    request.update(protocolVersion=2,requestId=number,operation=operation,caseId=case['id'],lane='warm-async',iterations=count)
    protocol.write_frame(p.stdin,request);response=protocol.read_frame(p.stdout)
    assert response['status']==('Equivalent'if operation=='verify'else'Completed')and response['completedInvocations']==count,response
    responses.append(response)
   protocol.write_frame(p.stdin,dict(protocolVersion=2,requestId=3,operation='quit'))
   closed=protocol.read_frame(p.stdout);assert closed['status']=='Closed',closed
   assert p.wait(timeout=20)==0
   record.append(dict(role=role,command=cmd,ready=ready,responses=responses,closed=closed,exitCode=p.returncode,instrumentedNoPerformanceClaim=True))
  finally:
   if p.poll()is None:p.kill();p.wait()
   p.stdin.close();p.stdout.close()
 (out/'identity-and-checks.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
 text=(out/(role+'.txt')).read_text(encoding='utf-8')
 print(role,*[line for line in text.splitlines()if'Assembly listing for method'in line or'Total bytes of code'in line],sep='\n',flush=True)
