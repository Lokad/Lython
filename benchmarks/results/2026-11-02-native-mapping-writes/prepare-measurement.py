import hashlib,json,pathlib,shutil
root=pathlib.Path(__file__).parent
old=root.parent/'native-call-boundary-b116'
text=(old/'collect-csv-cpu.py').read_text()
text=text.replace('native-call-boundary-b116-20261102','native-mapping-writes-b117-20261102')
text=text.replace("identity-cpu.json","identity.json").replace("out = root / 'csv-cpu-confirmation'","out = root / 'confirmation'")
text=text.replace("jobs = [('scripts.csv-summary-helpers.large','warm')]*2",'''jobs = [(case,'warm') for block in range(2) for case in [
    'scripts.csv-summary-helpers.large','scripts.text-summary-helpers.large',
    'scripts.json-summary-helpers.large','scripts.native-helper-report.large']]''')
text=text.replace("labels += ['legacy','legacy-control']","if case_id == 'scripts.csv-summary-helpers.large': labels += ['legacy','legacy-control']")
text=text.replace('if job_index >= 1:', 'if job_index >= 4:').replace('block=job_index,','block=job_index//4,')
text=text.replace('B116 CSV-only CPU gap confirmation; external TGID CPU through response, runtime/JIT included; sum9 batches, no guest-only or allocation claim',
    'B117 native mapping writes; two complete source-pair blocks; external process CPU through response; no guest-only or allocation claim')
(root/'collect.py').write_text(text,encoding='utf-8')
for name in ['cpython-worker.py','catalog.json','assess.py']:
    shutil.copy2(old/name,root/name)
# Assessment reads the existing confirmation folder; no second CPU campaign.
legacy=root/'legacy';legacy.mkdir(exist_ok=True)
for path in (root/'baseline').iterdir():
    if path.is_file():shutil.copy2(path,legacy/path.name)
shutil.copy2(old/'legacy/Lokad.Lython.dll',legacy/'Lokad.Lython.dll')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
identity={'sourceRevision':'4411fdae + uncommitted B117 common native store source',
    'baselineRuntime':'4411fdae','legacyRuntime':'bc0d9823; CSV only',
    'coreSHA256':{label:sha(root/('candidate' if label=='candidate' else 'legacy' if label.startswith('legacy') else 'baseline')/'Lokad.Lython.dll')
        for label in ['baseline','candidate','before-control','legacy','legacy-control']},
    'cpuMetric':'External Linux/proc TGID user+system ticks before command/after response, runtime/JIT/response included; weighted9-batch means; not guest-only or allocation measurements.',
    'boundary':'Warm worker batch: fixture reset, actual mediated I/O, full output/file checks, previous-result disposal. Last result/final compiled/process/fixture cleanup outside batch, all joined. Compilation/startup excluded.'}
(root/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8')
