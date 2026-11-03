set -eu
root=/home/lokad/lython-bench-checks/csv-record-storage-b150-20261103
/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13 -I -S -X utf8 "$root/collect.py"
sh "$root/collect-allocation.sh"
