set -eu
root=/home/lokad/lython-bench-checks/shared-process-control-b55-20261101
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
worker=/home/lokad/lython-bench-checks/numeric-report-b52-20261101/candidate/Lokad.Lython.Benchmarks.dll
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
date -u +%FT%TZ > "$root/started.utc"
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
python3 - "$worker" <<'PY'
import hashlib,sys
from pathlib import Path
p=Path(sys.argv[1]).parent/'Lokad.Lython.dll'
assert hashlib.sha256(p.read_bytes()).hexdigest()=='6e51564be11be5702d215c3e1022fc114832aa0ce1a706a9f699de294203df02'
PY
"$dotnet" "$worker" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-before.json"
"$dotnet" "$root/jobs/bin/Release/net11.0/complete-jobs.dll" "$worker" "$root/catalog.json" "$root/hash-witness.json" > "$root/control.json" 2> "$root/control.stderr.txt"
python3 - "$root/control.json" <<'PY'
import json,sys
v=json.load(open(sys.argv[1]));assert v['bothBalancesZero'] and v['imageIsolationVerified'] and v['allGoldensPassed'] and v['ownedFixturesRemoved']
print('Single shared-process control complete, all image/ownership/output checks pass.',flush=True)
PY
"$dotnet" "$worker" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-after.json"
