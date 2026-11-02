import json,pathlib,subprocess
root=pathlib.Path(__file__).parent
data=json.loads((root/'semantic-goldens.json').read_text(encoding='utf-8-sig'))
assert len(data)==19
for row in data:
    result=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',row['source']],capture_output=True,timeout=10)
    row['actual']=result.stdout.decode().replace('\r\n','\n')
    row['stderr']=result.stderr.decode()
    row['exitCode']=result.returncode
    row['passed']=result.returncode==0 and not result.stderr and row['actual']==row['expected']
    assert row['passed'],row
(root/'cpython-semantic-results.json').write_text(json.dumps({'reference':'CPython3.13.2','cases':data},indent=2),encoding='utf-8')
print('Independent CPython semantic goldens:',len(data),'passed')
