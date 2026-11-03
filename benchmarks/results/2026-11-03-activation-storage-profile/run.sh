set -eu
root=/home/lokad/lython-bench-checks/activation-storage-b170-20261103
tar -xzf "$root/payload.tgz" -C "$root"
sudo systemd-run --unit=lython-b170-activation-20261103 --uid=lokad --wait --collect --property=RuntimeMaxSec=120 --property=KillMode=control-group --property=TimeoutStopSec=10 --property=SendSIGKILL=yes /bin/sh "$root/collect.sh"
