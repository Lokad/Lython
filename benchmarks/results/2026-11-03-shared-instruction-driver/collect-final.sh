set -eu
root=/home/lokad/lython-bench-checks/shared-instruction-driver-b162-20261103
before="$root/baseline/Lokad.Lython.Benchmarks.dll"
candidate="$root/final-candidate/Lokad.Lython.Benchmarks.dll"
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export TMPDIR=/dev/shm
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
mkdir "$root/final"
"$dotnet" "$before" --compare check-machine --out "$root/final/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/final/machine-before.json"
for api in sync async; do
 mkdir "$root/final/$api"
 cp /home/lokad/lython-bench-checks/prepared-format-layout-b160-20261103/hash-witness.json "$root/final/$api/hash-witness.json"
 "$dotnet" "$root/tool/complete-jobs.dll" "$before" "$candidate" "$root/catalog.json" "$root/final/$api/hash-witness.json" reverse warm "$api" > "$root/final/$api/measurements.json" 2> "$root/final/$api/stderr.txt"
 python3 - "$root/final/$api/measurements.json" <<'CHECK'
import json,sys
v=json.load(open(sys.argv[1]));assert v['allGoldensPassed'] and v['allBalancesZero'] and v['ownedFixturesRemoved'] and v['imageIsolationVerified']
for o in v['observations']:
 assert len({i['Lokad.Lython.dll'] for i in o['identities'].values()})==2
 assert o['identities']['before']==o['identities']['before-control']
CHECK
done
"$dotnet" "$before" --compare check-machine --out "$root/final/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/final/machine-after.json"
