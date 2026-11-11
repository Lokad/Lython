from pathlib import Path
import hashlib,json,shutil
r=Path(__file__).parent;n=r/'refined';n.mkdir()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for name,h in meta['sourceHashes'].items():assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==h,name
for phase in ['warm-sync','warm-async','compile']:
 assert (r/phase/'summary.json').exists();(n/phase).mkdir();shutil.copyfile(r/phase/'hash-witness.json',n/phase/'hash-witness.json')
for role in ['before','candidate']:shutil.copytree(r/'images/before',n/'images'/role)
shutil.copytree(r/'originals',n/'originals')
for name in ['before-net10.dll','catalog.json','python-goldens.json','python-goldens.py','run-focused.py','freeze-and-measure.py','summarize.py']:shutil.copyfile(r/name,n/name)
before=json.loads((r/'before.json').read_text(encoding='utf-8'))
before['hypothesis']+=' Refinement removes iteration-result references from ordinary recursive value evaluation. Prepared root continuations alone carry private handoff; moved StoreClosure/Conditional handling is not duplicated. Initial adverse source pair remains frozen; CPU cause unproved.'
(n/'before.json').write_text(json.dumps(before,indent=2)+'\n',encoding='utf-8')
# Copied scripts retain the existing driver one directory higher.
p=n/'freeze-and-measure.py';t=p.read_text(encoding='utf-8').replace("r.parent/'iteration-returns-r225/tool", "r.parent.parent/'iteration-returns-r225/tool");p.write_text(t,encoding='utf-8')
print('Initial exact source/core pair, checks and all local adversity frozen. Refined source pair keeps the same qualified baseline, worker, scripts and bounds.')
