set -eu
root=/home/lokad/lython-bench-checks/shared-instruction-driver-b162-20261103
before="$root/baseline/Lokad.Lython.Benchmarks.dll"
candidate="$root/retained-candidate/Lokad.Lython.Benchmarks.dll"
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export TMPDIR=/dev/shm
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
mkdir "$root/retained"
"$dotnet" "$before" --compare check-machine --out "$root/retained/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/retained/machine-before.json"
for api in sync async; do
 mkdir "$root/retained/$api"
 cp /home/lokad/lython-bench-checks/prepared-format-layout-b160-20261103/hash-witness.json "$root/retained/$api/hash-witness.json"
 "$dotnet" "$root/tool/complete-jobs.dll" "$before" "$candidate" "$root/catalog.json" "$root/retained/$api/hash-witness.json" reverse warm "$api" > "$root/retained/$api/measurements.json" 2> "$root/retained/$api/stderr.txt"
 python3 - "$root/retained/$api/measurements.json" <<'CHECK'
import json,sys
v=json.load(open(sys.argv[1]));assert v['allGoldensPassed'] and v['allBalancesZero'] and v['ownedFixturesRemoved'] and v['imageIsolationVerified']
for o in v['observations']:
 assert len({i['Lokad.Lython.dll'] for i in o['identities'].values()})==2
 assert o['identities']['before']==o['identities']['before-control']
CHECK
done
"$dotnet" "$before" --compare check-machine --out "$root/retained/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/retained/machine-after.json"
