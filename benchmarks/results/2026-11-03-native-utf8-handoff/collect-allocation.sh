set -eu
root=/home/lokad/lython-bench-checks/native-utf8-handoff-b158-20261103
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export TMPDIR=/dev/shm
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
out="$root/allocation-results"
mkdir "$out"
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$out/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$out/machine-before.json"
"$dotnet" "/home/lokad/lython-bench-checks/json-iterator-storage-b154-20261103/allocation-confirmation-tool/complete-jobs.dll" "$root/baseline/Lokad.Lython.Benchmarks.dll" "$root/candidate/Lokad.Lython.Benchmarks.dll" "$root/allocation-catalog.json" "$root/hash-witness.json" reverse warm > "$out/observations.json" 2> "$out/stderr.txt"
python3 - "$out/observations.json" <<'CHECK'
import json,sys
v=json.load(open(sys.argv[1]));assert v['allBalancesZero'] and v['imageIsolationVerified'] and v['allGoldensPassed'] and v['ownedFixturesRemoved']
CHECK
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$out/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$out/machine-after.json"
