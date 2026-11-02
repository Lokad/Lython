import hashlib,json,pathlib,shutil
root=pathlib.Path(__file__).parent
old=root.parent/'shared-formatted-output-b120'
catalog=json.loads((old/'catalog.json').read_text(encoding='utf-8-sig'))
selected=['scripts.report-render.large','scripts.json-summary-helpers.large',
          'scripts.csv-summary-helpers.large','scripts.text-summary-helpers.large']
catalog['cases']=[c for name in selected for c in catalog['cases'] if c['id']==name]
(root/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
text=(old/'collect.py').read_text().replace('shared-formatted-output-b120-20261102','native-json-tokens-b123-20261102')
start=text.index('jobs = [(');end=text.index('env = os.environ.copy()',start)
text=text[:start]+"jobs = [(case,'warm') for block in range(2) for case in "+repr(selected)+']\n\n'+text[end:]
text=text.replace('if job_index >= 6:','if job_index >= 4:').replace('block=job_index//6,','block=job_index//4,')
text=text.replace('time.monotonic()-started < 260','time.monotonic()-started < 180')
text=text.replace('B120 shared formatted output; complete report warm/fresh/format-call and CSV/text/JSON controls; external process CPU through response; allocation measured separately',
    'B123 native JSON tokens; complete report/JSON-lines with CSV/text controls; external process CPU through response; allocation measured separately')
(root/'collect.py').write_text(text,encoding='utf-8',newline='\n')
shutil.copy2(old/'cpython-worker.py',root/'cpython-worker.py')
text=(old/'assess-results.py').read_text().replace("len(data['jobs'])==12","len(data['jobs'])==8")
text=text.replace('retrieved/confirmation-completion','retrieved/confirmation')
(root/'assess-results.py').write_text(text,encoding='utf-8',newline='\n')
identity={'sourceRevision':'aa6cf3cb + uncommitted B123 native plain JSON token construction',
    'baselineRuntime':'c3fa26cc',
    'coreSHA256':{label:hashlib.sha256((root/('candidate' if label=='candidate' else 'baseline')/'Lokad.Lython.dll').read_bytes()).hexdigest()
        for label in ['baseline','candidate','before-control']},
    'boundary':'Warm existing worker protocol: fixture staging/reset, actual mediated I/O, full stdout/file checks and prior-result cleanup inside batches; final result/script/process/fixture cleanup outside and joined. Startup/compilation excluded.',
    'cpuMetric':'External Linux/proc TGID user+system through response, weighted nine-batch means. Includes runtime/JIT/response, not guest-only CPU.'}
(root/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8',newline='\n')
jobs=root/'allocation';jobs.mkdir(exist_ok=True)
shutil.copy2(old/'allocation/complete-jobs.csproj',jobs/'complete-jobs.csproj')
text=(old/'allocation/Program.cs').read_text().replace('scripts.report-format-call.large','scripts.json-summary-helpers.large')
text=text.replace('Require the same complete report via f-string and format calls.','Require complete report and JSON-lines/helper scripts.')
(jobs/'Program.cs').write_text(text,encoding='utf-8',newline='\n')
allocation_catalog=dict(catalog);allocation_catalog['cases']=[c for c in catalog['cases'] if c['id'] in selected[:2]]
(root/'allocation-catalog.json').write_text(json.dumps(allocation_catalog,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
for name in ['hash-witness.json','assess-allocation.py']:
    shutil.copy2(old/name,root/name)
text=(old/'collect-allocation.sh').read_text().replace('shared-formatted-output-b120-20261102','native-json-tokens-b123-20261102')
(root/'collect-allocation.sh').write_text(text,encoding='utf-8',newline='\n')
