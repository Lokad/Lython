set -eu
root=/home/lokad/lython-bench-checks/allocation-current-20261101-ae22392d
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
export DOTNET_TieredCompilation=0
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
"$dotnet" "$root/worker/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-before.json"
python3 "$root/profile.py" "$root/eventpipe"
"$dotnet" "$root/worker/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-after.json"
echo 'Fixed allocation capture complete; all workers/collectors joined.'
