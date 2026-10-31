set -eu
root=/home/lokad/lython-bench-checks/numeric-cpu-20261031-0268ebff
dotnet=/home/lokad/lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet
export DOTNET_ROOT="$(dirname "$dotnet")"
exec 9>/home/lokad/lython-bench-checks/comparison-vm.lock
flock -n 9
"$dotnet" "$root/worker/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-before.json"
python3 "$root/profile.py" "$root/perf" perf
"$dotnet" "$root/worker/Lokad.Lython.Benchmarks.dll" --compare check-machine --out "$root/machine-after.json"
