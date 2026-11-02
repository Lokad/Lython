from pathlib import Path
import hashlib,json,shutil,subprocess,gzip
r=Path('.git/agent-notes/review-20261031/shared-iteration-b127');t=r/'refined';t.mkdir(exist_ok=False)
for n in ['baseline','candidate']:shutil.copytree(r/n,t/n)
shutil.copytree(r/'allocation-output',t/'allocation')
for n in ['Lokad.Lython.dll','Lokad.Lython.pdb']:shutil.copy2(Path('src/Lokad.Lython/bin/Release/net11.0')/n,t/'candidate'/n)
for n in ['measurement-catalog.json','allocation-catalog.json','hash-witness.json','cpython-worker.py','assess-results.py','assess-allocation.py']:shutil.copy2(r/n,t/n)
for n in ['collect.py','collect-allocation.sh']:
 s=(r/n).read_text(encoding='utf-8').replace('shared-iteration-b127-20261102','shared-iteration-b127-refined-20261102')
 (t/n).write_text(s,encoding='utf-8',newline='\n')
v=json.loads((r/'identity.json').read_text(encoding='utf-8'));v['sourceRevision']='19fc4324/runtime90785737 + B127 shared iteration, cached sync protocol and existing async interface'
v['coreSHA256']['candidate']=hashlib.sha256((t/'candidate/Lokad.Lython.dll').read_bytes()).hexdigest()
(t/'identity.json').write_text(json.dumps(v,indent=2),encoding='utf-8',newline='\n')
paths=['src/Lokad.Lython/Runtime/PyIteration.cs','src/Lokad.Lython/Runtime/PyIteration.Sequences.cs','src/Lokad.Lython/Runtime/PyCoreIterators.cs','src/Lokad.Lython/Runtime/PyUserIterator.cs','src/Lokad.Lython/Runtime/LythonRuntime.Executable.Interpreter.cs','src/Lokad.Lython/Runtime/LythonRuntime.Executable.Operations.cs','tests/Lokad.Lython.Tests/EnumerableStateTests.cs','tests/Lokad.Lython.Tests/CheckedRangeIterationTests.cs','tests/Lokad.Lython.Tests/CursorDispatchTests.cs']
(t/'source.patch.gz').write_bytes(gzip.compress(subprocess.check_output(['git','diff','--',*paths]),mtime=0))
print(v['coreSHA256'])
