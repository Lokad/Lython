import base64,json,pathlib,subprocess,sys,tempfile
root=pathlib.Path(sys.argv[1]);python=sys.argv[2];records=[]
for case in json.loads((root/'catalog.json').read_text())["cases"]:
 with tempfile.TemporaryDirectory(prefix='lython-comparison-files-ref-') as directory:
  path=pathlib.Path(directory);fixture=json.loads(case['fixtureJson'])
  for name,value in fixture.get('inputFiles',{}).items():
   file=path/name;file.parent.mkdir(parents=True,exist_ok=True);file.write_bytes(base64.b64decode(value))
  result=subprocess.run([python,'-I','-S','-X','utf8','-c',case['source']],cwd=path,stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=5)
  assert result.returncode==0 and not result.stderr,(case['id'],result.stderr)
  assert result.stdout==case['expectedOutput'].encode('utf-8'),case['id']
  for name,value in fixture.get('outputFiles',{}).items():assert (path/name).read_bytes()==base64.b64decode(value),name
  records.append(dict(case=case['id'],completeStdoutAndFileGoldensPassed=True))
(root/'reference-validation.json').write_text(json.dumps(records,indent=2))
