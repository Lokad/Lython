set -eu
root=/home/lokad/lython-bench-checks/record-key-shape-b166-20261103-refined
tar -xzf "$root/payload.tgz" -C "$root"
sudo systemd-run --unit=lython-b166-record-key-refined-20261103 --uid=lokad --wait --collect --property=RuntimeMaxSec=300 --property=KillMode=control-group --property=TimeoutStopSec=10 --property=SendSIGKILL=yes /bin/sh "$root/collect.sh"
