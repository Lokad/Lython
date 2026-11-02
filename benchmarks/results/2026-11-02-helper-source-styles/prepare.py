from pathlib import Path
import json,hashlib,shutil
r=Path('.git/agent-notes/review-20261031/helper-styles-b129');r.mkdir(exist_ok=False)
old=r.parent/'shared-iteration-b127/refined'
for n in ['baseline','candidate']:shutil.copytree(old/'candidate',r/n)
allcases=json.loads((r.parent/'operand-frame-source-b128/catalog.json').read_text(encoding='utf-8'))
ids=['scripts.csv-summary-helpers.large','scripts.text-summary-helpers.large']
allcases['cases']=[c for c in allcases['cases'] if c['id'] in ids or c['id'] in [n+'.locals' for n in ids]]
assert len(allcases['cases'])==4
(r/'measurement-catalog.json').write_text(json.dumps(allcases,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
s=(old/'collect.py').read_text(encoding='utf-8').replace('shared-iteration-b127-refined-20261102','helper-styles-b129-20261102')
a=s.index('jobs = [(');b=s.index('env = os.environ.copy()',a)
s=s[:a]+'jobs = '+repr([(n,lane) for n in ids for lane in ['warm','compile-run']])+'\n\n'+s[b:]
s=s.replace('if job_index >= 4:','if job_index >= 2:').replace('block=job_index//4,','block=job_index//2,').replace('time.monotonic()-started < 180','time.monotonic()-started < 110')
s=s.replace('Worker(label,cases[case_id],lane,stack)','Worker(label,cases[case_id + (".locals" if label == "candidate" else "")],lane,stack)')
s=s.replace('B127 shared explicit iteration cursor; complete text/search, CSV and existing range controls; external process CPU through response; allocation separate','B129 current7da7335a helper source styles; candidate named intermediates vs compact baseline/unchanged control; not a source optimization; complete scripts and actual I/O/cleanup')
(r/'collect.py').write_text(s,encoding='utf-8',newline='\n')
for n in ['cpython-worker.py','hash-witness.json','assess-allocation.py']:shutil.copy2(old/n,r/n)
s=(old/'assess-results.py').read_text(encoding='utf-8').replace("len(data['jobs'])==8","len(data['jobs'])==4")
(r/'assess-results.py').write_text(s,encoding='utf-8',newline='\n')
v=json.loads((old/'identity.json').read_text(encoding='utf-8'))
v['sourceRevision']='7da7335a, same core in all images; candidate executes equivalent named-intermediate source'
v['baselineRuntime']='7da7335a'
v['coreSHA256']={n:hashlib.sha256((r/'candidate/Lokad.Lython.dll').read_bytes()).hexdigest() for n in ['baseline','candidate','before-control']}
(r/'identity.json').write_text(json.dumps(v,indent=2),encoding='utf-8',newline='\n')
a=r/'allocation';a.mkdir()
prior=r.parent/'shared-iteration-b127/allocation'
shutil.copy2(prior/'complete-jobs.csproj',a/'complete-jobs.csproj')
s=(prior/'Program.cs').read_text(encoding='utf-8').replace('cases.Length != 3','cases.Length != 2')
s=s.replace('var cases = catalog.RootElement.GetProperty("cases").EnumerateArray().ToArray();','var allCases = catalog.RootElement.GetProperty("cases").EnumerateArray().ToArray();\nvar cases = allCases.Where(x => !x.GetProperty("id").GetString()!.EndsWith(".locals",StringComparison.Ordinal)).ToArray();')
s=s.replace('new Image(candidatePath, item, hashSource, "candidate")','new Image(candidatePath, allCases.Single(x => x.GetProperty("id").GetString() == caseId + ".locals"), hashSource, "candidate")')
(a/'Program.cs').write_text(s,encoding='utf-8',newline='\n')
shutil.copy2(r/'measurement-catalog.json',r/'allocation-catalog.json')
s=(old/'collect-allocation.sh').read_text(encoding='utf-8').replace('shared-iteration-b127-refined-20261102','helper-styles-b129-20261102')
(r/'collect-allocation.sh').write_text(s,encoding='utf-8',newline='\n')
p=Path('PLAN.md');s=p.read_text(encoding='utf-8');s+='\nB129 ACTIVE bounded helper-style comparison at7da7335a: identical core/auxiliary\nbits, CSV/text canonical versus equivalent named intermediates, independent\nunchanged source control, warm/fresh separate;4 existing-protocol jobs plus short\nallocation diagnostic. This is source-style evidence, not a runtime optimization\nor refreshed CPython ratio. Decide next construction design from its results.\n';p.write_text(s,encoding='utf-8',newline='\n')
