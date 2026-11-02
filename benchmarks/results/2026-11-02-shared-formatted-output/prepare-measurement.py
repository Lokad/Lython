import base64,copy,hashlib,json,pathlib,shutil,subprocess,tempfile
root=pathlib.Path(__file__).parent
report_catalog=json.loads((root.parent/'formatted-output-source-b119/catalog.json').read_text(encoding='utf-8-sig'))
prior=json.loads((root.parent/'native-mapping-writes-b117/catalog.json').read_text(encoding='utf-8-sig'))
report=next(c for c in report_catalog['cases'] if c['id']=='scripts.report-render.large')
alternate=copy.deepcopy(report);alternate['id']='scripts.report-format-call.large'
alternate['source']=report['source'].replace('return f"| {record[\'name\']:<20.12s} | {record[\'state\']:^8s} | {record[\'amount\']:>8,d} |\\n"',
    '''return '| '+format(record['name'],'<20.12s')+' | '+format(record['state'], '^8s')+' | '+format(record['amount'],'>8,d')+' |\\n' ''')
assert alternate['source']!=report['source']
alternate['sourceSha256']=hashlib.sha256(alternate['source'].encode()).hexdigest()
fixture=json.loads(alternate['fixtureJson'])
with tempfile.TemporaryDirectory(prefix='lython-b120-golden-') as folder:
    folder=pathlib.Path(folder)
    for name,data in fixture['inputFiles'].items():(folder/name).write_bytes(base64.b64decode(data))
    r=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',alternate['source']],cwd=folder,capture_output=True,check=True,timeout=10)
    assert not r.stderr and r.stdout.decode().replace('\r\n','\n')==alternate['expectedOutput']
    for name,data in fixture['outputFiles'].items():assert (folder/name).read_bytes()==base64.b64decode(data)
selected={'scripts.csv-summary-helpers.large','scripts.text-summary-helpers.large','scripts.json-summary-helpers.large'}
report_catalog['cases']=[report,alternate]+[c for c in prior['cases'] if c['id'] in selected]
(root/'catalog.json').write_text(json.dumps(report_catalog,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
old=root.parent/'bulk-ascii-casing-b118'
text=(old/'collect.py').read_text().replace('bulk-ascii-casing-b118-20261102','shared-formatted-output-b120-20261102')
begin=text.index('jobs = [(');end=text.index('env = os.environ.copy()',begin)
text=text[:begin]+'''jobs = [(case,lane) for block in range(2) for case,lane in [
    ('scripts.report-render.large','warm'),('scripts.report-render.large','fresh'),
    ('scripts.report-format-call.large','warm'),('scripts.csv-summary-helpers.large','warm'),
    ('scripts.text-summary-helpers.large','warm'),('scripts.json-summary-helpers.large','warm')]]

'''+text[end:]
text=text.replace('B118 bulk ASCII casing; complete scripts, Unicode/CSV controls; external process CPU through response; no guest-only or allocation claim',
    'B120 shared formatted output; complete report warm/fresh/format-call and CSV/text/JSON controls; external process CPU through response; allocation measured separately')
(root/'collect.py').write_text(text,encoding='utf-8',newline='\n')
shutil.copy2(old/'cpython-worker.py',root/'cpython-worker.py')
shutil.copy2(old/'assess-results.py',root/'assess-results.py')
identity={'sourceRevision':'17511dbf + uncommitted B120 shared formatted output','baselineRuntime':'93ee4513',
    'coreSHA256':{label:hashlib.sha256((root/('candidate' if label=='candidate' else 'baseline')/'Lokad.Lython.dll').read_bytes()).hexdigest()
        for label in ['baseline','candidate','before-control']},
    'boundary':'Existing standalone warm/fresh protocol; actual mediated I/O, staging/full goldens and previous-result cleanup inside batch. Last result/final compiled/process/fixture cleanup outside and joined. Startup excluded; fresh includes compilation/disposal.',
    'cpuMetric':'External Linux/proc TGID user+system through response; weighted nine-batch means; runtime/JIT/response included, not guest-only.'}
(root/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8',newline='\n')
# Reuse existing complete-job allocation diagnostic; ALC clocks are diagnostic only.
jobs=root/'allocation';jobs.mkdir(exist_ok=True)
old_jobs=root.parent/'call-construction-b113/jobs'
shutil.copy2(old_jobs/'complete-jobs.csproj',jobs/'complete-jobs.csproj')
program=(old_jobs/'Program.cs').read_text()
begin=program.index('if (cases.Length != 5');end=program.index('var phase = ',begin)
program=program[:begin]+'''if (cases.Length != 2 || !cases.Select(x => x.GetProperty("id").GetString()).Order().SequenceEqual(
    new[] { "scripts.report-format-call.large", "scripts.report-render.large" }))
    throw new InvalidDataException("Require the same complete report via f-string and format calls.");
'''+program[end:]
(jobs/'Program.cs').write_text(program,encoding='utf-8',newline='\n')
allocation_catalog=copy.deepcopy(report_catalog);allocation_catalog['cases']=[report,alternate]
(root/'allocation-catalog.json').write_text(json.dumps(allocation_catalog,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
shutil.copy2(root.parent/'call-construction-b113/hash-witness.json',root/'hash-witness.json')
