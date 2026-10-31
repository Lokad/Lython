"""Map actual on-CPU leaf IPs using the saved CLR perf map (Linux, offline)."""
import bisect
import collections
import json
import pathlib
import re
import subprocess
import sys

folder = pathlib.Path(sys.argv[1]).resolve()
record = json.loads((folder / "record.json").read_text())
active = max(record["threadCpuSeconds"], key=lambda row: row["seconds"])
ranges = []
malformed = []
for line in (folder / "perf.map").read_text().splitlines(keepends=True):
    parts = line.rstrip().split(" ", 2)
    if len(parts) != 3 or not line.endswith("\n"):
        malformed.append(line)
        continue
    start, size, name = parts
    ranges.append((int(start, 16), int(size, 16), name))
ranges.sort()
starts = [r[0] for r in ranges]
leaves = subprocess.check_output(["sudo", "-n", "perf", "script", "-G", "-i",
    str(folder / "perf.data"), "-F", "pid,tid,ip,sym"], text=True)
(folder / "perf-leaves.txt").write_text(leaves)
counts = collections.Counter()
for line in leaves.splitlines():
    match = re.match(r"\s*(\d+)/(\d+)\s+([0-9a-fx]+)\s*(.*)", line)
    if not match or match[2] != active["tid"]:
        continue
    address = int(match[3], 16)
    index = bisect.bisect_right(starts, address) - 1
    symbol = match[4]
    if index >= 0:
        start, size, name = ranges[index]
        if address < start + size:
            symbol = name
    counts[symbol] += 1
summary = dict(activeTid=active["tid"], samples=counts.total(), cpuSeconds=active["seconds"], malformedMapLines=malformed,
    topLeafCpuSamples=counts.most_common(24), unknown=counts["[unknown]"])
(folder / "cpu-leaves.json").write_text(json.dumps(summary, indent=2) + "\n")
