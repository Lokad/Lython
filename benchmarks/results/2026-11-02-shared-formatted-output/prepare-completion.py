import pathlib
root=pathlib.Path(__file__).parent
text=(root/'collect.py').read_text()
text=text.replace("out = root / 'confirmation'", "out = root / 'confirmation-completion'")
text=text.replace("('scripts.report-render.large','fresh')","('scripts.report-render.large','compile-run')")
text=text.replace("    save()\n    for job_index",'''    prefix_path = root/'confirmation/confirmation.json'
    prefix = json.loads(prefix_path.read_text())
    assert not prefix['complete'] and len(prefix['jobs']) == 1
    assert prefix['jobs'][0]['case'] == 'scripts.report-render.large'
    assert prefix['jobs'][0]['lane'] == 'warm' and prefix['jobs'][0]['workersJoinedAndFixturesRemoved']
    record['jobs'] = prefix['jobs']
    record['recoveredPrefix'] = dict(path=str(prefix_path),sha256=hashlib.sha256(prefix_path.read_bytes()).hexdigest(),
        reason='Supervisor used fresh instead of existing compile-run protocol lane; keep completed warm job unchanged.')
    save()
    for job_index''')
text=text.replace('    for job_index,(case_id,lane) in enumerate(jobs):\n',
    '    for job_index,(case_id,lane) in enumerate(jobs):\n        if job_index == 0: continue\n')
(root/'collect-completion.py').write_text(text,encoding='utf-8',newline='\n')
