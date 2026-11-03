set -eu
root=/home/lokad/lython-bench-checks/native-sequence-operands-b171-20261103-report
before=/home/lokad/lython-bench-checks/native-sequence-operands-b171-20261103/candidate/Lokad.Lython.Benchmarks.dll
candidate=/home/lokad/lython-bench-checks/record-key-shape-b166-20261103-refined/candidate/Lokad.Lython.Benchmarks.dll
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
export TMPDIR=/dev/shm
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
mkdir "$root/observations"
"$dotnet" "$before" --compare check-machine --out "$root/observations/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/observations/machine-before.json"
for api in sync async; do
 mkdir "$root/observations/$api"
 cp /home/lokad/lython-bench-checks/prepared-format-layout-b160-20261103/hash-witness.json "$root/observations/$api/hash-witness.json"
 "$dotnet" "$root/tool/complete-jobs.dll" "$before" "$candidate" "$root/catalog.json" "$root/observations/$api/hash-witness.json" reverse warm "$api" > "$root/observations/$api/measurements.json" 2> "$root/observations/$api/stderr.txt"
done
"$dotnet" "$before" --compare check-machine --out "$root/observations/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/observations/machine-after.json"
