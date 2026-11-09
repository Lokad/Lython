from pathlib import Path
import os,json,hashlib,subprocess,sys
root=Path('D:/code/Lython'); home=Path(__file__).parent; evidence=root/'benchmarks/results/2026-11-09-private-small-integers'
env=os.environ.copy(); env['DOTNET_ROOT']='C:/Users/JoannesVermorel/.dotnet11'; env['DOTNET_HOST_PATH']=env['DOTNET_ROOT']+'/dotnet.exe'; env['PATH']=env['DOTNET_ROOT']+os.pathsep+env['PATH']
baseline=json.loads((home/'baseline.json').read_text(encoding='utf-8')); results=[]
for framework in ['net11.0','net10.0']:
    for phase,args in [('build',['build','src/Lokad.Lython/Lokad.Lython.csproj','--no-incremental']),
                       ('retained-public',['test','tests/Lokad.Lython.PublicApi.Tests/Lokad.Lython.PublicApi.Tests.csproj','--filter',
                        'FullyQualifiedName~GuestIntegerRepresentationTests|FullyQualifiedName~NumericIngressContractTests|FullyQualifiedName~FreshIntegerCreationTests|FullyQualifiedName~ExactIntegerAdditionTests|FullyQualifiedName~CallInputLifetimeTests'])]:
        log=evidence/'qualification'/(framework+'-restored-'+phase+'.log')
        with log.open('w',encoding='utf-8') as out:
            done=subprocess.run([env['DOTNET_HOST_PATH'],*args,'-c','Release','-f',framework,'-v','q'],env=env,cwd=root,stdout=out,stderr=subprocess.STDOUT,timeout=180)
        print(framework,phase,'exit',done.returncode,flush=True)
        results.append(dict(framework=framework,phase=phase,exitCode=done.returncode,log=log.name))
        if done.returncode:
            print(log.read_text(encoding='utf-8')[-3000:],flush=True);sys.exit(done.returncode)
for name,expected in baseline['productionSources'].items(): assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==expected,name
(evidence/'restored-checks.json').write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8')
print('Exact qualified production still matches after nonincremental builds; retained semantic/lifetime cases pass both actual targets.',flush=True)
