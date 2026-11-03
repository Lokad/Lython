set -eu
/home/lokad/lython-bench-toolchains/cpython/3.13.16/bin/python3.13 -I -S -X utf8 /home/lokad/lython-bench-checks/native-utf8-handoff-b158-20261103/collect.py
sh /home/lokad/lython-bench-checks/native-utf8-handoff-b158-20261103/collect-allocation.sh
