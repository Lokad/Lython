set -eu
root=/home/lokad/lython-bench-checks/main-loop-b43-20261101-5d16ead3
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
export DOTNET_TieredCompilation=0
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-before.json"
python3 "$root/ops-profile.py" "$root/perf"
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;sys.exit(0 if json.load(open(sys.argv[1]))["gate"]["quiet"] else 1)' "$root/machine-after.json"
python3 "$root/analyze-cpu-guest.py" "$root/perf/text-warm"
python3 "$root/native-origins.py" "$root"
python3 - "$root" <<'PY2'
import json,sys
from pathlib import Path
r=Path(sys.argv[1]);d=json.loads((r/'perf/text-warm/record.json').read_text());assert d['exitCode']==0,d
assert (r/'perf/text-warm/jit-disasm.txt').stat().st_size>0
print('One fixed capture and all worker/profiler exits verified; no recapture.',flush=True)
PY2
