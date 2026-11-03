set -eu
root=/home/lokad/lython-bench-checks/native-sequence-checkpoint-b172-20261103
before=/home/lokad/lython-bench-checks/native-sequence-operands-b171-20261103/candidate
python3 - "$root" "$before" <<'LINKS'
from pathlib import Path
import sys,shutil,hashlib,json
root,before=map(Path,sys.argv[1:])
for label in ['baseline','candidate']:
 candidate=root/label;candidate.mkdir()
 physical={'Lokad.Lython.dll','Lokad.Lython.Benchmarks.dll','Lokad.Lython.Benchmarks.deps.json','Lokad.Lython.Benchmarks.runtimeconfig.json'}
 for p in before.iterdir():
  if p.name in physical:shutil.copyfile(p,candidate/p.name)
  else:(candidate/p.name).symlink_to(p,target_is_directory=p.is_dir())
 assert hashlib.sha256((candidate/'Lokad.Lython.dll').read_bytes()).hexdigest()==json.loads((root/'identity.json').read_text())['coreSHA256']['candidate']
LINKS
python3 "$root/collect.py"
