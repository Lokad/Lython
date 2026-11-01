set -eu
root=/home/lokad/lython-bench-checks/local-updates-b72-20261101
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
date -u +%FT%TZ > "$root/started.utc"
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
python3 - "$root" <<'CHECK'
import hashlib,json,sys
from pathlib import Path
r=Path(sys.argv[1]);i=json.loads((r/'candidate-identity.json').read_text())
for label,key in [('baseline','baselineCoreSHA256'),('candidate','coreSHA256')]:
 p=r/label/'Lokad.Lython.dll'
 assert hashlib.sha256(p.read_bytes()).hexdigest()==i[key]
for name,expected in i['baselineFiles'].items():
 assert hashlib.sha256((r/'baseline'/name).read_bytes()).hexdigest()==expected
 if name!='Lokad.Lython.dll':assert hashlib.sha256((r/'candidate'/name).read_bytes()).hexdigest()==expected
CHECK
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-before.json"
"$dotnet" "$root/jobs/bin/Release/net11.0/complete-jobs.dll" "$root/baseline/Lokad.Lython.Benchmarks.dll" "$root/candidate/Lokad.Lython.Benchmarks.dll" "$root/catalog.json" "$root/hash-witness.json" > "$root/comparison.json" 2> "$root/comparison.stderr.txt"
python3 - "$root/comparison.json" <<'CHECK'
import json,sys
v=json.load(open(sys.argv[1]));assert v['allBalancesZero'] and v['imageIsolationVerified'] and v['allGoldensPassed'] and v['ownedFixturesRemoved']
CHECK
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-after.json"
