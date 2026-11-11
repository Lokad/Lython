from pathlib import Path
import hashlib,json,os,shutil,subprocess
r=Path(__file__).resolve().parent
repo=Path.cwd()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
before=json.loads((r/'before.json').read_text(encoding='utf-8'))
for role in ['before','candidate']:
    for name,h in before['images'][role].items():
        assert sha(r/'images'/role/name)==h,(role,name)
assert not (r/'diagnostic-identity.json').exists()
changed=[]
hashes={}
for name,h in before['sourceHashes'].items():
    hashes[name]=sha(repo/name)
    if hashes[name]!=h:changed.append(name)
assert sorted(changed)==sorted(p.relative_to(r/'originals').as_posix()for p in(r/'originals').rglob('*.cs')),changed
shutil.copyfile(repo/'src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',r/'images/candidate/Lokad.Lython.dll')
shutil.copyfile(repo/'src/Lokad.Lython/bin/Release/net10.0/Lokad.Lython.dll',r/'candidate-net10.dll')
data=dict(beforeRevision=before['head'],beforeQualification=before['qualification'],
    hypothesis=before['hypothesis'],controls=before['controls'],sourceHashes=hashes,
    changedProductionFiles=changed,
    images={role:{p.name:sha(p)for p in(r/'images'/role).iterdir()if p.is_file()}for role in ['before','candidate']},
    net10SHA256=sha(r/'candidate-net10.dll'),
    measurement='Existing R225 complete-jobs binary; fixed common R236 worker/adapter, three isolated core images sharing CoreLib/GC, 256 warmups + 5 rounds x 16 complete jobs per image. Desktop clocks diagnostic; warm sync/async and compilation separate. No CPython ratios.')
(r/'diagnostic-identity.json').write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe')
env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+os.pathsep+env['PATH']
driver=r.parent/'iteration-returns-r225/tool/bin/Release/net11.0/complete-jobs.dll'
for phase in ['warm-sync','warm-async','compile']:
    folder=r/phase
    assert not (folder/'measurements.json').exists()
    command=[str(dotnet),str(driver),str(r/'images/before/Lokad.Lython.Benchmarks.dll'),
        str(r/'images/candidate/Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(folder/'hash-witness.json'),
        'forward','compile' if phase=='compile' else 'warm','async' if phase=='warm-async' else 'sync']
    with (folder/'measurements.json').open('wb')as stdout,(folder/'stderr.log').open('wb')as stderr:
        done=subprocess.run(command,stdout=stdout,stderr=stderr,env=env,timeout=240)
    assert done.returncode==0,(phase,done.returncode,(folder/'stderr.log').read_text(encoding='utf-8'))
    result=json.loads((folder/'measurements.json').read_text(encoding='utf-8-sig'))
    assert result['allGoldensPassed'] and result['allBalancesZero'] and result['ownedFixturesRemoved']
    print(phase+' complete: all script goldens, balances and owned fixture cleanup pass.',flush=True)
shutil.copyfile(r.parent/'call-preparation-r236/summarize.py',r/'summarize.py')
subprocess.run(['C:/Python313/python.exe','-X','utf8',str(r/'summarize.py')],check=True)
