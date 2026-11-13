from pathlib import Path
import json,os,subprocess
r=Path(__file__).parent;sdk=Path('C:/Users/JoannesVermorel/.dotnet11');env=os.environ.copy();env['DOTNET_ROOT']=str(sdk);env['PATH']=str(sdk)+';C:/Python313;'+env['PATH']
with(r/'build.log').open('w',encoding='utf-8')as log:subprocess.run([str(sdk/'dotnet.exe'),'build',str(r/'tool/tool.csproj'),'-c','Release','--disable-build-servers'],env=env,stdout=log,stderr=subprocess.STDOUT,check=True)
with(r/'observations.json').open('w',encoding='utf-8')as out,(r/'stderr.log').open('w',encoding='utf-8')as err:done=subprocess.run([str(sdk/'dotnet.exe'),str(r/'tool/bin/Release/net11.0/Lokad.Lython.WhiteBox.Tests.dll'),str(r.parent/'integer-chain-r311/catalog.json')],env=env,stdout=out,stderr=err,timeout=60)
assert done.returncode==0,(r/'stderr.log').read_text(encoding='utf-8')
d=json.loads((r/'observations.json').read_text(encoding='utf-8'));assert d['coreSHA256']=='91d0540d439109dd35f3246c4c6234a69a2bc2b698012c83859e9d425d14cc78'
for x in d['rows']:print(x['id'],'parse',x['parserBytes'],'compile',x['wholeCompileAndCleanupBytes'],'token duplicate model',x['modeledDuplicateStringBytes'],round(x['modelPercentOfCompile'],2),'%',flush=True)
