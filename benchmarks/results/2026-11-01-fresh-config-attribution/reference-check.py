import base64, hashlib, json, pathlib, subprocess, tempfile

root = pathlib.Path(__file__).resolve().parent
python = pathlib.Path.home()/'lython-bench-toolchains/cpython/3.13.16/bin/python3.13'
records = []
for item in json.loads((root/'catalog.json').read_text())['cases']:
    fixture = json.loads(item['fixtureJson'])
    assert hashlib.sha256(item['source'].encode()).hexdigest() == item['sourceSha256']
    with tempfile.TemporaryDirectory(prefix='lython-comparison-files-') as directory:
        for name, data in fixture['inputFiles'].items():
            target = pathlib.Path(directory)/name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(base64.b64decode(data))
        result = subprocess.run([str(python), '-I', '-X', 'utf8', '-c', item['source']], cwd=directory, capture_output=True, timeout=5)
        assert result.returncode == 0 and not result.stderr, (item['id'], result.stderr)
        assert result.stdout.decode() == item['expectedOutput'], item['id']
        for name, data in fixture['outputFiles'].items():
            assert (pathlib.Path(directory)/name).read_bytes() == base64.b64decode(data), (item['id'],name)
        records.append({'caseId':item['id'], 'sourceSHA256':item['sourceSha256'], 'status':'Equivalent', 'allOutputBytesPassed':True})
(root/'reference-validation.json').write_text(json.dumps({'reference':subprocess.check_output([str(python), '--version'],text=True).strip(), 'records':records},indent=2)+'\n')
