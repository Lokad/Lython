import hashlib,json,pathlib,shutil
root=pathlib.Path(__file__).parent
old=root.parent/'affected-cpython-b115'
text=(old/'collect.py').read_text(encoding='utf-8-sig')
text=text.replace('affected-cpython-b115-20261102','native-call-boundary-b116-20261102')
text=text.replace("catalog = root / 'representative.json'","catalog = root / 'catalog.json'")
start=text.index('jobs = [(case, lane)')
end=text.index('env = os.environ.copy()',start)
text=text[:start]+'''jobs = [(case, "warm") for block in range(2) for case in [
    'scripts.csv-summary-helpers.large','scripts.text-summary-helpers.large',
    'scripts.json-summary-helpers.large','scripts.native-helper-report.large','scripts.file-search.large']]

'''+text[end:]
text=text.replace("str(worker), '--compare', 'worker'", "str(root/('candidate' if label == 'candidate' else 'legacy' if label == 'legacy' else 'baseline')/'Lokad.Lython.Benchmarks.dll'), '--compare', 'worker'")
text=text.replace("identity['files']['Lokad.Lython.dll'].lower()", "identity['coreSHA256'][label].lower()")
text=text.replace('B115 affected CSV/text checkpoint, source609e5de1, new cumulative source, not an optimization source pair',
    'B116 shared native selector source pair; independent processes and two blocks; legacy CSV diagnostic')
text=text.replace('targetBatchSeconds=.2','targetBatchSeconds=.1').replace('math.ceil(count*.2/','math.ceil(count*.1/')
text=text.replace('for case_id,lane in jobs:', 'for job_index,(case_id,lane) in enumerate(jobs):')
text=text.replace("time.monotonic()-started < 200, 'Reserve40 seconds", "time.monotonic()-started < 260, 'Reserve40 seconds")
text=text.replace("workers = [Worker(label,cases[case_id],lane,stack) for label in ['baseline','candidate','CPython']]",
    '''labels=['baseline','candidate','before-control']
            if case_id == 'scripts.csv-summary-helpers.large': labels.append('legacy')
            if job_index >= 5: labels.reverse()
            workers = [Worker(label,cases[case_id],lane,stack) for label in labels]''')
text=text.replace('for offset in range(3):','for offset in range(len(workers)):')
text=text.replace('i=(round+offset)%3 if round%2==0 else (round+3-offset)%3',
    'i=(round+offset)%len(workers) if round%2==0 else (round+len(workers)-offset)%len(workers)')
text=text.replace('observation=dict(case=case_id,lane=lane,', 'observation=dict(block=job_index//5,case=case_id,lane=lane,')
(root/'collect-standalone.py').write_text(text,encoding='utf-8')
shutil.copy2(old/'cpython-worker.py',root/'cpython-worker.py')
legacy=root/'legacy';legacy.mkdir(exist_ok=True)
for path in (root/'baseline').iterdir():
    if path.is_file():shutil.copy2(path,legacy/path.name)
shutil.copy2(root.parent/'call-construction-b113/baseline/Lokad.Lython.dll',legacy/'Lokad.Lython.dll')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
identity={'sourceRevision':'be96e231 + uncommitted B116 shared selector source',
    'baselineRuntime':'609e5de1', 'legacyRuntime':'bc0d9823, only CSV',
    'coreSHA256':{label:sha(root/('candidate' if label=='candidate' else 'legacy' if label=='legacy' else 'baseline')/'Lokad.Lython.dll')
        for label in ['baseline','candidate','before-control','legacy']},
    'measurementCorrection':'Prospectively use existing independent-process protocol/timed4s warmup instead of shared-CoreLib images, because previous controls were unstable. Legacy CSV runs twice in same campaign; one legacy process per block is a diagnostic, not an unchanged legacy control.',
    'boundary':'Existing worker warm boundary: fixture reset, actual host I/O, full output/file checks, previous-result cleanup; last result and final compiled/process/fixture cleanup outside batch, all joined. No compilation/startup timing.'}
(root/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8')
