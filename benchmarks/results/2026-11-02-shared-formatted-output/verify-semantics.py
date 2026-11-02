import json,pathlib,subprocess
root=pathlib.Path(__file__).parent
sources=[
"s=''.join(['é','😀'])\nprint(f'{s}' is s,f'{s:}' is s,format(s,'') is s,format(s,'>1s') is s)\nprint(f'head:{-12345:😀=12,d}|{17:*^7d}|{s:😀^7.1s}:tail')",
"print(f'A{complex(1,-2):😀^16.2f}|{1234.5:🟢=12,.2f}Z')",
"text='é😀'\nclass Result:\n value=f'X{17:*^8d}{text:😀^6.1s}Y'\nprint(Result.value)"]
rows=[]
for source in sources:
    r=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',source],capture_output=True,check=True,timeout=10)
    assert not r.stderr
    expected=r.stdout.decode().replace('\r\n','\n')
    rows.append({'source':source,'expected':expected,'reference':'CPython3.13.2 -I -S -X utf8'})
    print(repr(expected))
(root/'semantic-goldens.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
