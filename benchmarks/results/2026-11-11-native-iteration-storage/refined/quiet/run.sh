set -eu
root=/home/lokad/lython-bench-checks/native-iteration-storage-b593-20261111
date -u +%FT%TZ > "$root/started.utc"
systemctl show lython-b593-native-iteration-storage-20261111.service --property=RuntimeMaxUSec,LoadState,ActiveState,MainPID > "$root/live-unit.txt"
trap 'exit 143' TERM
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
python3 "$root/collect.py"
