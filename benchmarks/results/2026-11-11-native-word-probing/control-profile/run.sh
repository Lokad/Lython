set -eu
root=/home/lokad/lython-bench-checks/word-control-profile-b596-20261111
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
export TMPDIR=/home/lokad/lython-bench-checks/word-control-profile-b596-20261111/guest-fixtures
date -u +%FT%TZ > "$root/started.utc"
systemctl show lython-b596-word-control-profile-20261111.service --property=RuntimeMaxUSec,LoadState,ActiveState,MainPID > "$root/live-unit.txt"
trap 'exit 143' TERM
trap 'result=$?; printf "%s\n" "$result" > "$root/exit-code.txt"; date -u +%FT%TZ > "$root/stopped.utc"' EXIT
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
sleep 5
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-before.json"
findmnt -T "/home/lokad/lython-bench-checks/word-control-profile-b596-20261111/guest-fixtures" -o TARGET,SOURCE,FSTYPE,OPTIONS > "$root/fixture-filesystem.txt"
python3 "$root/ops-profile.py" "$root/guest-fixtures/profiles"
sleep 5
"$dotnet" "$root/candidate/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-after.json"
for case in reference candidate; do
 python3 "$root/analyze-cpu-guest.py" "$root/guest-fixtures/profiles/$case"
done
# All sudo perf reads have ended before transferring these owned files.
for case in reference candidate; do
 sudo -n chown lokad:lokad "$root/guest-fixtures/profiles/$case/perf.data"
done
python3 "$root/archive.py"
