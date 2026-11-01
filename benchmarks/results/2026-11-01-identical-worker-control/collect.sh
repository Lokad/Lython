set -eu
root=/home/lokad/lython-bench-checks/short-control-b54-20261101
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
python=/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13
worker=/home/lokad/lython-bench-checks/numeric-report-b52-20261101/candidate/Lokad.Lython.Benchmarks.dll
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
date -u +%FT%TZ > "$root/started.utc"
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
"$dotnet" "$worker" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-before.json"
env DOTNET_TieredCompilation=0 "$dotnet" "$worker" --compare micro \
    --catalog "$root/catalog.json" --dotnet "$dotnet" --baseline-worker "$worker" \
    --python "$python" --python-worker "$root/cpython-worker.py" \
    --out "$root/micro.json" --case scripts.csv-summary-helpers.large --lane warm > "$root/micro.log" 2>&1
cat "$root/micro.log"
"$dotnet" "$worker" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-after.json"
