#!/bin/sh
set -eu
root=/home/lokad/lython-bench-checks/boundary-cost-b144-20261103
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
python=/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
mkdir "$root/results"
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/results/machine-before.json"
"$python" -I -S -c 'import json,sys; assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/results/machine-before.json"
"$dotnet" "$root/driver/complete-jobs.dll" "$root/candidate/Lokad.Lython.Benchmarks.dll" "$root/candidate/Lokad.Lython.Benchmarks.dll" "$root/catalog.json" "$root/hash-witness.json" forward warm > "$root/results/observations.json" 2> "$root/results/stderr.txt"
"$python" -I -S -X utf8 "$root/python-components.py"
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/results/machine-after.json"
"$python" -I -S -c 'import json,sys; assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/results/machine-after.json"
