set -eu
root=/home/lokad/lython-bench-checks/invocation-metadata-b148-20261103
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
gate=/home/lokad/lython-bench-checks/current-text-cpu-b146-20261103/candidate/Lokad.Lython.Benchmarks.dll
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
"$dotnet" "$gate" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-before.json"
"$dotnet" "$root/diagnostic/setup.dll" > "$root/setup-first.json"
"$dotnet" "$root/diagnostic/setup.dll" > "$root/setup-confirm.json"
"$dotnet" "$gate" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-after.json"
echo 'Component gate complete; both processes joined, funding returned.'
