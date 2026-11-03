set -eu
root=/home/lokad/lython-bench-checks/native-sequence-operands-b171-20261103-report
tar -xzf "$root/payload.tgz" -C "$root"
sudo systemd-run --unit=lython-b171-report-confirmation-20261103 --uid=lokad --wait --collect --property=RuntimeMaxSec=120 --property=KillMode=control-group --property=TimeoutStopSec=10 --property=SendSIGKILL=yes /bin/sh "$root/collect.sh"
