set -eu
root=/dev/shm/lython-allowance-gate-b177-20261103
before=/home/lokad/lython-bench-checks/terminal-utf8-storage-b174-20261103/candidate/Lokad.Lython.Benchmarks.dll
candidate="$root/candidate/Lokad.Lython.Benchmarks.dll"
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export TMPDIR=/dev/shm
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
python3 - "$root/candidate" <<'LINKS'
from pathlib import Path
import sys,hashlib,json,shutil
candidate=Path(sys.argv[1])
before=Path('/home/lokad/lython-bench-checks/terminal-utf8-storage-b174-20261103/candidate')
for p in before.iterdir():
 if p.name=='Lokad.Lython.dll': continue
 if p.name in ['Lokad.Lython.Benchmarks.dll','Lokad.Lython.Benchmarks.deps.json','Lokad.Lython.Benchmarks.runtimeconfig.json']: shutil.copyfile(p,candidate/p.name)
 else: (candidate/p.name).symlink_to(p,target_is_directory=p.is_dir())
identity=json.loads((candidate.parent/'identity.json').read_text())
assert hashlib.sha256((before/'Lokad.Lython.dll').read_bytes()).hexdigest()==identity['baselineCoreSHA256']
assert hashlib.sha256((candidate/'Lokad.Lython.dll').read_bytes()).hexdigest()==identity['candidateCoreSHA256']
LINKS
mkdir "$root/observations"
"$dotnet" "$before" --compare check-machine --out "$root/observations/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/observations/machine-before.json"
for api in sync async; do
 mkdir "$root/observations/$api"
 cp /home/lokad/lython-bench-checks/prepared-format-layout-b160-20261103/hash-witness.json "$root/observations/$api/hash-witness.json"
 "$dotnet" "$root/tool/complete-jobs.dll" "$before" "$candidate" "$root/catalog.json" "$root/observations/$api/hash-witness.json" reverse warm "$api" > "$root/observations/$api/measurements.json" 2> "$root/observations/$api/stderr.txt"
 python3 - "$root/observations/$api/measurements.json" <<'CHECK'
import json,sys
v=json.load(open(sys.argv[1]));assert all(v[k] for k in ['allGoldensPassed','allBalancesZero','ownedFixturesRemoved','imageIsolationVerified'])
for o in v['observations']:
 assert len({i['Lokad.Lython.dll'] for i in o['identities'].values()})==2
 assert o['identities']['before']==o['identities']['before-control']
CHECK
done
"$dotnet" "$before" --compare check-machine --out "$root/observations/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/observations/machine-after.json"
