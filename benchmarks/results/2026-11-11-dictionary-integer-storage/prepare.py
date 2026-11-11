from pathlib import Path
import hashlib,json,shutil,subprocess
r=Path(__file__).parent;r.mkdir(parents=True,exist_ok=True);repo=Path.cwd();prior=r.parent/'known-call-binding-r244'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
before=json.loads((prior/'before.json').read_text(encoding='utf-8'))
for n,h in before['sourceHashes'].items():assert sha(repo/n)==h,n
targets=['Runtime/PyDict.cs','Runtime/PyDict.Storage.cs','Runtime/PyDict.Accumulation.cs','Runtime/PyDict.Records.cs']
for f in targets:
 n='src/Lokad.Lython/'+f;p=r/'originals'/n;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(n,p)
for role in ['before','candidate']:shutil.copytree(prior/'images/before',r/'images'/role)
shutil.copyfile(prior/'before-net10.dll',r/'before-net10.dll');shutil.copyfile(prior/'catalog.json',r/'catalog.json')
for phase in ['warm-sync','warm-async','compile']:
 (r/phase).mkdir();shutil.copyfile(prior/phase/'hash-witness.json',r/phase/'hash-witness.json')
before['head']=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
before['qualification']='Exact unchanged fully qualified6904093e production source; frozenccdd source pair, no unchanged full qualification replay.'
before['hypothesis']='Native dictionary integer accumulation retains a machine integer in its already owned entry until a Python read materializes one immutable cached BigInteger. Remove per-update integer boxes/BigInteger operator work; no side table/tagged public value/new owner or dispatch. Existing coarse slot headroom funds the eventual box, entry capacity/overlap admits extra eight bytes. Copy/share materialize before alias publication.'
before['controls']='Existing complete captured/ordinary text and CSV helpers; search/report/JSON controls, both APIs and separate compilation. No speed forecast. Preserve all before/after controls and adverse data.'
(r/'before.json').write_text(json.dumps(before,indent=2)+'\n',encoding='utf-8')
shutil.copyfile('PLAN.md',r/'PLAN-before-r245.md')
shutil.copyfile(prior/'freeze-and-measure.py',r/'freeze-and-measure.py')
print('Qualified baseline frozen before dictionary value representation change; same scripts/worker/adapter.')
