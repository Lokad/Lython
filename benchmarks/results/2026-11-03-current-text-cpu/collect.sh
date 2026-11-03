set -eu
root=/home/lokad/lython-bench-checks/current-text-cpu-b146-20261103
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-before.json"
python3 "$root/ops-profile.py" "$root/perf"
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-after.json"
for case in text-warm; do
 python3 "$root/analyze-cpu-guest.py" "$root/perf/$case"
done
python3 "$root/ops-chains-guest.py"
echo 'Fixed synchronous CPU capture and offline derivation complete; all children joined.'

python3 "$root/native-origins.py" "$root"
