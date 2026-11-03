set -eu
root=/home/lokad/lython-bench-checks/json-key-storage-b138-refined-20261103
python3 "$root/collect.py"
sh "$root/collect-allocation.sh"
