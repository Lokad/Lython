from pathlib import Path
import hashlib,json,os,re,shutil,subprocess,sys
root=Path(__file__).resolve().parent;repo=Path('D:/code/Lython')
env=os.environ.copy();env['DOTNET_ROOT']='C:/Users/JoannesVermorel/.dotnet11';env['DOTNET_HOST_PATH']=env['DOTNET_ROOT']+'/dotnet.exe';env['PATH']=env['DOTNET_ROOT']+os.pathsep+env['PATH']
props=[f'-p:DirectoryBuildPropsPath={root}/Directory.Build.props',f'-p:DirectoryPackagesPropsPath={root}/Directory.Packages.props',f'-p:RestoreConfigFile={root}/NuGet.Config','-p:RestoreNoCache=true','-p:RestoreForce=true','-p:RestoreForceEvaluate=true']
checks={}
def run(label,args):
    log=root/(label+'.log')
    if log.exists():
        log=root/(label+'-lock-corrected.log')
    assert not log.exists(),str(log)
    with log.open('w',encoding='utf-8') as output:
        r=subprocess.run([env['DOTNET_HOST_PATH'],*args,*props],cwd=repo,env=env,stdout=output,stderr=subprocess.STDOUT)
    text=log.read_text(encoding='utf-8');print(label,r.returncode,text[-1000:],flush=True)
    if r.returncode:sys.exit(r.returncode)
    m=re.search(r'Passed:\s+(\d+)',text);checks[label]={'passed':int(m[1])} if m else {'exitCode':r.returncode}
for name in ['Lokad.Lython.Tests','Lokad.Lython.PublicApi.Tests']:
    run('build-'+name,['build','tests/'+name,'-c','Release','--nologo','--tl:off'])
manifest=json.loads((root.parent/'adapter40/workflow-fixture-manifest.json').read_text(encoding='utf-8'))
for relative,files in manifest.items():
    source=repo/'tests/Fixtures'/relative
    for name,digest in files.items():assert hashlib.sha256((source/name).read_bytes()).hexdigest()==digest
    for framework in ['net11.0','net10.0']:
        target=root/f'out/Lokad.Lython.PublicApi.Tests/Release/{framework}/Fixtures'/relative
        assert not target.exists();shutil.copytree(source,target)
link=root/'tests/Lokad.Lython.WorkerFixture/bin/Release';link.parent.mkdir(parents=True)
subprocess.run(['cmd','/c','mklink','/J',str(link),str(root/'out/Lokad.Lython.WorkerFixture/Release')],check=True,capture_output=True)
for framework in ['net11.0','net10.0']:
    for suite in ['Lokad.Lython.Tests','Lokad.Lython.PublicApi.Tests']:
        selection='Category!=Stress&(FullyQualifiedName~Regex|FullyQualifiedName~Fnmatch|FullyQualifiedName~Glob|FullyQualifiedName~CursorAdmissionTests)'
        run(suite+'-'+framework,['test','tests/'+suite,'-c','Release','-f',framework,'--no-build','--no-restore','--filter',selection])
    folder=root/f'out/Lokad.Lython.PublicApi.Tests/Release/{framework}'
    checks[framework+'-loadedFiles']={name:hashlib.sha256((folder/(name+'.dll')).read_bytes()).hexdigest()
        for name in ['Lokad.Lython','Lokad.Lython.Utf8Regex','Lokad.Utf8Regex','Lokad.Utf8Regex.PythonRe']}
(root/'qualification.json').write_text(json.dumps({'identity':json.loads((root/'identity.json').read_text(encoding='utf-8')),
    'checks':checks,'limitations':'Private immutable development adapter only; normal pin/source unchanged; final producer/safety qualification separate'},indent=2)+'\n',encoding='utf-8')
