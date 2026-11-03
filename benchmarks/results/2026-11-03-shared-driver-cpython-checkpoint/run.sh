set -eu
root=/home/lokad/lython-bench-checks/shared-driver-checkpoint-b163-20261103
image=/home/lokad/lython-bench-checks/shared-instruction-driver-b162-20261103/retained-candidate
ln -s "$image" "$root/candidate"
ln -s "$image" "$root/baseline"
sudo systemd-run --unit=lython-b163-cpython-checkpoint-20261103 --uid=lokad --wait --collect --property=RuntimeMaxSec=300 --property=KillMode=control-group --property=TimeoutStopSec=10 --property=SendSIGKILL=yes /home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13 -I -S -X utf8 "$root/collect.py"
