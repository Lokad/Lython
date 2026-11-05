from pathlib import Path
import bisect,collections,hashlib,json,re
root=Path(__file__).resolve().parent
out={}
def declared(name):
    match=re.search(r'\[Lokad\.Lython\]\s+([^\s(]+)::([^\s(]+)',name)
    return match.groups() if match else None
def group(pair):
    if pair is None:return []
    owner,method=pair;groups=[]
    if method in ['GuardNativeReturn','GuardNativeFormattedValue','TryReadNativeFormattedValue']:groups.append('native dynamic proof')
    if method in ['EvaluateNativeReturn','AppendNativeFormattedReturn','EvaluateNativeFormattedReturn']:groups.append('native evaluation')
    if '+JsonModule' in owner or '/JsonModule' in owner:groups.append('JSON')
    if 'LythonMemory' in owner or 'MemoryGovernor' in owner:groups.append('allowance ownership')
    if method in ['AppendInterpolatedStringValue','AppendInterpolatedStringPart','FormatInterpolatedStringValue']:groups.append('shared primitive formatting')
    if method in ['AppendIntegerFormat','AppendFloatingFormat','AppendAsciiNumericParts','WriteNumericParts','GetNumericOutputLayout']:groups.append('numeric formatting')
    if method in ['CollectWhitespaceForward','CollectWhitespaceReverse','SplitWhitespaceCore','AppendWhitespaceRange']:groups.append('whitespace construction')
    if method in ['WriteCaseUtf8','MapCaseRune','CaseOutputCapacity','WriteAsciiCase']:groups.append('Unicode casing')
    if method=='ExecuteOrdinaryCall':groups.append('ordinary call')
    if method=='SetSubscriptValue':groups.append('subscript store')
    if 'PyNumberOps' in owner and method in ['TryAsNumber','TryAsInteger']:groups.append('scalar classification')
    return groups
for name in ['text','report']:
    folder=root/'retrieved/perf'/name
    record=json.loads((folder/'record.json').read_text(encoding='utf-8'))
    ranges=[];malformed=[]
    for line in (folder/'perf.map').read_text(encoding='utf-8').splitlines(keepends=True):
        parts=line.rstrip().split(' ',2)
        if len(parts)==3 and line.endswith('\n'):
            try:ranges.append((int(parts[0],16),int(parts[1],16),parts[2]))
            except ValueError:malformed.append(line)
        else:malformed.append(line)
    ranges.sort();starts=[r[0] for r in ranges]
    def resolve(address,fallback):
        index=bisect.bisect_right(starts,address)-1
        return ranges[index][2] if index>=0 and address<ranges[index][0]+ranges[index][1] else fallback
    leaves=collections.Counter();inclusive=collections.Counter();group_leaves=collections.Counter();group_inclusive=collections.Counter();locks=collections.Counter();total=0
    for block in (folder/'perf-stacks.txt').read_text(encoding='utf-8').split('\n\n'):
        lines=block.strip().splitlines()
        if not lines:continue
        header=re.match(r'(\d+)/(\d+)',lines[0])
        if not header or header[2] not in record['guestTids']:continue
        symbols=[]
        for line in lines[1:]:
            match=re.match(r'\s*([0-9a-f]+) (.*)',line)
            if match:symbols.append(resolve(int(match[1],16),match[2]))
        if not symbols:continue
        total+=1;leaves[symbols[0]]+=1
        methods=[declared(symbol) for symbol in symbols]
        for pair in set(methods)-{None}:inclusive['::'.join(pair)]+=1
        for label in set(group(methods[0])):group_leaves[label]+=1
        for label in set(label for pair in methods for label in group(pair)):group_inclusive[label]+=1
        if 'System.Threading.Monitor::' in symbols[0]:
            caller=next((pair for pair in methods[1:] if pair is not None),None)
            locks['::'.join(caller) if caller else '[unresolved managed caller]']+=1
    guest=json.loads((folder/'guest-cpu-leaves.json').read_text(encoding='utf-8'))
    assert total==guest['samples']
    result=dict(samples=total,guestCPUSeconds=guest['cpuSeconds'],unknownLeafSamples=leaves['[unknown]'],malformedMapLines=malformed,
        leafGroups=dict(group_leaves),inclusiveGroups=dict(group_inclusive),
        topManagedInclusive=inclusive.most_common(30),topLeaves=leaves.most_common(35),lockFirstDeclaredManagedCallers=locks.most_common(18))
    out[name]=result
    print(name,total,'samples',result['guestCPUSeconds'],'CPU seconds')
    print(' leaf groups',dict(group_leaves))
    print(' inclusive groups',dict(group_inclusive))
    print(' lock callers',locks.most_common(10))
(root/'current-attribution.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print('Raw archive SHA256',hashlib.sha256((root/'observations.tgz').read_bytes()).hexdigest())
