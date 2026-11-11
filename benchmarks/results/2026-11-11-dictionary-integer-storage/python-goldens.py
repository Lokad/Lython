from pathlib import Path
import json,re,subprocess
r=Path(__file__).parent
source=Path('tests/Lokad.Lython.PublicApi.Tests/DictionaryValueLifetimeTests.cs').read_text(encoding='utf-8')
cases=re.findall(r'\{\s*("(?:[^"\\]|\\.)*"),\s*("(?:[^"\\]|\\.)*")\s*\}',source)
assert len(cases)==4
observations=[]
for program,expected in cases:
 program,expected=json.loads(program),json.loads(expected)
 result=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',program],capture_output=True,text=True,encoding='utf-8',timeout=10)
 assert result.returncode==0 and result.stdout==expected,(program,result.stdout,result.stderr,expected)
 observations.append(dict(source=program,stdout=result.stdout,exitCode=result.returncode))
(r/'python-goldens.json').write_text(json.dumps(dict(reference='CPython3.13.2 isolated -I -S, correctness only',observations=observations),indent=2)+'\n',encoding='utf-8')
print('4 independent CPython correctness goldens pass; no timing claim.')
