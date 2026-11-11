from pathlib import Path
import hashlib,json,shutil,subprocess
r=Path(__file__).resolve().parent;repo=Path.cwd();prior=r.parent/'native-closure-sequence-r243'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
old=json.loads((prior/'refined/diagnostic-identity.json').read_text(encoding='utf-8'))
for name,h in old['sourceHashes'].items():assert sha(repo/name)==h,name
assert not(r/'before.json').exists()
targets=['Frontend/Syntax.cs','Frontend/ExecutableScript.cs','Frontend/ExecutableScript.Infrastructure.cs','Frontend/StaticAbstractInterpreter.ExpressionAnalyzers.cs','Frontend/StaticBindingEngine.Functions.cs','Runtime/Calls/FunctionBindingPlan.cs','Runtime/LythonRuntime.Calls.Binding.cs']
for f in targets:
 name='src/Lokad.Lython/'+f;out=r/'originals'/name;out.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(repo/name,out)
for role in ['before','candidate']:
 shutil.copytree(prior/'refined/images/candidate',r/'images'/role)
 assert sha(r/'images'/role/'Lokad.Lython.dll')=='ccdd645dc6d2a541dbea0aaf170e33381666563d2579cd6f596fbebce427aeaa'
shutil.copyfile(prior/'refined/candidate-net10.dll',r/'before-net10.dll')
catalog=json.loads((prior/'b584/representative.json').read_text(encoding='utf-8'))
ids=['scripts.file-search.large','scripts.csv-validated-records.large','scripts.text-summary-helpers.nonlocal-counter.large','scripts.text-summary-helpers.large','scripts.report-render.large','scripts.json-summary-helpers.large']
catalog['cases']=[next(c for c in catalog['cases']if c['id']==n)for n in ids]
(r/'catalog.json').write_text(json.dumps(catalog,indent=2)+'\n',encoding='utf-8')
for phase in ['warm-sync','warm-async','compile']:
 (r/phase).mkdir();shutil.copyfile(prior/'refined'/phase/'hash-witness.json',r/phase/'hash-witness.json')
data=dict(head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),runtimeSource='6904093e',qualification='FrozenR243 ccdd core, all production hashes exact retained690, 4742focused11/10 and10Python goldens. Windows fullGREEN, Ubuntu ordinary/package pass and originalStress pending43058223560; no unchanged qualification replay.',
 sourceHashes=old['sourceHashes'],images={role:{p.name:sha(p)for p in(r/'images'/role).iterdir()if p.is_file()}for role in ['before','candidate']},net10SHA256=sha(r/'before-net10.dll'),
 hypothesis='Compiler-owned known signature/argument-to-slot metadata reuses immutable FunctionParameterLayout identity in current native/owned destination binding. Skip repeated runtime normalization, keyword matching and omitted-input discovery; live defaults still read from selected function. No runtime call cache/controller/new dispatch/owner; mismatched/protocol/dynamic signatures keep general binder.',
 controls='Unchanged complete keyword-default search and CSV helpers, captured/ordinary text/report/JSON controls; both APIs and compilation separate. Preserve all adverse data, source pairs and debt; no speed forecast or fixed veto.')
(r/'before.json').write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
shutil.copyfile('PLAN.md',r/'PLAN-before-r244.md')
shutil.copyfile(prior/'freeze-and-measure.py',r/'freeze-and-measure.py')
print('Exact retained690 source/cores frozen before R244 edits; existing common worker and six complete scripts reused.')
