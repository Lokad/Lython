from pathlib import Path
import hashlib
import json
import shutil
import subprocess

repo = Path('D:/code/Lython')
root = Path(__file__).parent
prior = root.parent / 'regex-dev67-trusted-r215'
tool = root / 'tool'
tool.mkdir(exist_ok=True)
for name in ['Directory.Build.props', 'complete-jobs.csproj']:
    shutil.copyfile(prior / 'tool' / name, tool / name)
source = (prior / 'tool/Program.cs').read_text(encoding='utf-8')
source = source.replace('kv.Key != "Lokad.Lython.Utf8Regex.dll"', 'kv.Key != "Lokad.Lython.dll"')
source = source.replace('Require six UTF-8 admission/control diagnostics.', 'Require four existing regex diagnostics and two complete-script controls.')
source = source.replace('R215 same-producer trusted-input diagnostic', 'R219 operation-owned projection storage; fixed normal0.4 adapter/producer')
(tool / 'Program.cs').write_text(source, encoding='utf-8', newline='\n')
catalog = json.loads((prior / 'catalog.json').read_text(encoding='utf-8'))
keep = {'diagnostic.utf8.ascii-prefix', 'diagnostic.utf8.unicode-prefix',
        'diagnostic.utf8.unicode-lookbehind', 'diagnostic.utf8.unicode-forward'}
cases = [c for c in catalog['cases'] if c['id'] in keep]
representative = json.loads((root.parent / 'cpython-construction-r209/b561/representative.json').read_text(encoding='utf-8'))
cases += [next(c for c in representative['cases'] if c['id'] == name)
          for name in ['scripts.text-summary-helpers.large', 'scripts.csv-validated-records.large']]
assert len(cases) == 6
catalog['cases'] = cases
(root / 'catalog.json').write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
auxiliary = repo / 'benchmarks/Lokad.Lython.Benchmarks/bin/Release/net11.0'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
for role in ['before', 'candidate']:
    destination = root / 'images' / role
    destination.mkdir(parents=True)
    for path in auxiliary.iterdir():
        if path.is_file() and path.suffix != '.pdb':
            shutil.copyfile(path, destination / path.name)
    core = root / 'baseline/net11.0/Lokad.Lython.dll' if role == 'before' else repo / 'src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll'
    shutil.copyfile(core, destination / core.name)
images = {role: {p.name: sha(p) for p in (root / 'images' / role).iterdir() if p.is_file()}
          for role in ['before', 'candidate']}
assert [name for name in images['before'] if images['before'][name] != images['candidate'][name]] == ['Lokad.Lython.dll']
identity = dict(images=images, base=json.loads((root / 'before.json').read_text(encoding='utf-8')),
    currentSource={name: sha(repo / name) for name in ['src/Lokad.Lython/Runtime/LythonRuntime.RegexMatcher.cs', 'src/Lokad.Lython/Runtime/LythonRuntime.RegexObjects.cs']},
    normalAdapter='0.4.0 unchanged; this is a core source pair, not newer producer qualification',
    boundary='Existing public complete-job driver: compilation separate; warm includes fixture reset, mediated file I/O where present, full output checks and result disposal. Execution-only clocks retained; desktop elapsed/CPU diagnostic. Same-process ALCs share CoreLib/JIT/GC.')
(root / 'diagnostic-identity.json').write_text(json.dumps(identity, indent=2) + '\n', encoding='utf-8')
(root / 'measured-source.patch').write_bytes(subprocess.check_output(['git', 'diff', '--', 'src'], cwd=repo))
for phase in ['warm-sync', 'warm-async', 'compile']:
    destination = root / phase
    destination.mkdir()
    shutil.copyfile(prior / 'confirmation/hash-witness.json', destination / 'hash-witness.json')
print('Prepared six cases and isolated core-only source pair; no diagnostic has run yet.')
