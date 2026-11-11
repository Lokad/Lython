from pathlib import Path
import hashlib,json,re,subprocess,textwrap
r=Path(__file__).parent;t=Path('tests/Lokad.Lython.PublicApi.Tests/ReturnedIterationTests.cs').read_text(encoding='utf-8')
section=t[t.index('PythonGoldens => new()'):t.index('    };')];rows=[]
for line in section.splitlines():
 line=line.strip()
 if not line.startswith('{ "'):continue
 if 'NativeSources'in line:
  source=textwrap.dedent(re.search(r'private const string NativeSources = """\n(.*?)\n        """;',t,re.S).group(1))
  tail=line.split('NativeSources, ',1)[1].rsplit(' }',1)[0];expected=json.loads(tail)
  name='nested-native-and-string-sources'
 else:
  name,source,expected=json.loads('['+line[1:line.rindex('}')]+']')
 done=subprocess.run(['C:/Python313/python.exe','-I','-S','-X','utf8','-c',source],capture_output=True,text=True,encoding='utf-8',timeout=10)
 assert done.returncode==0 and not done.stderr and done.stdout==expected,(name,done.stdout,done.stderr,expected)
 rows.append(dict(name=name,source=source,expected=expected,actual=done.stdout,sourceSHA256=hashlib.sha256(source.encode()).hexdigest(),exitCode=done.returncode))
version=subprocess.check_output(['C:/Python313/python.exe','--version'],text=True).strip()
(r/'python-goldens.json').write_text(json.dumps(dict(reference=version,isolatedNoSite=True,cases=rows),indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(len(rows),'independent',version,'goldens match, including nested native/ordinary sources and protected returns.')
