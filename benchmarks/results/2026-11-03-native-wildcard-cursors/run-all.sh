set -eu
root=/home/lokad/lython-bench-checks/native-wildcard-cursors-b141-20261103
python3 "$root/collect.py"
sleep 5
sh "$root/collect-allocation.sh"
