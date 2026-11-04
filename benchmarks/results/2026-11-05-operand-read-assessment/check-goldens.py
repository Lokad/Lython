from pathlib import Path
import json,re,subprocess
root=Path(__file__).resolve().parent;repo=Path('D:/code/Lython')
cases=[]
source=(repo/'tests/Lokad.Lython.PublicApi.Tests/OperandReadFailureTests.cs').read_text(encoding='utf-8-sig')
for match in re.finditer(r'\[InlineData\("((?:\\.|[^"\\])*)", "([^"]+)", "([^"]+)"\)\]',source):
    code=json.loads('"'+match[1]+'"')
    program="import json\nsource="+repr(code)+"\ntry:exec(compile(source,'guest.py','exec'))\nexcept BaseException as e:print(json.dumps([type(e).__name__,str(e)],ensure_ascii=False))\n"
    result=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',program],capture_output=True,text=True,encoding='utf-8',check=True)
    value=json.loads(result.stdout)
    assert value[0]==match[3] and "'"+match[2]+"'" in value[1] and not result.stderr
    cases.append(dict(source=code,name=match[2],exceptionType=value[0],message=value[1],noProtocolOutput=True))
assert len(cases)==7
(root/'cpython-error-goldens.json').write_text(json.dumps(dict(version=subprocess.check_output(['C:/Python313/python.exe','--version'],text=True).strip(),cases=cases),indent=2)+'\n',encoding='utf-8')
print('Seven failure-order goldens independently match isolated CPython3.13.2; no protocol output.')
