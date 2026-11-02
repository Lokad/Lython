from pathlib import Path
import hashlib,json,shutil
r=Path('.git/agent-notes/review-20261031/native-local-forwarding-b130');old=r.parent/'shared-iteration-b127/refined'
for n in ['baseline','candidate']:shutil.copytree(old/'candidate',r/n,dirs_exist_ok=True)
for n in ['Lokad.Lython.dll','Lokad.Lython.pdb']:shutil.copy2(Path('src/Lokad.Lython/bin/Release/net11.0')/n,r/'candidate'/n)
cat=json.loads((r.parent/'operand-frame-source-b128/catalog.json').read_text(encoding='utf-8'))
ids=['scripts.csv-summary-helpers.large.locals','scripts.text-summary-helpers.large.locals','scripts.text-summary-helpers.large','loops.integer.large']
cat['cases']=[c for c in cat['cases'] if c['id'] in ids]
assert len(cat['cases'])==4
(r/'measurement-catalog.json').write_text(json.dumps(cat,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
s=(old/'collect.py').read_text(encoding='utf-8').replace('shared-iteration-b127-refined-20261102','native-local-forwarding-b130-20261102')
a=s.index('jobs = [(');b=s.index('env = os.environ.copy()',a)
jobs=[(n,'warm') for _ in range(2) for n in ids[:2]]+[(n,'warm') for n in ids[2:]]+[(n,'compile-run') for n in ids[:2]]
s=s[:a]+'jobs = '+repr(jobs)+'\n\n'+s[b:]
s=s.replace('block=job_index//4,','block=job_index//2,')
s=s.replace('B127 shared explicit iteration cursor; complete text/search, CSV and existing range controls; external process CPU through response; allocation separate','B130 native value forwarding; source7da7335a vs native-plan correction, complete named CSV/text warm/fresh plus compact text/range controls')
(r/'collect.py').write_text(s,encoding='utf-8',newline='\n')
for n in ['cpython-worker.py','hash-witness.json','assess-results.py','assess-allocation.py']:shutil.copy2(old/n,r/n)
v=json.loads((old/'identity.json').read_text(encoding='utf-8'))
v['sourceRevision']='7da7335a + native-plan adjacent local forwarding, original executable instructions unchanged'
v['baselineRuntime']='7da7335a'
v['boundary']='Warm and fresh compile-run measured separately; complete original worker/reset/I/O/output-check/prior-result cleanup boundary. Final cached owners/results/process/fixtures disposed and joined outside batches.'
v['coreSHA256']={n:hashlib.sha256((r/('candidate' if n=='candidate' else 'baseline')/'Lokad.Lython.dll').read_bytes()).hexdigest() for n in ['baseline','candidate','before-control']}
(r/'identity.json').write_text(json.dumps(v,indent=2),encoding='utf-8',newline='\n')
a=r/'allocation';a.mkdir(exist_ok=True)
prior=r.parent/'shared-iteration-b127/allocation'
shutil.copy2(prior/'complete-jobs.csproj',a/'complete-jobs.csproj')
(a/'Program.cs').write_text((prior/'Program.cs').read_text(encoding='utf-8'),encoding='utf-8',newline='\n')
alloc=dict(cat);alloc['cases']=[c for c in cat['cases'] if c['id'] in ids[:3]]
(r/'allocation-catalog.json').write_text(json.dumps(alloc,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
s=(old/'collect-allocation.sh').read_text(encoding='utf-8').replace('shared-iteration-b127-refined-20261102','native-local-forwarding-b130-20261102')
(r/'collect-allocation.sh').write_text(s,encoding='utf-8',newline='\n')
print(v['coreSHA256'])
