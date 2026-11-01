set -eu
root=/home/lokad/lython-bench-checks/file-search-b79-20261102
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
date -u +%FT%TZ > "$root/started.utc"
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
"$dotnet" "$root/worker/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-before.json"
python3 "$root/reference-check.py" "$root" /home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13
"$dotnet" "$root/phases/bin/Release/net11.0/Diagnostics.dll" "$root/worker/Lokad.Lython.Benchmarks.dll" "$root/catalog.json" "$root/phases.json" > "$root/phases.stdout" 2> "$root/phases.stderr"
python3 "$root/profile.py" "$root/perf" > "$root/profile.log" 2> "$root/profile.stderr"
"$dotnet" "$root/worker/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-after.json"
