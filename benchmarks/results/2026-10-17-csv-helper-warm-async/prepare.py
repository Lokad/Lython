import os,json,subprocess,fcntl,hashlib,time
from pathlib import Path
from datetime import datetime,timezone
stage=Path('/home/lokad/lython-bench-checks/csv-warm-async-1b6a1531')
dotnet='/home/lokad/lython-bench-toolchains/dotnet/10.0.401/dotnet'
lease=Path('/home/lokad/lython-bench-checks/comparison-vm.lock')
status={'startedUtc':datetime.now(timezone.utc).isoformat(),'phase':'lease'}
def save(): (stage/'preparation.json').write_text(json.dumps(status,indent=2)+'\n')
def run(args,cwd=None,log=None):
    result=subprocess.run(args,cwd=cwd,env=env,capture_output=True,text=True)
    if log: (stage/log).write_text(result.stdout+result.stderr)
    if result.returncode: raise RuntimeError(f'{args[0]} failed:{result.returncode}: {(result.stderr or result.stdout)[-2500:]}')
    return result.stdout.strip()
env=dict(os.environ,DOTNET_ROOT=str(Path(dotnet).parent),DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1',MSBUILDDISABLENODEREUSE='1',DOTNET_CLI_USE_MSBUILD_SERVER='0',GIT_TERMINAL_PROMPT='0')
source_patterns='/*\n!/*/\n/src/\n/benchmarks/Lokad.Lython.Benchmarks/\n/benchmarks/scripts/\n/benchmarks/cpython-worker.py\n/third_party/\n'
try:
    with lease.open('a+') as lock:
        fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
        launch=stage/'build-launch';launch.mkdir()
        (launch/'global.json').write_text(json.dumps({'sdk':{'version':'10.0.401','rollForward':'disable'}}))
        status.update({'dotnetVersion':run([dotnet,'--version'],cwd=launch),'loadBefore':os.getloadavg(),'workers':{}});save()
        for name,revision in [('old','5b546b2edbeaba45f8fe1b79be0b542fba0b5444'),('candidate','1b6a15318b00ec056af02e23b72fa64f861964df')]:
            root=stage/('source-'+name);root.mkdir()
            status.update(phase='fetch-'+name);save()
            run(['git','init','-q',str(root)])
            run(['git','-C',str(root),'remote','add','origin','https://github.com/Lokad/Lython.git'])
            run(['git','-C',str(root),'fetch','--depth=1','--filter=blob:none','origin',revision],log='fetch-'+name+'.log')
            run(['git','-C',str(root),'config','core.sparseCheckout','true'])
            (root/'.git/info/sparse-checkout').write_text(source_patterns)
            run(['git','-C',str(root),'checkout','--detach',revision],log='checkout-'+name+'.log')
            assert run(['git','-C',str(root),'rev-parse','HEAD'])==revision
            assert not run(['git','-C',str(root),'status','--porcelain','--untracked-files=normal'])
            status.update(phase='build-'+name);save()
            project=root/'benchmarks/Lokad.Lython.Benchmarks/Lokad.Lython.Benchmarks.csproj'
            run([dotnet,'build',str(project),'-c','Release','-v','minimal','-nodeReuse:false'],cwd=launch,log='build-'+name+'.log')
            assert not run(['git','-C',str(root),'status','--porcelain','--untracked-files=normal'])
            assembly=project.parent/'bin/Release/net10.0/Lokad.Lython.Benchmarks.dll'
            library=assembly.with_name('Lokad.Lython.dll')
            status['workers'][name]={'revision':revision,'root':str(root),'worker':str(assembly),'workerSha256':hashlib.sha256(assembly.read_bytes()).hexdigest(),'librarySha256':hashlib.sha256(library.read_bytes()).hexdigest(),'sourceClean':True}
            save()
        status.update(phase='catalog')
        candidate=Path(status['workers']['candidate']['root'])
        catalog=stage/'catalog.json'
        run([dotnet,status['workers']['candidate']['worker'],'--compare','list','--profile','representative','--out',str(catalog)],cwd=candidate,log='catalog.log')
        status.update(phase='complete',completedUtc=datetime.now(timezone.utc).isoformat(),catalog=str(catalog),catalogSha256=hashlib.sha256(catalog.read_bytes()).hexdigest(),loadAfter=os.getloadavg());save()
except BaseException as error:
    status.update(phase='failed',error=repr(error),completedUtc=datetime.now(timezone.utc).isoformat());save();raise
