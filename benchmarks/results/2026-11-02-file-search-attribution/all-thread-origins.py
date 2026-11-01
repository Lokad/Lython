"""One offline view of the existing B79 capture, including non-guest threads."""
import bisect, collections, gzip, json, re
from pathlib import Path

root = Path(__file__).resolve().parent / 'retrieved/perf/file-search'
record = json.loads((root / 'record.json').read_text(encoding='utf-8'))
ranges = []
malformed = []
for line in (root / 'perf.map').read_text(encoding='utf-8').splitlines(keepends=True):
    parts = line.rstrip().split(' ', 2)
    if len(parts) != 3 or not line.endswith('\n'):
        malformed.append(line)
        continue
    ranges.append((int(parts[0], 16), int(parts[1], 16), parts[2]))
ranges.sort()
starts = [r[0] for r in ranges]
def symbol(address, fallback):
    index = bisect.bisect_right(starts, address) - 1
    return ranges[index][2] if index >= 0 and address < ranges[index][0] + ranges[index][1] else fallback

def core_method(name):
    # Parameters and return types can mention the core from another assembly.
    # Use the declaring type before ::, and reject incomplete generic owners.
    prefix = name.partition('::')[0]
    match = re.search(r'\[([^]]+)\]\s+([^[]+)$', prefix)
    return bool(match and match[1] == 'Lokad.Lython'
                and match[2].startswith('Lokad.Lython.')
                and match[2].count('<') == match[2].count('>'))

assert core_method('instance bool [Lokad.Lython] Lokad.Lython.Runtime.PyRange+<IterateChecked>d__22::MoveNext()')
assert not core_method('instance string [Lokad.Lython.Benchmarks] Lokad.Lython.Benchmarks.Worker::Invoke(class [Lokad.Lython]Lokad.Lython.LythonCompiledScript)')
assert not core_method('class [Lokad.Lython]Lokad.Lython.LythonExecutionResult [Other] External.Host::Run()')
assert not core_method('instance void [System.Private.CoreLib] System.Collections.Generic.List`1<class [Lokad.Lython] Lokad.Lython.Item>::Clear()')

threads = {t['tid']: dict(t, samples=0, coreSelf=collections.Counter(), coreInclusive=0,
                        firstCore=collections.Counter(), allLeaves=collections.Counter())
           for t in record['threadCpuSeconds']}
raw = gzip.decompress((root / 'saved-chains.txt.gz').read_bytes()).decode('utf-8')
tid, frames = None, []
for line in raw.splitlines() + ['']:
    head = re.fullmatch(r'\s*(\d+)/(\d+)\s*', line)
    frame = re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)', line)
    if head:
        tid, frames = head[2], []
    elif frame and tid is not None:
        frames.append(symbol(int(frame[1], 16), frame[2]))
    elif not line.strip() and tid is not None:
        row = threads.setdefault(tid, dict(tid=tid, name='not in thread snapshot', seconds=None,
                                         samples=0, coreSelf=collections.Counter(), coreInclusive=0,
                                         firstCore=collections.Counter(), allLeaves=collections.Counter()))
        row['samples'] += 1
        leaf = frames[0] if frames else '[empty]'
        row['allLeaves'][leaf] += 1
        core = [f for f in frames if core_method(f)]
        if core:
            row['coreInclusive'] += 1
            row['firstCore'][core[0]] += 1
        if core_method(leaf):
            row['coreSelf'][leaf] += 1
        tid, frames = None, []
result = []
for row in threads.values():
    for key in ['allLeaves', 'coreSelf', 'firstCore']:
        row[key] = row[key].most_common()
    result.append(row)
result.sort(key=lambda x: x['samples'], reverse=True)
(root / 'all-thread-origins.json').write_text(json.dumps(dict(
    boundary='Offline same stopped B79 worker, every recorded thread; no new capture',
    note='Inclusive caller chains overlap and can omit inlined/missing frames. No causal fractions.',
    malformedMapLines=malformed, threads=result), indent=2), encoding='utf-8')
for row in result:
    print(row['name'], row['seconds'], 'samples', row['samples'], 'core-self',
          sum(n for _, n in row['coreSelf']), 'core-inclusive', row['coreInclusive'])
    if row['name'] != 'Lython executio':
        print(' first Lython callers:', row['firstCore'][:6])
