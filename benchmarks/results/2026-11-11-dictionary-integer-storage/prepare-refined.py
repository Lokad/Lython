from pathlib import Path
import hashlib,json,shutil
r=Path(__file__).parent;out=r/'refined';out.mkdir(exist_ok=False)
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for name,h in meta['sourceHashes'].items():assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==h,name
for name in meta['changedProductionFiles']:
 p=out/'originals'/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(r/'originals'/name,p)
for role in ['before','candidate']:shutil.copytree(r/'images/before',out/'images'/role)
for name in ['before-net10.dll','catalog.json','before.json','freeze-sources.py']:shutil.copyfile(r/name,out/name)
for phase in ['warm-sync','warm-async','compile']:
 (out/phase).mkdir();shutil.copyfile(r/phase/'hash-witness.json',out/phase/'hash-witness.json')
t=(r/'freeze-and-measure.py').read_text(encoding='utf-8').replace("r.parent/'iteration-returns-r225","r.parent.parent/'iteration-returns-r225").replace("r.parent/'call-preparation-r236","r.parent.parent/'call-preparation-r236")
(out/'freeze-and-measure.py').write_text(t,encoding='utf-8')
before=json.loads((out/'before.json').read_text(encoding='utf-8'))
before['hypothesis']+=' Refine only existing shared-record lookup: its values are materialized before layout publication, so read the actual owned array rather than inlining the generic lazy-integer branch. Initial JIT shared-record code422→562bytes; source-backed removal of dead allocation branch, not a timing-cause claim.'
(out/'before.json').write_text(json.dumps(before,indent=2)+'\n',encoding='utf-8')
t=(r/'run-focused.py').read_text(encoding='utf-8').replace('FullyQualifiedName~Csv|','FullyQualifiedName~Csv|FullyQualifiedName~Json|')
(out/'run-focused.py').write_text(t,encoding='utf-8')
print('Original exact sources/cores/JIT/observations preserved. Refinement changes one shared-record read; same qualified source pair, scripts and fixed workers.')
