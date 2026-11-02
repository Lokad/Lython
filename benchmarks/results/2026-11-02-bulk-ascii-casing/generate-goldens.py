import hashlib,json,pathlib,subprocess
root=pathlib.Path(__file__).parent
program='''import hashlib,json
cases=[''.join(map(chr,range(128)))*33,'a'*4095+'bCD xY','ßα'+'a'*4094+' Σ/Abİzßﬃ']
methods=['lower','upper','capitalize','title','swapcase']
print(json.dumps([dict(input=text,hashes=[hashlib.sha256(getattr(text,method)().encode()).hexdigest() for method in methods]) for text in cases],ensure_ascii=True))
'''
result=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',program],check=True,capture_output=True,timeout=10)
assert not result.stderr
(root/'cpython-goldens.json').write_bytes(result.stdout)
data=json.loads(result.stdout)
expressions=['string.Concat(Enumerable.Repeat(new string(Enumerable.Range(0, 128).Select(i => (char)i).ToArray()), 33))',
    'new string(\'a\', 4095) + "bCD xY"','"ßα" + new string(\'a\', 4094) + " Σ/Abİzßﬃ"']
for expression,row in zip(expressions,data):
    print('        yield return ['+expression+', new string[] { '+', '.join('"'+h+'"' for h in row['hashes'])+' }];')
