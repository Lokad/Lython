from pathlib import Path
import hashlib,json,os,shutil,subprocess,tarfile,xml.etree.ElementTree as ET

r=Path(__file__).resolve().parent;repo=Path('D:/code/Lython');sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert not(r/'diagnostic-identity.json').exists()
checks={}
for label in ['final-internal11','final-public11','final-internal10','final-public10']:
    c=next(x for x in ET.parse(r/'tests'/(label+'.trx')).iter()if x.tag.endswith('Counters'))
    assert c.attrib['failed']=='0'and c.attrib['notExecuted']=='0';checks[label]=c.attrib
before=json.loads((r/'before.json').read_text(encoding='utf-8'))
source=json.loads((r/'focused-source-identity.json').read_text(encoding='utf-8'))
for n,h in source.items():assert sha(repo/n)==h,n
changed=sorted(n for n,h in source.items()if h!=before['sourceHashes'].get(n))
assert changed==sorted(['src/Lokad.Lython/'+n for n in ['Frontend/ExecutableScript.NativeReturns.cs','Runtime/LythonRuntime.Executable.Calls.Invoke.cs','Runtime/LythonRuntime.Executable.Inputs.cs','Runtime/LythonRuntime.Executable.Interpreter.Dispatch.cs','Runtime/LythonRuntime.Executable.NativeCalls.cs','Runtime/LythonRuntime.Executable.NativeReturn.cs','Runtime/LythonRuntime.ReceiverMethods.cs','Runtime/LythonRuntime.StringMembers.Basic.cs']]),changed
assert sha(r/'images/before/Lokad.Lython.dll')==before['baselineCoreSHA256']
shutil.copyfile(repo/'src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',r/'images/candidate/Lokad.Lython.dll')
shutil.copyfile(repo/'src/Lokad.Lython/bin/Release/net10.0/Lokad.Lython.dll',r/'candidate-net10.dll')
meta={'beforeRevision':before['head'],'beforeQualification':before['qualification'],'hypothesis':before['hypothesis'],'controls':before['controls'],'sourceHashes':source,'changedProductionFiles':changed,'focusedChecks':checks,'images':{role:{p.name:sha(p)for p in(r/'images'/role).iterdir()if p.is_file()}for role in ['before','candidate']},'net10SHA256':sha(r/'candidate-net10.dll'),'correctnessFix':'Replace now awaits guest count index protocols underRunAsync; validate old/new before callback.','measurement':'Seven complete scripts bothAPIs and separatecompile, existing common worker/host and256warmups/5rotated rounds16jobs. Desktop elapsed diagnostic; physical allocation and source simplification separate from CPU.'}
(r/'diagnostic-identity.json').write_text(json.dumps(meta,indent=2)+'\n',encoding='utf-8')
for role,hashes in [('before',before['sourceHashes']),('candidate',source)]:
    with tarfile.open(r/('sources-'+role+'.tar.xz'),'w:xz')as a:
        for n,h in hashes.items():
            p=r/'originals'/n if role=='before'else repo/n;assert sha(p)==h,n;a.add(p,arcname=n)
tool=r/'tool';tool.mkdir();old=r.parent/'iteration-returns-r225/tool'
shutil.copyfile(old/'complete-jobs.csproj',tool/'complete-jobs.csproj')
text=(old/'Program.cs').read_text(encoding='utf-8');needle='if (cases.Length != 6) throw new InvalidDataException("Require two counter variants and four complete-script controls.");'
assert text.count(needle)==1;text=text.replace(needle,'if (cases.Length != 7) throw new InvalidDataException("Require six unchanged controls and the guarded helper script.");')
text=text.replace('R225 ordinary return consumption; identical whole scripts and fixed common images; shared ALCs/process GC; precise allocation, desktop elapsed diagnostic only; compile/warm separate','R256 shared canonical receiver/truth services; seven complete scripts and fixed common images; shared ALCs/process GC; precise allocation, desktop elapsed diagnostic only; compile/warm separate')
(tool/'Program.cs').write_text(text,encoding='utf-8')
dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe');env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
with(r/'build-driver.log').open('wb')as f:p=subprocess.run([str(dotnet),'build',str(tool/'complete-jobs.csproj'),'-c','Release','--nologo','--tl:off'],cwd=repo,env=env,stdout=f,stderr=subprocess.STDOUT)
assert p.returncode==0,(r/'build-driver.log').read_text(encoding='utf-8')[-5000:]
for phase in ['warm-sync','warm-async','compile']:
    folder=r/phase;assert not(folder/'measurements.json').exists()
    cmd=[str(dotnet),str(tool/'bin/Release/net11.0/complete-jobs.dll'),str(r/'images/before/Lokad.Lython.Benchmarks.dll'),str(r/'images/candidate/Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(folder/'hash-witness.json'),'forward','compile'if phase=='compile'else'warm','async'if phase=='warm-async'else'sync']
    with(folder/'measurements.json').open('wb')as f,(folder/'stderr.log').open('wb')as err:p=subprocess.run(cmd,cwd=repo,env=env,stdout=f,stderr=err)
    assert p.returncode==0,(phase,(folder/'stderr.log').read_text(encoding='utf-8'))
    j=json.loads((folder/'measurements.json').read_text(encoding='utf-8-sig'));assert len(j['observations'])==7 and j['allGoldensPassed']and j['allBalancesZero']and j['ownedFixturesRemoved']
    print(phase,'seven full-script goldens/files/balances/cleanup pass.',flush=True)
