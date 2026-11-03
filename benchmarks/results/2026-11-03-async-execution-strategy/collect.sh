set -eu
root=/home/lokad/lython-bench-checks/async-execution-b161-20261103
image=/home/lokad/lython-bench-checks/prepared-format-layout-b160-20261103/candidate/Lokad.Lython.Benchmarks.dll
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export TMPDIR=/dev/shm
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
mkdir "$root/observations"
"$dotnet" "$image" --compare check-machine --out "$root/observations/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/observations/machine-before.json"
"$dotnet" "$root/tool/complete-jobs.dll" "$image" "$image" "$root/catalog.json" /home/lokad/lython-bench-checks/prepared-format-layout-b160-20261103/hash-witness.json reverse warm > "$root/observations/measurements.json" 2> "$root/observations/stderr.txt"
python3 - "$root/observations/measurements.json" <<'CHECK'
import json,sys
v=json.load(open(sys.argv[1]));assert v['allGoldensPassed'] and v['allBalancesZero'] and v['ownedFixturesRemoved'] and v['imageIsolationVerified']
for o in v['observations']:
 assert len({i['Lokad.Lython.dll'] for i in o['identities'].values()})==1
CHECK
"$dotnet" "$image" --compare check-machine --out "$root/observations/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/observations/machine-after.json"
