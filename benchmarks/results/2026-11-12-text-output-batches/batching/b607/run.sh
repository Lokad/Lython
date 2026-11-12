set -eu
root=/home/lokad/lython-bench-checks/text-batches-b607-20261112
date -u +%FT%TZ > "$root/started.utc"
systemctl show lython-b607-text-batches-20261112.service --property=RuntimeMaxUSec,LoadState,ActiveState,MainPID > "$root/live-unit.txt"
trap 'exit 143' TERM
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
python3 "$root/collect.py"
