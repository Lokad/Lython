set -eu
root=/home/lokad/lython-bench-checks/activation-storage-b170-20261103
before=/home/lokad/lython-bench-checks/record-key-shape-b166-20261103-refined/candidate
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
export TMPDIR=/dev/shm
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
python3 - "$root/candidate" "$before" <<'LINKS'
from pathlib import Path
import sys,shutil
candidate,before=map(Path,sys.argv[1:]);candidate.mkdir()
physical={'Lokad.Lython.dll','Lokad.Lython.Benchmarks.dll','Lokad.Lython.Benchmarks.deps.json','Lokad.Lython.Benchmarks.runtimeconfig.json'}
for p in before.iterdir():
 if p.name in physical:shutil.copyfile(p,candidate/p.name)
 else:(candidate/p.name).symlink_to(p,target_is_directory=p.is_dir())
LINKS
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-before.json"
python3 "$root/profile.py" /dev/shm/lython-activation-b170-20261103
for case in text-warm report-warm; do
 "$dotnet" "$root/reader/Diagnostics.dll" "/dev/shm/lython-activation-b170-20261103/$case/profile.nettrace" "/dev/shm/lython-activation-b170-20261103/$case/record.json" "$root/$case-allocation.json"
done
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-after.json"
