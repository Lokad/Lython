set -eu
root=/home/lokad/lython-bench-checks/call-construction-b113-refined-20261102
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
date -u +%FT%TZ > "$root/started.utc"
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-before.json"
for phase in warm fresh; do
 for block in first second; do
  out="$root/$phase-$block"
  mkdir "$out"
  cp "$root/hash-witness.json" "$out/hash-witness.json"
  direction=forward
  if [ "$block" = second ]; then direction=reverse; fi
  "$dotnet" "$root/jobs/bin/Release/net11.0/complete-jobs.dll" "$root/baseline/Lokad.Lython.Benchmarks.dll" "$root/candidate/Lokad.Lython.Benchmarks.dll" "$root/measurement-catalog.json" "$out/hash-witness.json" "$direction" "$phase" > "$out/comparison.json" 2> "$out/comparison.stderr.txt"
  python3 - "$out/comparison.json" <<'CHECK'
import json,sys
v=json.load(open(sys.argv[1]));assert v['allBalancesZero'] and v['imageIsolationVerified'] and v['allGoldensPassed'] and v['ownedFixturesRemoved']
CHECK
 done
done
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/machine-after.json"
