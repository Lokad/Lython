set -eu
root=/home/lokad/lython-bench-checks/native-sequence-checkpoint-b172-20261103
tar -xzf "$root/payload.tgz" -C "$root"
sudo systemd-run --unit=lython-b172-cpython-checkpoint-20261103 --uid=lokad --wait --collect --property=RuntimeMaxSec=360 --property=KillMode=control-group --property=TimeoutStopSec=10 --property=SendSIGKILL=yes /bin/sh "$root/collect.sh"
