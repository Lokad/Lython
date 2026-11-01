set -eu
root=/home/lokad/lython-bench-checks/async-hosting-b50-20261101
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
worker=/home/lokad/lython-bench-checks/expression-route-b48-20261101/candidate/Lokad.Lython.Benchmarks.dll
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
"$dotnet" "$worker" --compare check-machine --out "$root/profile-machine-before.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/profile-machine-before.json"
python3 "$root/ops-profile.py" "$root/perf"
"$dotnet" "$worker" --compare check-machine --out "$root/profile-machine-after.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/profile-machine-after.json"
python3 "$root/analyze-cpu-guest.py" "$root/perf/csv-warm-async"
python3 "$root/native-origins.py" "$root"
python3 - "$root" <<'PY'
import json,sys
from pathlib import Path
r=Path(sys.argv[1]);d=json.loads((r/'perf/csv-warm-async/record.json').read_text());assert d['exitCode']==0,d
print('Single allowed async CSV capture complete; workers/profiler joined.',flush=True)
PY
