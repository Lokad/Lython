from pathlib import Path
import hashlib,json,shutil,subprocess

root=Path(__file__).resolve().parent;repo=Path('D:/code/Lython')
assert subprocess.check_output(['git','status','--porcelain'],cwd=repo,text=True)==''
identity={'revision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip()}
names=subprocess.check_output(['git','ls-files','src/Lokad.Lython'],cwd=repo,text=True).splitlines()
identity['normalizedSource']={n:hashlib.sha256((repo/n).read_bytes().replace(b'\r\n',b'\n')).hexdigest()for n in names if n.endswith('.cs')}
identity['coreSHA256']={}
for framework in ['net11.0','net10.0']:
    source=repo/f'benchmarks/Lokad.Lython.Benchmarks/bin/Release/{framework}'
    shutil.copytree(source,root/'baseline'/framework)
    identity['coreSHA256'][framework]=hashlib.sha256((source/'Lokad.Lython.dll').read_bytes()).hexdigest()
for name in ['src/Lokad.Lython/Runtime/LythonRuntime.Json.Deserialize.cs','src/Lokad.Lython/Runtime/LythonRuntime.Json.Options.cs','tests/Lokad.Lython.Tests/JsonStringTokenStorageTests.cs']:
    target=root/'source-before'/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(repo/name,target)
identity['boundary']='Qualified runtime69644713, evidence b8b05ccc; forced full Release build completed before freezing. Actual loaded0.4 adapter and benchmark host retained; no parallel build/test/timing.'
(root/'before.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
docs=['"plain"','"café😀\\n\\ud83d\\ude00"','"bad\\q"','"\\u12g4"','"\\u123"','"unterminated','"end\\','"\\ud800"','"\\udc00"','"\\ud800x"','"\\ud800\\u0041"','"a\x00b"']
programs=[f"import json\ntry:\n value=json.loads({doc!r})\n print(json.dumps(value,ensure_ascii=True))\nexcept Exception as error:\n print(type(error).__name__,str(error))\n"for doc in docs]
command=['C:/Users/JoannesVermorel/.dotnet11/dotnet.exe',str(repo/'tools/LythonProbe/bin/Release/net11.0/LythonProbe.dll'),'--batch-json','--compare-python','--json']
run=subprocess.run(command,input=json.dumps(programs,ensure_ascii=False),cwd=repo,text=True,encoding='utf-8',capture_output=True)
records=[json.loads(line)for line in run.stdout.splitlines()if line.strip()]
assert len(records)==len(programs),(run.returncode,run.stderr)
(root/'baseline-probes.json').write_text(json.dumps(dict(documents=docs,programs=programs,exitCode=run.returncode,records=records,stderr=run.stderr),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Frozen',len(identity['normalizedSource']),'production sources, both targets and original boundary probes.',flush=True)
for doc,row in zip(docs,records):print(ascii(doc),row['Lython']['StandardOutput'],row['Lython']['Failure'],flush=True)
