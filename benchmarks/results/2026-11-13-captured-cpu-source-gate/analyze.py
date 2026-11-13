from pathlib import Path
import bisect,collections,gzip,json,re
r=Path(__file__).parent;folder=r/'retrieved/profiles/captured';record=json.loads((folder/'record.json').read_text(encoding='utf-8'))
ranges=[];malformed=[]
for line in gzip.open(folder/'perf.map.gz','rt',encoding='utf-8').read().splitlines(keepends=True):
    parts=line.rstrip().split(' ',2)
    if len(parts)!=3 or not line.endswith('\n'):malformed.append(line);continue
    start,size,name=parts;ranges.append((int(start,16),int(size,16),name))
ranges=sorted(set(ranges));starts=[row[0]for row in ranges];prefix=[]
for start,size,name in ranges:prefix.append(max(start+size,prefix[-1] if prefix else 0))
def symbol(address,fallback):
    index=bisect.bisect_right(starts,address)-1;matches=set()
    while index>=0 and prefix[index]>address:
        start,size,name=ranges[index]
        if start<=address<start+size:matches.add(name)
        index-=1
    if len(matches)==1:return next(iter(matches))
    if matches:return '[ambiguous] '+' | '.join(sorted(matches))
    return fallback
samples=[];current=None
for line in gzip.open(folder/'perf-stacks.txt.gz','rt',encoding='utf-8').read().splitlines():
    header=re.match(r'^.*?\s(\d+)\s+\d+\.\d+:\s+\d+\s+cpu-clock:',line)
    if header:
        current=[]if header[1]in record['guestTids']else None
        if current is not None:samples.append(current)
        continue
    frame=re.match(r'^\s+([0-9a-f]+)\s+(.*)$',line)
    if current is not None and frame:current.append(symbol(int(frame[1],16),frame[2]))
leaves=collections.Counter(stack[0]for stack in samples if stack)
guest=collections.Counter(next((name for name in stack if 'Lokad.Lython.'in name),'[no Lython frame]')for stack in samples if stack)
families=collections.Counter()
for name,count in guest.items():
    family=('casing/whitespace'if any(x in name for x in ['PyStringOps','UnicodeCaseMapping'])else'dictionary/hash'if any(x in name for x in ['PyDict','HashCode','StructuralHash'])else'native helper/slot dispatch'if any(x in name for x in ['NativeExecution','TryStartNativeCall','StartInline'])else'ordinary instruction/call'if any(x in name for x in ['Executable','PyFunction','PyBound'])else'memory admission'if any(x in name for x in ['MemoryGovernor','LythonMemory','OwnedMemory'])else'file/iteration'if any(x in name for x in ['TextReader','ByteFile','PyIteration','TextFile'])else'other')
    families[family]+=count
summary=dict(source='c5d05cff/3a2c1cf6 captured source gate',event='cpu-clock/fp199Hz;8s capture',selectedGuestTids=record['guestTids'],guestCpuSeconds=sum(row['seconds']for row in record['threadCpuSeconds']if row['tid']in record['guestTids']),timedExecutionSeconds=record['response']['elapsedTicks']/record['response']['clockFrequency'],samples=leaves.total(),malformedMapLines=malformed,unmappedLeafSamples=sum(n for name,n in leaves.items()if '[unknown]'in name),ambiguousLeafSamples=sum(n for name,n in leaves.items()if name.startswith('[ambiguous]')),topLeafCpuSamples=leaves.most_common(40),nearestLythonFrame=guest.most_common(40),nearestFrameFamilies=families.most_common(),interpretation='CPU samples, not wall-time. Nearest owning frames include useful/inlined/native work and are not removable shares. Wider worker CPU/reset/validation and other threads remain in raw evidence. No timing or allocation comparison.')
(r/'attribution.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8',newline='\n')
print('Captured CPU gate:',summary['samples'],'samples;',summary['guestCpuSeconds'],'guest CPU s;',summary['unmappedLeafSamples'],'unknown leaves;',summary['ambiguousLeafSamples'],'ambiguous.')
