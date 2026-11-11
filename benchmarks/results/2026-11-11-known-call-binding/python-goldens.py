from pathlib import Path
import ast,base64,hashlib,json,re,subprocess,tempfile,textwrap
r=Path(__file__).parent
source=Path('tests/Lokad.Lython.PublicApi.Tests/PreparedFunctionBindingTests.cs').read_text(encoding='utf-8')
pairs=re.findall(r'\{ ("(?:[^"\\]|\\.)*"), ("(?:[^"\\]|\\.)*") \}',source)
assert len(pairs)==11
rows=[]
for index,(program,expected)in enumerate(pairs):
 p=ast.literal_eval(program);e=ast.literal_eval(expected)
 result=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',p],capture_output=True,text=True,encoding='utf-8',timeout=10)
 assert result.returncode==0 and result.stdout==e,(index,result.stdout,result.stderr,e)
 rows.append(dict(case=index,sourceSHA256=hashlib.sha256(p.encode()).hexdigest(),stdout=result.stdout,version='CPython3.13.2',exitCode=result.returncode))
for width in [16,17,65]:
 params=','.join(f'p{i}={i}'for i in range(width))
 p=f"def pick({params}): return p{width-1}\npayload=['kept']\nvalue=pick(p{width-1}=payload)\npayload=None\nprint(value)"
 result=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',p],capture_output=True,text=True,encoding='utf-8',timeout=10)
 assert result.returncode==0 and result.stdout=="['kept']\n",(width,result.stdout,result.stderr)
 rows.append(dict(width=width,sourceSHA256=hashlib.sha256(p.encode()).hexdigest(),stdout=result.stdout,version='CPython3.13.2',exitCode=result.returncode))
io=textwrap.dedent(re.search(r'const string source = """\n(.*?)\n\s*""";',source,re.S)[1])
io=io.replace('return keep(value=payload)','result=keep(value=payload)\nprint(len(result[0]))')
with tempfile.TemporaryDirectory(prefix='lython-r244-golden-')as d:
 out=Path(d);(out/'events.txt').write_text('before',encoding='utf-8')
 result=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',io],cwd=out,capture_output=True,text=True,encoding='utf-8',timeout=10)
 assert result.returncode==0 and result.stdout=='8192\n'and(out/'events.txt').read_text(encoding='utf-8')=='beforecalled',(result.stdout,result.stderr)
 rows.append(dict(sourceSHA256=hashlib.sha256(io.encode()).hexdigest(),stdout=result.stdout,file='beforecalled',version='CPython3.13.2',exitCode=0))
(r/'python-goldens.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
print('Eleven source goldens, width16/17/65 and I/O independently match CPython3.13.2; correctness only.')
