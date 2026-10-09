#!/usr/bin/env bash
set -euo pipefail
# Run in a dedicated systemd service with RuntimeMaxSec=30,
# TimeoutStopSec=0 and KillMode=control-group. Build and transfer first.
if (( $# < 5 || $# > 6 )); then
  printf '%s\n' 'Usage: run-micro-linux.sh <dotnet> <python> <baseline-worker.dll> <new-output-directory> <shared-lock-file> [case-id]' >&2
  exit 2
fi
dotnet=$1 python=$2 baseline=$3 notes=$4 lock=$5
case_id=${6:-loops.integer.large}
case "$case_id" in
  control.empty.control|control.tiny.control|loops.integer.small|loops.integer.medium|loops.integer.large) profile=core-loop ;;
  *) profile=full ;;
esac
for path in "$dotnet" "$python" "$baseline" "$notes" "$lock"; do
  [[ "$path" == /* ]] || { printf '%s\n' 'All paths must be absolute.' >&2; exit 2; }
done
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$root"
exec 9>"$lock"
/usr/bin/flock -n 9 || { printf '%s\n' 'The VM comparison lease is held.' >&2; exit 5; }
[[ ! -e "$notes/started.utc" ]] || { printf '%s\n' 'Preserve existing microbenchmark evidence.' >&2; exit 5; }
mkdir -p "$notes"
date -u +%FT%TZ > "$notes/started.utc"
trap 'result=$?; printf "%s\n" "$result" > "$notes/exit-code.txt"; date -u +%FT%TZ > "$notes/stopped.utc"' EXIT
assembly="$root/benchmarks/Lokad.Lython.Benchmarks/bin/Release/net10.0/Lokad.Lython.Benchmarks.dll"
"$dotnet" "$assembly" --compare list --profile "$profile" --out "$notes/catalog.json" > "$notes/catalog.log" 2>&1
/usr/bin/env DOTNET_TieredCompilation=0 "$dotnet" "$assembly" --compare micro \
  --catalog "$notes/catalog.json" --dotnet "$dotnet" --baseline-worker "$baseline" \
  --python "$python" --python-worker "$root/benchmarks/cpython-worker.py" \
  --out "$notes/micro.json" --case "$case_id" > "$notes/micro.log" 2>&1
cat "$notes/micro.log"
