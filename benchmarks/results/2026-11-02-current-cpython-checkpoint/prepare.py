import pathlib,shutil,json,hashlib
r=pathlib.Path('.git/agent-notes/review-20261031/cpython-checkpoint-b125');r.mkdir(exist_ok=False)
old=r.parent/'report-cpython-b121';current=r.parent/'call-frame-source-b124'
shutil.copytree(current/'candidate',r/'candidate')
shutil.copy2(current/'measurement-catalog.json',r/'representative.json')
text=(old/'collect.py').read_text().replace('report-cpython-b121-20261102','cpython-checkpoint-b125-20261102')
a=text.index('jobs = [(');b=text.index('env = os.environ.copy()',a)
names=['scripts.csv-summary-helpers.large','scripts.json-summary-helpers.large','scripts.text-summary-helpers.large','scripts.file-search.large','scripts.report-render.large']
jobs=[(name,lane) for name in names for lane in ['compile-run','warm']]+[(name,'compile') for name in [names[0],names[-1]]]
text=text[:a]+'jobs = '+repr(jobs)+'\n\n'+text[b:]
text=text.replace('time.monotonic()-started < 200','time.monotonic()-started < 250')
text=text.replace('B121 affected report checkpoint, sourcec3fa26cc, fresh/warm/compile-only; no inference of source speedup from independent medians','B125 coherent five-workload checkpoint source90785737; fresh/warm plus CSV/report compile-only, no source speedup inference from independent medians')
(r/'collect.py').write_text(text,encoding='utf-8',newline='\n')
shutil.copy2(old/'cpython-worker.py',r/'cpython-worker.py')
text=(old/'assess.py').read_text().replace("len(data['jobs'])==3","len(data['jobs'])==12")
(r/'assess.py').write_text(text,encoding='utf-8',newline='\n')
identity={'sourceRevision':'9078573794366fea4332caa7669192dda3724752','files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (r/'candidate').iterdir() if p.is_file()}}
(r/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8',newline='\n')
print('Prepared twelve jobs; no new framework, unchanged protocol and complete goldens')
