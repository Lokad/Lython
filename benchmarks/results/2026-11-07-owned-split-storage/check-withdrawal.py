from pathlib import Path
import hashlib,json,os,re,subprocess
root=Path(__file__).parent
env=os.environ.copy();env['DOTNET_ROOT']='C:/Users/JoannesVermorel/.dotnet11';env['DOTNET_HOST_PATH']=env['DOTNET_ROOT']+'/dotnet.exe';env['PATH']=env['DOTNET_ROOT']+os.pathsep+'C:/Python313'+os.pathsep+env['PATH']
def run(args, log):
    with (root/log).open('w',encoding='utf-8') as output:
        result=subprocess.run([env['DOTNET_HOST_PATH'],*args],env=env,stdout=output,stderr=subprocess.STDOUT)
    if result.returncode:
        print((root/log).read_text(encoding='utf-8')[-3000:]);raise SystemExit(result.returncode)
    return (root/log).read_text(encoding='utf-8')
run(['build','Lokad.Lython.slnx','-c','Release','--no-incremental','--no-restore','--nologo','--tl:off'],'build-withdrawal.log')
checks={}
for tfm in ['net11.0','net10.0']:
    for suite,selection in [('Lokad.Lython.Tests','FullyQualifiedName~StringWindowOwnershipTests|FullyQualifiedName~WhitespaceStorageOwnershipTests'),
                            ('Lokad.Lython.PublicApi.Tests','FullyQualifiedName~SplitResultSubjectsKeepTheirOwnUnicodeOffsetsAndText')]:
        key=suite+'-'+tfm
        log=run(['test',f'tests/{suite}/{suite}.csproj','-c','Release','-f',tfm,'--no-build','--no-restore','--nologo','--tl:off','--filter',selection],f'focused-withdrawal-{key}.log')
        checks[key]=dict(exitCode=0,selection=selection,summaries=re.findall(r'Passed!\s+[^\r\n]+',log))
        assert checks[key]['summaries'],log
        print(key,checks[key],flush=True)
result=dict(checks=checks,coreSHA256={tfm:hashlib.sha256(Path(f'src/Lokad.Lython/bin/Release/{tfm}/Lokad.Lython.dll').read_bytes()).hexdigest() for tfm in ['net11.0','net10.0']})
(root/'focused-withdrawal.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
