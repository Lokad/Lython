set -eu
root=/dev/shm/lython-allowance-gate-b177-20261103
before=/home/lokad/lython-bench-checks/terminal-utf8-storage-b174-20261103/candidate/Lokad.Lython.Benchmarks.dll
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
"$dotnet" "$before" --compare check-machine --out "$root/micro/machine-before.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/micro/machine-before.json"
"$dotnet" "$root/micro/complete-jobs.dll" /home/lokad/lython-bench-checks/terminal-utf8-storage-b174-20261103/candidate/Lokad.Lython.dll "$root/candidate/Lokad.Lython.dll" "$root/micro/measurements.json"
"$dotnet" "$before" --compare check-machine --out "$root/micro/machine-after.json"
python3 -c 'import json,sys;assert json.load(open(sys.argv[1]))["gate"]["quiet"]' "$root/micro/machine-after.json"
