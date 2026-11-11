from pathlib import Path
import hashlib,json,os,shutil,subprocess
r=Path(__file__).resolve().parent;dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe')
env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
before=json.loads((r/'before.json').read_text(encoding='utf-8'))
for framework in ['net11.0','net10.0']:
 core=r/('images/before/Lokad.Lython.dll'if framework=='net11.0'else'before-net10.dll')
 expected=before['images']['before']['Lokad.Lython.dll']if framework=='net11.0'else before['net10SHA256']
 assert hashlib.sha256(core.read_bytes()).hexdigest()==expected
 for kind,project,assembly,pattern in [
  ('public','Lokad.Lython.PublicApi.Tests','Lokad.Lython.PublicApi.Tests','FullyQualifiedName~NativeWordBinding|FullyQualifiedName~DictionaryCountingLoop|FullyQualifiedName~ActiveCancellationStopsRepeatedCasedHistogram'),
  ('internal','Lokad.Lython.Tests','Lokad.Lython.WhiteBox.Tests','FullyQualifiedName~DictionaryCountingLoopAdmission')]:
  folder=r/'baseline-tests'/framework/kind;assert not folder.exists()
  shutil.copytree(Path('tests')/project/'bin/Release'/framework,folder,copy_function=os.link)
  # The isolated tree owns this link only. Never replace the actual build output.
  dll=folder/'Lokad.Lython.dll';assert dll.is_relative_to(r/'baseline-tests');dll.unlink();shutil.copyfile(core,dll)
  assert hashlib.sha256(dll.read_bytes()).hexdigest()==expected
  name='baseline-regressions-'+framework+'-'+kind
  command=[str(dotnet),'vstest',str(folder/(assembly+'.dll')),'/TestCaseFilter:('+pattern+')&Category!=Stress','/Logger:trx;LogFileName='+name+'.trx','/ResultsDirectory:'+str(r/'tests')]
  with(r/(name+'.log')).open('xb')as f:done=subprocess.run(command,env=env,stdout=f,stderr=subprocess.STDOUT,timeout=120)
  assert done.returncode==0,(r/(name+'.log')).read_text(encoding='utf-8')[-12000:]
  print(name,(r/(name+'.log')).read_text(encoding='utf-8').split('Passed!')[-1].strip(),flush=True)
print('New externally useful regressions also pass exact qualified reference images; no production/source/build-output restoration performed.')
