set -eu
root=/home/lokad/lython-bench-checks/fresh-config-b68-20261101
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
worker="$root/worker/Lokad.Lython.Benchmarks.dll"
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
date -u +%FT%TZ > "$root/started.utc"
trap 'printf "143\n" > "$root/terminated.txt"; exit 143' TERM INT
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
python3 - "$root" <<'PY'
import hashlib,json,sys
from pathlib import Path
r=Path(sys.argv[1]);h=json.loads((r/'identity.json').read_text())['coreSHA256']
for p in [r/'worker/Lokad.Lython.dll',r/'phases/bin/Release/net11.0/Lokad.Lython.dll']:
 assert hashlib.sha256(p.read_bytes()).hexdigest()==h
PY
"$dotnet" "$worker" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-before.json"
python3 "$root/reference-check.py"
"$dotnet" "$root/phases/bin/Release/net11.0/Diagnostics.dll" "$worker" "$root/catalog.json" "$root/phases.json" > "$root/phases.stdout" 2> "$root/phases.stderr"
python3 "$root/profile.py" "$root/perf"
"$dotnet" "$worker" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-after.json"
python3 "$root/native-origins.py" "$root"
