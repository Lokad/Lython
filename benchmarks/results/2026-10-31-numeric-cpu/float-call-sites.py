"""Offline attribution of saved float events; never runs a guest workload.

Pass the extracted float-warm folder, containing perf.map, jit-disasm.txt,
record.json and saved-chains.txt (perf script -F pid,tid,ip,sym).
"""
import bisect
import collections
import json
from pathlib import Path
import re
import sys

folder = Path(sys.argv[1])
record = json.loads((folder / 'record.json').read_text())
tid = next(int(t['tid']) for t in record['threadCpuSeconds']
           if t['name'].startswith('Lython executio'))
maps = []
for line in (folder / 'perf.map').read_text().splitlines(keepends=True):
    fields = line.rstrip().split(' ', 2)
    if len(fields) == 3 and line.endswith('\n'):
        maps.append((int(fields[0], 16), int(fields[1], 16), fields[2]))
maps.sort()
starts = [m[0] for m in maps]
targets = [m for m in maps if 'JsonModule::ConvertJsonNumberTextAsync' in m[2]]
assert len(targets) == 1, 'Require one unambiguous recorded method.'
target = targets[0]


def resolve(ip):
    i = bisect.bisect_right(starts, ip) - 1
    return maps[i] if i >= 0 and ip < maps[i][0] + maps[i][1] else None


chunks = (folder / 'jit-disasm.txt').read_text().split('; Assembly listing for method ')
methods = [c for c in chunks if c.startswith(
    'Lokad.Lython.Runtime.LythonRuntime+JsonModule:ConvertJsonNumberTextAsync')]
assert len(methods) == 1
size = re.search(r'Total bytes of code (\d+)', methods[0])
assert size and int(size[1]) == target[1], 'JIT listing/map size must agree.'
offset = None
calls = {}
for line in methods[0].splitlines():
    block = re.search(r';; offset=0x([0-9A-Fa-f]+)', line)
    if block:
        offset = int(block[1], 16)
    instruction = re.match(r'\s+([0-9A-F]{2,})\s+(.*)', line)
    if instruction and offset is not None:
        offset += len(instruction[1]) // 2
        if 'call ' in instruction[2]:
            calls[offset] = instruction[2]

chains = []
active = None
frames = []
for line in (folder / 'saved-chains.txt').read_text().splitlines() + ['']:
    header = re.fullmatch(r'\s*(\d+)/(\d+)\s*', line)
    frame = re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)', line)
    if header:
        active = int(header[2])
        frames = []
    elif frame and active == tid:
        frames.append(int(frame[1], 16))
    elif not line.strip() and active is not None:
        if active == tid:
            chains.append(frames)
        active = None
        frames = []

self_offsets = collections.Counter()
return_offsets = collections.Counter()
for frames in chains:
    for index, ip in enumerate(frames):
        method = resolve(ip)
        if method and '[Lokad.Lython]' in method[2]:
            if method == target:
                counts = self_offsets if index == 0 else return_offsets
                counts[ip - target[0]] += 1
            break

result = dict(
    source='0268ebff (production 959df325), existing B11 capture',
    guestSamples=len(chains),
    methodStart=hex(target[0]), methodCodeBytes=target[1],
    firstManagedSamples=sum(self_offsets.values()) + sum(return_offsets.values()),
    directSelfSamples=sum(self_offsets.values()),
    selfOffsets=[dict(offset=hex(o), count=n) for o, n in self_offsets.most_common()],
    nestedCallerSamples=[dict(returnOffset=hex(o), count=n,
                              call=calls.get(o, '<unresolved>'))
                         for o, n in return_offsets.most_common()],
    limitations='Approximate samples; native/inlined callees may be unresolved. '
                 'A caller return IP identifies its call site, not the exact '
                 'callee algorithm or a removable time fraction. No recapture.')
print(json.dumps(result, indent=2))
