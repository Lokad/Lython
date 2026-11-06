from pathlib import Path
import bisect,collections,json,re
root=Path(__file__).resolve().parent;folder=root/'retrieved/perf/compound'
record=json.loads((folder/'record.json').read_text());tids=set(record['guestTids'])
ranges=[]
for line in (folder/'perf.map').read_text().splitlines(keepends=True):
 p=line.rstrip().split(' ',2)
 if len(p)==3 and line.endswith('\n'):ranges.append((int(p[0],16),int(p[1],16),p[2]))
ranges.sort();starts=[v[0] for v in ranges]
def symbol(address,fallback):
 i=bisect.bisect_right(starts,address)-1
 if i>=0 and address<ranges[i][0]+ranges[i][1]:return ranges[i][2]
 return fallback
leaves=collections.Counter();inclusive=collections.Counter();stacks=[]
for block in (folder/'perf-stacks.txt').read_text().split('\n\n'):
 lines=block.splitlines()
 if not lines:continue
 m=re.match(r'(\d+)/(\d+)',lines[0])
 if not m or m[2] not in tids:continue
 names=[]
 for line in lines[1:]:
  p=line.split(None,1)
  if len(p)==2:names.append(symbol(int(p[0],16),p[1]))
 if not names:continue
 stacks.append(names);leaves[names[0]]+=1;inclusive.update(set(names))
keywords=['BeginPendingCall','BindPositional','EnterInvocationFrame','InitializeExecutableFrame','CloseInvocation','CompletePendingCall',
 'ReturnFunctionWorkspace','AdvanceCallFrames','PrepareNative','ResolveName','CsvRecordParser','CsvDictReaderObject','PyDict','TryResolveInstanceFunctionForCall']
groups={key:dict(inclusiveSamples=sum(any(key in name for name in names) for names in stacks),leafSamples=sum(count for name,count in leaves.items() if key in name)) for key in keywords}
result=dict(totalSamples=len(stacks),groups=groups,leaves=leaves.most_common(),inclusive=inclusive.most_common(),
 limitations='Inclusive methods can include useful work and overlap. Inlined frames and unknown/native leaves are not inferred. Sampling is not a removable-work percentage or source-pair gain.')
(root/'mapped-stacks.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(dict(totalSamples=len(stacks),groups=groups),indent=2))
for name,n in leaves.most_common(45):print(n,name)
