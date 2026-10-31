set -eu
root=/home/lokad/lython-bench-checks/cpu-ops-20261031-2fd1ccfd
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
"$dotnet" "$root/worker/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-before.json"
sudo -n systemd-run --quiet --wait --pipe --collect --unit=lython-ops-20261031 --uid=lokad --working-directory="$root" -p RuntimeMaxSec=180 -p TimeoutStopSec=0 -p KillMode=control-group python3 "$root/ops-profile.py" "$root/perf" perf
"$dotnet" "$root/worker/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-after.json"
