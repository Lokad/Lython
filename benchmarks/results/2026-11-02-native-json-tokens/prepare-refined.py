import hashlib,json,pathlib,shutil
root=pathlib.Path(__file__).parent
target=root/'refined'
text=(root/'collect.py').read_text().replace('native-json-tokens-b123-20261102','native-json-tokens-b123-refined-20261102')
begin=text.index('jobs = [(');end=text.index('env = os.environ.copy()',begin)
text=text[:begin]+"jobs = [(case,'warm') for block in range(2) for case in ['scripts.report-render.large','scripts.json-summary-helpers.large']]\n\n"+text[end:]
text=text.replace('if job_index >= 4:','if job_index >= 2:').replace('block=job_index//4,','block=job_index//2,')
text=text.replace('time.monotonic()-started < 180','time.monotonic()-started < 110')
(target/'collect.py').write_text(text,encoding='utf-8',newline='\n')
text=(root/'assess-results.py').read_text().replace("len(data['jobs'])==8","len(data['jobs'])==4")
(target/'assess-results.py').write_text(text,encoding='utf-8',newline='\n')
text=(root/'collect-allocation.sh').read_text().replace('native-json-tokens-b123-20261102','native-json-tokens-b123-refined-20261102')
(target/'collect-allocation.sh').write_text(text,encoding='utf-8',newline='\n')
shutil.copy2(root/'assess-allocation.py',target/'assess-allocation.py')
identity=json.loads((root/'identity.json').read_text())
identity['sourceRevision']='aa6cf3cb + B123 native plain JSON tokens and corrected bounded scan cancellation'
identity['coreSHA256']['candidate']=hashlib.sha256((target/'candidate/Lokad.Lython.dll').read_bytes()).hexdigest()
(target/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8',newline='\n')
