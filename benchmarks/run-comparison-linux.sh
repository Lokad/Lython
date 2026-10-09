#!/usr/bin/env bash
set -euo pipefail

# One lane, one ten-minute wall budget, including catalog preparation and retries.
# Build/test first. Run this in a dedicated systemd service with RuntimeMaxSec=600,
# TimeoutStopSec=0 and KillMode=control-group to enforce the bound on all children.
if (( $# != 6 )); then
  printf '%s\n' 'Usage: run-comparison-linux.sh <dotnet> <python> <toolchains.json> <output-directory> <shared-lock-file> <lane>' >&2
  exit 2
fi
dotnet=$1 python=$2 toolchains=$3 notes=$4 lock=$5 lane=$6
case "$lane" in warm|compile-run|compile|fresh-process) ;; *) exit 2 ;; esac
for path in "$dotnet" "$python" "$toolchains" "$notes" "$lock"; do
  [[ "$path" == /* ]] || { printf '%s\n' 'All paths must be absolute.' >&2; exit 2; }
done
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$root"
started=$(date +%s)
deadline=$((started + 600))
mkdir -p "$notes"
exec 9>"$lock"
/usr/bin/flock -n 9 || { printf '%s\n' 'The VM comparison lease is held.' >&2; exit 5; }
[[ ! -e "$notes/$lane-started.utc" ]] || { printf '%s\n' 'This lane already started; preserve its evidence and original budget.' >&2; exit 5; }
date -u +%FT%TZ > "$notes/$lane-started.utc"
trap 'result=$?; printf "%s\n" "$result" > "$notes/$lane-exit-code.txt"; date -u +%FT%TZ > "$notes/$lane-stopped.utc"' EXIT
[[ -z "$(git status --porcelain --untracked-files=normal)" ]] || { printf '%s\n' 'A clean checkout is required.' >&2; exit 5; }
assembly="$root/benchmarks/Lokad.Lython.Benchmarks/bin/Release/net10.0/Lokad.Lython.Benchmarks.dll"
helper="$root/benchmarks/cpython-worker.py"
catalog="$notes/$lane-catalog.json"
"$dotnet" "$assembly" --compare list --profile quick --out "$catalog" > "$notes/$lane-catalog.log" 2>&1
result=3
for attempt in 1 2 3; do
  resume=()
  if (( attempt > 1 )); then
    sleep 5
    resume=(--resume true)
  fi
  remaining=$((deadline - $(date +%s) - 10))
  if (( remaining <= 0 )); then result=124; break; fi
  printf 'LANE=%s ATTEMPT=%s REMAINING_COLLECTION_SECONDS=%s\n' "$lane" "$attempt" "$remaining"
  set +e
  /usr/bin/timeout --signal=INT --kill-after=5s "${remaining}s" \
    "$dotnet" "$assembly" --compare qualify --catalog "$catalog" --dotnet "$dotnet" --python "$python" \
      --python-worker "$helper" --toolchains "$toolchains" --out "$notes/$lane.json" \
      --case quick --lane "$lane" "${resume[@]}" > "$notes/$lane-attempt-$attempt.log" 2>&1
  result=$?
  set -e
  printf 'LANE=%s ATTEMPT=%s EXIT=%s ELAPSED_SECONDS=%s\n' "$lane" "$attempt" "$result" "$(( $(date +%s) - started ))"
  # Only initial/final noise may retry. Case exclusions are final; failed,
  # interrupted or timed-out attempts never get a fresh budget here.
  if (( result != 3 )); then break; fi
done
exit "$result"
