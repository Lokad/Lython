set -eu
root=/home/lokad/lython-bench-checks/native-iteration-retirement-b594-20261111
date -u +%FT%TZ > "$root/started.utc"
systemctl show lython-b594-native-iteration-retirement-20261111.service --property=RuntimeMaxUSec,LoadState,ActiveState,MainPID > "$root/live-unit.txt"
trap 'exit 143' TERM
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
python3 "$root/collect.py"
