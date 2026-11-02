from pathlib import Path
import hashlib,json,shutil
r=Path('.git/agent-notes/review-20261031/shared-iteration-b127');old=r.parent/'call-frame-source-b124'
for n in ['baseline','candidate']:shutil.copytree(old/'candidate',r/n,dirs_exist_ok=True)
for n in ['Lokad.Lython.dll','Lokad.Lython.pdb']:shutil.copy2(Path('src/Lokad.Lython/bin/Release/net11.0')/n,r/'candidate'/n)
assert hashlib.sha256((r/'baseline/Lokad.Lython.dll').read_bytes()).hexdigest()=='137b563ac198d769a13f45b1894b8fb4309a764b6da62b7e94bd14d01a8a58f5'
catalog=json.loads((old/'measurement-catalog.json').read_text(encoding='utf-8-sig'))
ids=['scripts.text-summary-helpers.large','scripts.file-search.large','scripts.csv-summary-helpers.large']
selected=[next(c for c in catalog['cases'] if c['id']==n) for n in ids]
core=json.loads((r/'core-catalog.json').read_text(encoding='utf-8-sig'))
control=next(c for c in core['cases'] if c['id']=='loops.integer.large')
selected.append(control);catalog['cases']=selected
(r/'measurement-catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
text=(old/'collect.py').read_text(encoding='utf-8').replace('call-frame-source-b124-20261102','shared-iteration-b127-20261102')
a=text.index('jobs = [(');b=text.index('env = os.environ.copy()',a)
text=text[:a]+"jobs = [(case,'warm') for block in range(2) for case in "+repr(ids+[control['id']])+']\n\n'+text[b:]
text=text.replace('if job_index >= 5:','if job_index >= 4:').replace('block=job_index//5,','block=job_index//4,').replace('time.monotonic()-started < 250','time.monotonic()-started < 180')
text=text.replace("response['actualFilesSha256'] == self.case['expectedFilesSha256']","response.get('actualFilesSha256') == self.case.get('expectedFilesSha256')")
text=text.replace('B124 closed native module functions without lexical context allocation; complete CSV/JSON/text/search plus report control; external process CPU through response; allocation separate','B127 shared explicit iteration cursor; complete text/search, CSV and existing range controls; external process CPU through response; allocation separate')
(r/'collect.py').write_text(text,encoding='utf-8',newline='\n')
for n in ['cpython-worker.py','hash-witness.json','assess-allocation.py']:shutil.copy2(old/n,r/n)
identity=json.loads((old/'identity.json').read_text(encoding='utf-8-sig'))
identity['sourceRevision']='19fc4324/runtime90785737 + B127 shared iteration with terminal cleanup and original-subject retention'
identity['baselineRuntime']='90785737'
for n in ['baseline','candidate','before-control']:identity['coreSHA256'][n]=hashlib.sha256((r/('candidate' if n=='candidate' else 'baseline')/'Lokad.Lython.dll').read_bytes()).hexdigest()
(r/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8',newline='\n')
text=(old/'assess-results.py').read_text(encoding='utf-8').replace("len(data['jobs'])==10","len(data['jobs'])==8")
(r/'assess-results.py').write_text(text,encoding='utf-8',newline='\n')
a=r/'allocation';a.mkdir(exist_ok=True)
shutil.copy2(old/'allocation/complete-jobs.csproj',a/'complete-jobs.csproj')
text=(old/'allocation/Program.cs').read_text(encoding='utf-8').replace('cases.Length != 4','cases.Length != 3')
(a/'Program.cs').write_text(text,encoding='utf-8',newline='\n')
allocation=dict(catalog);allocation['cases']=selected[:3]
(r/'allocation-catalog.json').write_text(json.dumps(allocation,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
text=(old/'collect-allocation.sh').read_text(encoding='utf-8').replace('call-frame-source-b124-20261102','shared-iteration-b127-20261102')
(r/'collect-allocation.sh').write_text(text,encoding='utf-8',newline='\n')
print(identity['coreSHA256']);print('Prepared8 warm jobs;3 complete-job allocation cases. Pure range uses existing catalog golden.')
