set -eu
root=/home/lokad/lython-bench-checks/completed-fault-b167-20261103
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/copyhook-machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/copyhook-machine-before.json"
python3 "$root/check-copyhook.py"
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/copyhook-machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/copyhook-machine-after.json"
flock -u 9
sh "$root/collect-normal.sh"
