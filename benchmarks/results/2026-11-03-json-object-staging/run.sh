set -eu
root=/home/lokad/lython-bench-checks/json-object-construction-b169-20261103
tar -xzf "$root/payload.tgz" -C "$root"
sudo systemd-run --unit=lython-b169-record-construction-20261103 --uid=lokad --wait --collect --property=RuntimeMaxSec=180 --property=KillMode=control-group --property=TimeoutStopSec=10 --property=SendSIGKILL=yes /bin/sh "$root/collect.sh"
