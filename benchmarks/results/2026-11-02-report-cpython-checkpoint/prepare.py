import hashlib,json,pathlib,shutil
root=pathlib.Path(__file__).parent
prior=root.parent/'affected-cpython-b115'
current=root.parent/'shared-formatted-output-b120'
shutil.copytree(current/'candidate',root/'candidate',dirs_exist_ok=True)
catalog=json.loads((current/'catalog.json').read_text(encoding='utf-8-sig'))
catalog['cases']=[c for c in catalog['cases'] if c['id']=='scripts.report-render.large']
(root/'representative.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
text=(prior/'collect.py').read_text().replace('affected-cpython-b115-20261102','report-cpython-b121-20261102')
start=text.index('jobs = [(');end=text.index('env = os.environ.copy()',start)
text=text[:start]+"jobs = [('scripts.report-render.large',lane) for lane in ['compile-run','warm','compile']]\n\n"+text[end:]
text=text.replace("        assert response['actualOutputSha256'] == self.case['expectedOutputSha256']\n        assert response['actualFilesSha256'] == self.case['expectedFilesSha256']", """        if self.lane == 'compile' and operation == 'batch':
            assert response['actualOutputSha256'] is None
            assert response.get('actualFilesSha256') is None
        else:
            assert response['actualOutputSha256'] == self.case['expectedOutputSha256']
            assert response['actualFilesSha256'] == self.case['expectedFilesSha256']""")
text=text.replace('B115 affected CSV/text checkpoint, source609e5de1, new cumulative source, not an optimization source pair',
    'B121 affected report checkpoint, sourcec3fa26cc, fresh/warm/compile-only; no inference of source speedup from independent medians')
(root/'collect.py').write_text(text,encoding='utf-8',newline='\n')
shutil.copy2(prior/'cpython-worker.py',root/'cpython-worker.py')
text=(prior/'assess.py').read_text().replace("len(data['jobs'])==4","len(data['jobs'])==3")
(root/'assess.py').write_text(text,encoding='utf-8',newline='\n')
identity={'sourceRevision':'c3fa26cc','files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest()
    for p in (root/'candidate').iterdir() if p.is_file()}}
(root/'identity.json').write_text(json.dumps(identity,indent=2),encoding='utf-8',newline='\n')
