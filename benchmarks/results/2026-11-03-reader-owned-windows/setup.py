from pathlib import Path
import shutil,hashlib,json
root=Path('/home/lokad/lython-bench-checks/borrowed-text-windows-b183-20261103')
before=Path('/home/lokad/lython-bench-checks/direct-casing-b178-refined-20261103/candidate')
candidate=root/'candidate'
for p in before.iterdir():
 if p.name=='Lokad.Lython.dll':continue
 if p.name in ['Lokad.Lython.Benchmarks.dll','Lokad.Lython.Benchmarks.deps.json','Lokad.Lython.Benchmarks.runtimeconfig.json']:shutil.copyfile(p,candidate/p.name)
 else:(candidate/p.name).symlink_to(p,target_is_directory=p.is_dir())
assert hashlib.sha256((candidate/'Lokad.Lython.dll').read_bytes()).hexdigest()==json.loads((root/'identity.json').read_text())['coreSHA256']['candidate']
