from pathlib import Path
import hashlib,json,os,shutil,subprocess,tarfile,xml.etree.ElementTree as ET
r=Path(__file__).resolve().parent;repo=Path.cwd();sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert not(r/'diagnostic-identity.json').exists()
for name in ['stable-internal11','stable-public11','stable-internal10','stable-public10']:
 tree=ET.parse(r/'tests'/(name+'.trx'));c=next(x for x in tree.iter()if x.tag.endswith('Counters'))
 assert c.attrib['failed']=='0'and c.attrib['notExecuted']=='0',name
before=json.loads((r/'before.json').read_text(encoding='utf-8'))
focused=json.loads((r/'focused-source-identity.json').read_text(encoding='utf-8'))
for name,h in focused.items():assert sha(repo/name)==h,name
hashes={n:sha(repo/n)for n in before['sourceHashes']};hashes.update(focused)
changed=sorted(n for n,h in hashes.items()if h!=before['sourceHashes'].get(n))
assert len(changed)==5,changed
assert 'src/Lokad.Lython/Runtime/LythonRuntime.Executable.DictionaryAccumulation.cs'in changed
for role in ['before','candidate']:
 for name,h in before['images'][role].items():assert sha(r/'images'/role/name)==h,(role,name)
shutil.copyfile(repo/'src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',r/'images/candidate/Lokad.Lython.dll')
shutil.copyfile(repo/'src/Lokad.Lython/bin/Release/net10.0/Lokad.Lython.dll',r/'candidate-net10.dll')
meta=dict(beforeRevision=before['head'],beforeQualification=before['qualification'],hypothesis=before['hypothesis'],controls=before['controls'],sourceHashes=hashes,changedProductionFiles=changed,images={role:{p.name:sha(p)for p in(r/'images'/role).iterdir()if p.is_file()}for role in ['before','candidate']},net10SHA256=sha(r/'candidate-net10.dll'),measurement='Existing complete-jobs worker/protocol, fixed common worker/adapter and separate core images sharing CLR/GC. 256warmups,5rotating rounds x16complete jobs; six unchanged scripts, both APIs and compilation separate. Desktop elapsed diagnostic; allocation/source simplification separate from CPU.')
(r/'diagnostic-identity.json').write_text(json.dumps(meta,indent=2)+'\n',encoding='utf-8')
for role,m in [('before',before),('candidate',meta)]:
 with tarfile.open(r/('sources-'+role+'.tar.xz'),'w:xz')as archive:
  for n,h in m['sourceHashes'].items():
   p=r/'originals'/n if role=='before'else repo/n;assert sha(p)==h,n;archive.add(p,arcname=n)
dotnet=Path('C:/Users/JoannesVermorel/.dotnet11/dotnet.exe');env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+';C:/Python313;'+env['PATH']
driver=r.parent/'iteration-returns-r225/tool/bin/Release/net11.0/complete-jobs.dll'
for phase in ['warm-sync','warm-async','compile']:
 folder=r/phase;assert not(folder/'measurements.json').exists()
 command=[str(dotnet),str(driver),str(r/'images/before/Lokad.Lython.Benchmarks.dll'),str(r/'images/candidate/Lokad.Lython.Benchmarks.dll'),str(r/'catalog.json'),str(folder/'hash-witness.json'),'forward','compile'if phase=='compile'else'warm','async'if phase=='warm-async'else'sync']
 with(folder/'measurements.json').open('wb')as stdout,(folder/'stderr.log').open('wb')as stderr:done=subprocess.run(command,stdout=stdout,stderr=stderr,env=env,timeout=240)
 assert done.returncode==0,(phase,(folder/'stderr.log').read_text(encoding='utf-8'))
 data=json.loads((folder/'measurements.json').read_text(encoding='utf-8-sig'));assert data['allGoldensPassed']and data['allBalancesZero']and data['ownedFixturesRemoved']
 print(phase,'complete: goldens, balances and fixture cleanup pass.',flush=True)
