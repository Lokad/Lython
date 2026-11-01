set -eu
root=/home/lokad/lython-bench-checks/casing-context-b56-20261101
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
before=/home/lokad/lython-bench-checks/numeric-report-b52-20261101/candidate/Lokad.Lython.Benchmarks.dll
candidate="$root/candidate/Lokad.Lython.Benchmarks.dll"
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
date -u +%FT%TZ > "$root/started.utc"
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
python3 - "$root" "$before" <<'PY'
import hashlib,json,sys
from pathlib import Path
r=Path(sys.argv[1]);i=json.loads((r/'candidate-identity.json').read_text())
for p,h in ((Path(sys.argv[2]).parent/'Lokad.Lython.dll',i['baselineCoreSHA256']),(r/'candidate/Lokad.Lython.dll',i['coreSHA256'])):
 assert hashlib.sha256(p.read_bytes()).hexdigest()==h
PY
"$dotnet" "$candidate" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-before.json"
"$dotnet" "$root/jobs/bin/Release/net11.0/complete-jobs.dll" "$before" "$candidate" "$root/catalog.json" "$root/hash-witness.json" > "$root/comparison.json" 2> "$root/comparison.stderr.txt"
python3 - "$root/comparison.json" <<'PY'
import json,sys
v=json.load(open(sys.argv[1]));assert v['allBalancesZero'] and v['imageIsolationVerified'] and v['allGoldensPassed'] and v['ownedFixturesRemoved']
print('Single current-source comparison complete; all users/output/ownership checks pass.',flush=True)
PY
"$dotnet" "$candidate" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-after.json"
