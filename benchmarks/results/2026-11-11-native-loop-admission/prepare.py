from pathlib import Path
import hashlib,json,shutil,subprocess
r=Path(__file__).parent;r.mkdir(parents=True,exist_ok=True);prior=r.parent/'dictionary-value-storage-r245';repo=Path.cwd()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
meta=json.loads((prior/'refined/diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(repo/n)==h,n
target='src/Lokad.Lython/Runtime/LythonRuntime.Executable.DictionaryAccumulation.cs'
p=r/'originals'/target;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(target,p)
for role in ['before','candidate']:shutil.copytree(prior/'refined/images/candidate',r/'images'/role)
shutil.copyfile(prior/'refined/candidate-net10.dll',r/'before-net10.dll');shutil.copyfile(prior/'catalog.json',r/'catalog.json')
for phase in ['warm-sync','warm-async','compile']:
 (r/phase).mkdir();shutil.copyfile(prior/'refined'/phase/'hash-witness.json',r/phase/'hash-witness.json')
data=dict(head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),runtimeSource='8f8628a4 exact frozen refinedR245',sourceHashes=meta['sourceHashes'],images={role:{p.name:sha(p)for p in(r/'images'/role).iterdir()if p.is_file()}for role in ['before','candidate']},net10SHA256=sha(r/'before-net10.dll'),qualification='5092focused actual11/10+4Python goldens. Corrected458be840/43087005596 both platforms ordinary11/10 and package consumers pass, original stress still pending; first43081976930callbackdeadline failure preserved. No production change in later doc/test commits.',hypothesis='One managed stack admission scope for the existing native dictionary loop segment, replacing per-word entry/exit and inner try/finally. First admission remains after first binding and basic guards; existing loop cancellation/refusal checks remain. Scope leaves before fallback/error/return and private-total publication. Conservative extra logical nesting across namespace-binding reentry is explicit, managed safety never weakened. No new policy, guard framework, execution counter or ownership layer.',controls='Existing complete captured/ordinary text, CSV, search/report/JSON controls; both APIs and separate compilation. Preserve all adverse data and compare source/layout/CPU separately. No speed forecast or fixed percentage veto.')
(r/'before.json').write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
shutil.copyfile('PLAN.md',r/'PLAN-before-r247.md')
shutil.copyfile(prior/'freeze-and-measure.py',r/'freeze-and-measure.py')
print('Exact retained8f862 runtime frozen before one-file scope consolidation; current full delivery state recorded honestly, existing scripts/worker reused.')
