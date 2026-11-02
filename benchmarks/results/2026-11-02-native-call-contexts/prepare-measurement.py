import hashlib,json,pathlib,shutil
root=pathlib.Path('.git/agent-notes/review-20261031/call-frame-source-b124')
old=root.parent/'native-json-tokens-b123'
base=old/'refined/candidate'
for name in ['baseline','candidate']:
 shutil.copytree(base,root/name,dirs_exist_ok=True)
for name in ['Lokad.Lython.dll','Lokad.Lython.pdb']:
 shutil.copy2(pathlib.Path('src/Lokad.Lython/bin/Release/net11.0')/name,root/'candidate'/name)
assert hashlib.sha256((root/'baseline/Lokad.Lython.dll').read_bytes()).hexdigest()=='255c6cfc59e63673dd66cdf16a67dc973c2fcc1f5923811cd9a6c4ba35b43540'
selected=['scripts.csv-summary-helpers.large','scripts.json-summary-helpers.large','scripts.text-summary-helpers.large','scripts.file-search.large','scripts.report-render.large']
catalog=json.loads((root/'catalog.json').read_text(encoding='utf-8-sig'))
measure=dict(catalog);measure['cases']=[next(c for c in catalog['cases'] if c['id']==n) for n in selected]
(root/'measurement-catalog.json').write_text(json.dumps(measure,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
text=(old/'collect.py').read_text().replace('native-json-tokens-b123-20261102','call-frame-source-b124-20261102')
text=text.replace("catalog = root / 'catalog.json'","catalog = root / 'measurement-catalog.json'")
a=text.index('jobs = [(');b=text.index('env = os.environ.copy()',a)
text=text[:a]+"jobs = [(case,'warm') for block in range(2) for case in "+repr(selected)+']\n\n'+text[b:]
text=text.replace('if job_index >= 4:','if job_index >= 5:').replace('block=job_index//4,','block=job_index//5,').replace('time.monotonic()-started < 180','time.monotonic()-started < 250')
text=text.replace('B123 native JSON tokens; complete report/JSON-lines with CSV/text controls; external process CPU through response; allocation measured separately','B124 closed native module functions without lexical context allocation; complete CSV/JSON/text/search plus report control; external process CPU through response; allocation separate')
(root/'collect.py').write_text(text,encoding='utf-8',newline='\n')
shutil.copy2(old/'cpython-worker.py',root/'cpython-worker.py')
identity=json.loads((old/'refined/identity.json').read_text())
identity['sourceRevision']='090574d1 + B124 closed native module function context elimination'
identity['baselineRuntime']='090574d1'
for name in ['baseline','candidate','before-control']:
 identity['coreSHA256'][name]=hashlib.sha256((root/('candidate' if name=='candidate' else 'baseline')/'Lokad.Lython.dll').read_bytes()).hexdigest()
(root/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8',newline='\n')
text=(old/'assess-results.py').read_text().replace("len(data['jobs'])==8","len(data['jobs'])==10")
(root/'assess-results.py').write_text(text,encoding='utf-8',newline='\n')
alloc=root/'allocation';alloc.mkdir(exist_ok=True)
shutil.copy2(old/'allocation/complete-jobs.csproj',alloc/'complete-jobs.csproj')
text=(old/'allocation/Program.cs').read_text()
a=text.index('if (cases.Length != 2');b=text.index('var phase =',a)
text=text[:a]+'''if (cases.Length != 4) throw new InvalidDataException("Require four complete native helper scripts.");
'''+text[b:]
(alloc/'Program.cs').write_text(text,encoding='utf-8',newline='\n')
allocation=dict(measure);allocation['cases']=measure['cases'][:4]
(root/'allocation-catalog.json').write_text(json.dumps(allocation,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
for name in ['hash-witness.json','assess-allocation.py']:
 shutil.copy2(old/name,root/name)
text=(old/'collect-allocation.sh').read_text().replace('native-json-tokens-b123-20261102','call-frame-source-b124-20261102')
(root/'collect-allocation.sh').write_text(text,encoding='utf-8',newline='\n')
print(json.dumps(identity,indent=2))
