from pathlib import Path
import hashlib
import json
import shutil
import subprocess

repo = Path('D:/code/Lython')
root = Path(__file__).parent
prior = root.parent / 'regex-projection-storage-r219'
tool = root / 'tool'
tool.mkdir()
for name in ['Directory.Build.props', 'complete-jobs.csproj']:
    shutil.copyfile(prior / 'tool' / name, tool / name)
source = (prior / 'tool/Program.cs').read_text(encoding='utf-8')
source = source.replace('Require four existing regex diagnostics and two complete-script controls.', 'Require three whole-script lexical-store variants and three unchanged controls.')
source = source.replace('R219 operation-owned projection storage; fixed normal0.4 adapter/producer', 'R221 owner-aware incoming lexical stores; identical complete scripts and fixed common images')
(tool / 'Program.cs').write_text(source, encoding='utf-8', newline='\n')
shutil.copyfile(prior / 'summarize.py', root / 'summarize.py')
destination = root / 'images/candidate'
destination.mkdir()
for path in (root / 'images/before').iterdir():
    if path.is_file():
        shutil.copyfile(path, destination / path.name)
shutil.copyfile(repo / 'src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll', destination / 'Lokad.Lython.dll')
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
images = {role: {p.name: sha(p) for p in (root / 'images' / role).iterdir() if p.is_file()}
          for role in ['before', 'candidate']}
assert [n for n in images['before'] if images['before'][n] != images['candidate'][n]] == ['Lokad.Lython.dll']
paths = subprocess.check_output(['git', 'ls-files', 'src/Lokad.Lython'], cwd=repo, text=True).splitlines()
identity = dict(before=json.loads((root / 'before.json').read_text(encoding='utf-8')), images=images,
    measuredSource={p: hashlib.sha256((repo / p).read_bytes().replace(b'\r\n', b'\n')).hexdigest()
                    for p in paths if p.endswith('.cs')},
    qualification='Initial86internal/142public checks pass actual11. Final broader focused checks tracked separately; no platform/package claim.',
    boundary='Existing public complete-job driver, three identical counter variants and three unchanged scripts. Compile separate; warm includes fixture reset, mediated I/O, full goldens and result disposal. Desktop elapsed/coarse CPU diagnostic; allocation distinct; normalGC/tiering.')
(root / 'diagnostic-identity.json').write_text(json.dumps(identity, indent=2) + '\n', encoding='utf-8')
(root / 'measured-source.patch').write_bytes(subprocess.check_output(['git', 'diff', '--', 'src'], cwd=repo))
for phase in ['warm-sync', 'warm-async', 'compile']:
    target = root / phase
    target.mkdir()
    shutil.copyfile(prior / 'warm-sync/hash-witness.json', target / 'hash-witness.json')
print('Prepared exact baseline/candidate core pair; six whole scripts, fixed auxiliary images, no timings started.')
