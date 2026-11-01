set -eu
root=/home/lokad/lython-bench-checks/helper-attribution-b65-20261101
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
worker=/home/lokad/lython-bench-checks/string-windows-b64-20261101/baseline/Lokad.Lython.Benchmarks.dll
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
date -u +%FT%TZ > "$root/started.utc"
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
"$dotnet" "$worker" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-before.json"
python3 "$root/profile.py"
"$dotnet" "$worker" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-after.json"
python3 "$root/native-origins.py" "$root"
