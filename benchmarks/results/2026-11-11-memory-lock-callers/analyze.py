from pathlib import Path
import bisect
import collections
import hashlib
import json
import re
import tarfile

root = Path(__file__).parent
archive = root.parent / '2026-11-10-current-text-construction-cost/observations.tgz'
with tarfile.open(archive) as saved:
    inputs = {name: saved.extractfile('profiles/text-warm/' + name).read() for name in ['record.json', 'perf.map', 'perf-stacks.txt']}
record = json.loads(inputs['record.json'])
guest = set(record['guestTids'])
entries = []
for line in inputs['perf.map'].decode('utf-8').splitlines():
    start, size, name = line.split(' ', 2)
    entries.append((int(start, 16), int(size, 16), name))
raw_count = len(entries)
duplicate_starts = len([n for n in collections.Counter(row[0] for row in entries).values() if n > 1])
entries = sorted(set(entries))
starts = [entry[0] for entry in entries]
maximum_ends = []
for start, size, _ in entries:
    maximum_ends.append(max(maximum_ends[-1] if maximum_ends else 0, start + size))

def symbol(address, native):
    names = set()
    i = bisect.bisect_right(starts, address) - 1
    while i >= 0 and maximum_ends[i] > address:
        start, size, name = entries[i]
        if address < start + size:
            names.add(name)
        i -= 1
    if len(names) == 1:
        return next(iter(names)), sorted(names)
    if names:
        return '<ambiguous mapped ranges>', sorted(names)
    return native, []

frames = []
tid = None
samples = []
def finish():
    if tid in guest and frames:
        samples.append(list(frames))

for line in inputs['perf-stacks.txt'].decode('utf-8').splitlines():
    header = re.fullmatch(r'(\d+)/(\d+)\s*', line)
    if header:
        finish()
        tid = header[2]
        frames = []
    elif match := re.match(r'\s+([0-9a-f]+)\s+(.*)', line):
        address = int(match[1], 16)
        mapped, candidates = symbol(address, match[2])
        frames.append(dict(address=match[1], symbol=mapped, candidates=candidates, nativeSymbol=match[2]))
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
    inputs={name: hashlib.sha256(inputs[name]).hexdigest()
            for name in ['record.json', 'perf.map', 'perf-stacks.txt']},
    rawMapRows=raw_count, uniqueMapRows=len(entries), duplicateStartGroups=duplicate_starts,
    ambiguousMonitorFrames=sum(len(frame['candidates']) > 1 for sample in monitors for frame in sample),
    guestTids=sorted(guest), guestSamples=len(samples), monitorLeafSamples=len(monitors),
    monitorSharePercent=100 * len(monitors) / len(samples),
    directCallerSymbols=direct.most_common(), firstMappedLythonAncestor=ancestry.most_common(),
    firstLythonAncestorDepth=dict(depths), paths=paths,
    limits='Exact observed IP mapping over all containing ranges; ambiguous aliases remain explicit. Return addresses are not adjusted. Inlining and native/unmapped gaps remain; ancestor attribution is not removable CPU, lock contention or a speedup forecast.')
(root / 'recomputed-callers.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: result[k] for k in ['guestSamples', 'monitorLeafSamples', 'directCallerSymbols',
    'firstMappedLythonAncestor', 'firstLythonAncestorDepth']}, indent=2))
