from pathlib import Path
import bisect, collections, json, re, runpy
r = Path(__file__).parent
analysis = runpy.run_path(str(r / 'analyze-search.py'))
ranges = [v for v in analysis['ranges'] if 'PyExecutableFunction::StartNativeCall(' in v[2] and '[OptimizedTier1]' in v[2]]
assert len(ranges) == 1, [(hex(v[0]), v[1], v[2]) for v in ranges]
start, size, name = ranges[0]
folder = r / 'retrieved/profiles/file'
asm = (folder / 'native-entry.asm').read_text(encoding='utf-8')
sections = [v for v in re.split(r'(?=; Assembly listing for method )', asm) if 'StartNativeCall(' in v and '(Tier1)' in v.splitlines()[0]]
assert len(sections) == 1, [v.splitlines()[0] for v in sections]
body = sections[0]
declared = re.findall(r'; Total bytes of code (\d+)', body)
assert len(declared) == 1 and int(declared[0]) == size, (declared, size)
labels = list(re.finditer(r'^(G_M\d+_IG\d+):[^\n]*offset=0x([0-9a-fA-F]+)', body, re.M))
assert labels and int(labels[0][2], 16) == 0
offsets = [int(v[2], 16) for v in labels]
assert sorted(set(offsets)) == offsets and offsets[-1] < size
leaf_ips = analysis['leafIps']
assert len(leaf_ips) == analysis['leaves'].total()
inside = [ip - start for ip in leaf_ips if start <= ip < start + size]
counts = collections.Counter(bisect.bisect_right(offsets, offset) - 1 for offset in inside)
blocks = [dict(label=label[1], start=offsets[i], end=offsets[i+1] if i+1 < len(offsets) else size, leafSamples=counts[i], instructions=body[label.end():labels[i+1].start() if i+1 < len(labels) else body.index('; Total bytes of code')].strip()) for i, label in enumerate(labels)]
assert sum(b['leafSamples'] for b in blocks) == len(inside)
d = dict(source='Committed810379be/frozen8fbfa3f0', sameProcessJitAndSamples=True, tier1MapStart=hex(start), tier1MapBytes=size, disassemblyBytes=int(declared[0]), uniqueTier1RangeAndDisassembly=True, guestLeafSamples=len(leaf_ips), nativeEntryLeafSamples=len(inside), nativeEntryGuestSamplePercent=100*len(inside)/len(leaf_ips), sampledOffsets=sorted(collections.Counter(inside).items()), blocks=blocks, interpretation='Exact same-process map/disassembly lengths and instruction-group offsets; sampled instruction groups may contain several source operations. This is attribution, not a speedup forecast or proof that a zero-sample operation is free.')
(r / 'native-entry-instruction-groups.json').write_text(json.dumps(d, indent=2) + '\n', encoding='utf-8', newline='\n')
print('Exact entry attribution:', len(inside), '/', len(leaf_ips), 'guest samples;', size, 'bytes;', len(blocks), 'instruction groups.', flush=True)
for row in sorted(blocks, key=lambda v: v['leafSamples'], reverse=True)[:12]:
    print(row['label'], hex(row['start']), hex(row['end']), row['leafSamples'], row['instructions'], '\n', flush=True)
