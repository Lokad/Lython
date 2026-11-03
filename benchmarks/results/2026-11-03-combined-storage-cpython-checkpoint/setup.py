from pathlib import Path
import shutil,hashlib,json
root=Path('/home/lokad/lython-bench-checks/combined-storage-checkpoint-b184-20261103')
aux=Path('/home/lokad/lython-bench-checks/borrowed-text-windows-b183-20261103/candidate')
before=Path('/home/lokad/lython-bench-checks/record-key-shape-b166-20261103-refined/candidate')
identity=json.loads((root/'identity.json').read_text())
physical={'Lokad.Lython.dll','Lokad.Lython.Benchmarks.dll','Lokad.Lython.Benchmarks.deps.json','Lokad.Lython.Benchmarks.runtimeconfig.json'}
for label,core,role in [('candidate',aux,'candidate'),('baseline',before,'before-control')]:
 dest=root/label;dest.mkdir()
 for p in aux.iterdir():
  if p.name in physical:shutil.copyfile(core/p.name if p.name=='Lokad.Lython.dll' else p,dest/p.name)
  else:(dest/p.name).symlink_to(p,target_is_directory=p.is_dir())
 assert hashlib.sha256((dest/'Lokad.Lython.dll').read_bytes()).hexdigest()==identity['coreSHA256'][role]
