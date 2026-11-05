from pathlib import Path
import hashlib,json,shutil,subprocess,tarfile

repo=Path('D:/code/Lython')
root=Path(__file__).resolve().parent
old=root.parent/'cased-whitespace-b239'
remote='/home/lokad/lython-bench-checks/json-string-runs-b244-20261105'
unit='lython-b244-json-string-runs-20261105'
(root/'jobs').mkdir(exist_ok=True)
(root/'candidate').mkdir(exist_ok=True)
catalog=json.loads((old/'catalog.json').read_text())
sources={c['id']:c for c in catalog['cases']}
selected=[]
for name,mode in [('scripts.report-render.large','warm'),('scripts.report-render.large','async'),
                  ('scripts.report-render.large','fresh'),('scripts.json-summary-helpers.large','warm'),
                  ('scripts.text-summary-helpers.large','warm'),('scripts.report-render.large','compile')]:
    case=dict(sources[name]);case['mode']=mode;selected.append(case)
catalog['cases']=selected
(root/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False)+'\n',encoding='utf-8')
program=(old/'jobs/Program.cs').read_text()
start=program.index('if (cases.Length != 6')
end=program.index('var observations',start)
expected=',\n'.join('        '+json.dumps(c['id']+'/'+c['mode']) for c in sorted(selected,key=lambda c:c['id']+'/'+c['mode']))
program=program[:start]+'''if (cases.Length != 6 || !cases.Select(x=>x.GetProperty("id").GetString()+"/"+x.GetProperty("mode").GetString()).Order(StringComparer.Ordinal).SequenceEqual(new[] {
'''+expected+''' }))
    throw new InvalidDataException("Only six frozen whole JSON byte-run/control jobs are allowed.");
'''+program[end:]
program=program.replace('Complete native cased-whitespace pipeline versus f7fed42c/parser; text warm/async/fresh/compile and JSON/report controls',
                        'Complete JSON native byte-run reader/encoder versus cf3dc9cd; report warm/async/fresh/compile, JSON warm and text control')
(root/'jobs/Program.cs').write_text(program,encoding='utf-8')
shutil.copyfile(old/'jobs/complete-jobs.csproj',root/'jobs/complete-jobs.csproj')
for name in ['hash-witness.json','reference-check.py','assess.py']:
    shutil.copyfile(old/name,root/name)
assess=(root/'assess.py').read_text().replace('Whole cased-whitespace pipeline versus f7fed42c/parser; six complete-script text/control jobs; allocation/elapsed/compile separated, peaks initial untimed warm setup only',
    'Whole JSON byte-run reader/encoder versus cf3dc9cd; six complete-script report/JSON/text-control jobs; allocation/elapsed/compile separated, peaks initial untimed warm setup only')
(root/'assess.py').write_text(assess,encoding='utf-8')
source='src/Lokad.Lython/Runtime/LythonRuntime.Json.Serialize.cs'
files=['src/Lokad.Lython/Runtime/LythonRuntime.Json.Deserialize.cs','tests/Lokad.Lython.Tests/JsonStringTokenStorageTests.cs','tests/Lokad.Lython.PublicApi.Tests/JsonStringTokenBoundaryTests.cs',source,'tests/Lokad.Lython.Tests/JsonStringEmissionAdmissionTests.cs','tests/Lokad.Lython.PublicApi.Tests/JsonStringEmissionTests.cs']
core=repo/'src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll'
shutil.copyfile(core,root/'candidate/Lokad.Lython.dll')
identity=json.loads((old/'candidate-identity.json').read_text())
identity['files']['Lokad.Lython.dll']=identity['candidateCoreSHA256']
identity.update(sourceRevision='Baseline cf3dc9cd/43fdf658, measured current checkpoint; candidate parent787348c8 plus source.patch',
    candidateCoreSHA256=hashlib.sha256(core.read_bytes()).hexdigest(),
    candidateSource='Existing JSON reader/encoder scan ordinary native bytes in managed runs, retaining strict Unicode/token/escape/admission/cancellation paths',
    candidateSourceFiles={f:hashlib.sha256((repo/f).read_bytes()).hexdigest() for f in files})
(root/'candidate-identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
(root/'source.patch').write_bytes(subprocess.check_output(['git','diff','--binary','HEAD','--',*files],cwd=repo))
with tarfile.open(root/'source-snapshot.tgz','w:gz') as archive:
    for f in files:archive.add(repo/f,arcname=f)
shutil.copyfile(root/'plan.md',root/'pre-measurement-plan.md')
setup=(old/'setup.py').read_text().replace('/home/lokad/lython-bench-checks/cased-whitespace-b239-20261105',remote).replace(
    '/home/lokad/lython-bench-checks/parser-reassessment-b238-20261105/candidate',
    '/home/lokad/lython-bench-checks/cased-whitespace-b239-20261105/candidate')
(root/'setup.py').write_text(setup,encoding='utf-8')
for name in ['collect.sh','launch.py','archive.py']:
    text=(old/name).read_text().replace('/home/lokad/lython-bench-checks/cased-whitespace-b239-20261105',remote).replace(
        'lython-b239-cased-whitespace-20261105',unit).replace('b239-payload.tgz','b244-payload.tgz')
    if name=='archive.py':
        text=text.replace('--property=ActiveState,Result,MainPID','--property=LoadState,ActiveState,Result,MainPID')
        text=text.replace("assert (root/'exit-code.txt')", "assert (root/'started.utc').is_file() and (root/'stopped.utc').is_file()\nassert (root/'exit-code.txt')")
    (root/name).write_text(text,encoding='utf-8',newline='\n')
print(json.dumps(dict(candidateCore=identity['candidateCoreSHA256'],cases=[c['id']+'/'+c['mode'] for c in selected])))
