from pathlib import Path
import bisect
import collections
import hashlib
import json
import re

root = Path(__file__).parent
folder = root / 'profiles/text-warm'
record = json.loads((folder / 'record.json').read_text(encoding='utf-8'))
guest = set(record['guestTids'])
entries = []
for line in (folder / 'perf.map').read_text(encoding='utf-8').splitlines():
    start, size, name = line.split(' ', 2)
    entries.append((int(start, 16), int(size, 16), name))
entries.sort()
starts = [entry[0] for entry in entries]
assert len(set(starts)) == len(starts), 'Ambiguous map starts require a revised mapping policy.'
overlaps = [(entries[i - 1], entries[i]) for i in range(1, len(entries))
            if entries[i - 1][0] + entries[i - 1][1] > entries[i][0]]
assert not overlaps, 'Overlapping map ranges require explicit resolution.'

def symbol(address, native):
    i = bisect.bisect_right(starts, address) - 1
    if i >= 0:
        start, size, name = entries[i]
        if address < start + size:
            return name
    return native

frames = []
tid = None
samples = []
def finish():
    if tid in guest and frames:
        samples.append(list(frames))

for line in (folder / 'perf-stacks.txt').read_text(encoding='utf-8').splitlines():
    header = re.fullmatch(r'(\d+)/(\d+)\s*', line)
    if header:
        finish()
        tid = header[2]
        frames = []
    elif match := re.match(r'\s+([0-9a-f]+)\s+(.*)', line):
        address = int(match[1], 16)
        frames.append(dict(address=match[1], symbol=symbol(address, match[2]), nativeSymbol=match[2]))
finish()
assert len(samples) == 1151, len(samples)
monitors = [sample for sample in samples if 'System.Threading.Monitor::' in sample[0]['symbol']]
assert len(monitors) == 64, len(monitors)
direct = collections.Counter(sample[1]['symbol'] if len(sample) > 1 else '<missing>' for sample in monitors)
ancestry = collections.Counter()
depths = collections.Counter()
paths = []
for sample in monitors:
    first = next(((i, frame['symbol']) for i, frame in enumerate(sample[1:], 1)
                  if '[Lokad.Lython]' in frame['symbol']), None)
    ancestry[first[1] if first else '<no mapped Lython ancestor>'] += 1
    depths[str(first[0]) if first else '<missing>'] += 1
    paths.append(dict(firstLythonAncestorDepth=first[0] if first else None, frames=sample))
result = dict(source='R211 exact qualified e543/0e capture, 2026-11-10; offline only, no new runtime clock',
    inputs={name: hashlib.sha256((folder / name).read_bytes()).hexdigest()
            for name in ['record.json', 'perf.map', 'perf-stacks.txt']},
    mappedRanges=len(entries), duplicateStarts=0, overlappingRanges=0,
    guestTids=sorted(guest), guestSamples=len(samples), monitorLeafSamples=len(monitors),
    monitorSharePercent=100 * len(monitors) / len(samples),
    directCallerSymbols=direct.most_common(), firstMappedLythonAncestor=ancestry.most_common(),
    firstLythonAncestorDepth=dict(depths), paths=paths,
    limits='Exact observed IP mapping, same policy as R211. Return addresses are not adjusted. Inlining and native/unmapped gaps remain; ancestor attribution is not removable CPU, lock contention or a speedup forecast.')
(root / 'caller-attribution.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: result[k] for k in ['guestSamples', 'monitorLeafSamples', 'directCallerSymbols',
    'firstMappedLythonAncestor', 'firstLythonAncestorDepth']}, indent=2))
