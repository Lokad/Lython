import json,re,bisect,collections
from pathlib import Path
root=Path(__file__).parent/'retrieved/perf'
all_results={}
for case in ['text','ordinary']:
 folder=root/case
 r=json.loads((folder/'record.json').read_text(encoding='utf-8'))
 ranges=[]
 for line in (folder/'perf.map').read_text(encoding='utf-8').splitlines(keepends=True):
  parts=line.rstrip().split(' ',2)
  if len(parts)==3 and line.endswith('\n'):
   try:ranges.append((int(parts[0],16),int(parts[1],16),parts[2]))
   except ValueError:pass
 ranges.sort();starts=[x[0] for x in ranges]
 def resolve(address,original):
  i=bisect.bisect_right(starts,address)-1
  return ranges[i][2] if i>=0 and address<ranges[i][0]+ranges[i][1] else original
 counts=collections.Counter();leaves=collections.Counter();lock_callers=collections.Counter();frames=[];total=0
 for block in (folder/'perf-stacks.txt').read_text(encoding='utf-8').split('\n\n'):
  lines=block.strip().splitlines()
  if not lines:continue
  header=re.match(r'(\d+)/(\d+)',lines[0])
  if not header or header[2] not in r['guestTids']:continue
  names=[]
  for l in lines[1:]:
   m=re.match(r'\s*([0-9a-f]+) (.*)',l)
   if m:names.append(resolve(int(m[1],16),m[2]))
  if not names:continue
  total+=1;leaves[names[0]]+=1
  for n in set(names):counts[n]+=1
  if 'System.Threading.Monitor::' in names[0]:
   callers=[n for n in names[1:] if '[Lokad.Lython]' in n]
   lock_callers[callers[0] if callers else '[unresolved caller]']+=1
 groups={}
 for group,parts in {
  'native proof':['GuardNativeReturn','GuardNativeFormattedReturn','GuardNativeFormattedValue'],
  'native evaluation':['EvaluateNativeReturn','AppendNativeFormattedReturn','EvaluateNativeFormatted'],
  'ordinary call':['ExecuteOrdinaryCall'],
  'JSON':['ReadJson','ConvertJson','FindJsonProperty'],
  'whitespace splitting':['CollectWhitespace','SplitWhitespaceCore'],
  'casing':['StringMembers::Lower','ChangeCase','ToLower'],
  'shared allowance':['LythonMemoryBudget'],
 }.items():
  # Report each method count independently; inclusive samples overlap.
  groups[group]=[(n,c,round(100*c/total,2)) for n,c in counts.most_common() if any(p in n.split('(',1)[0] for p in parts)]
 result={'samples':total,'cpuSeconds':json.loads((folder/'guest-cpu-leaves.json').read_text(encoding='utf-8'))['cpuSeconds'],'groups':groups,'lockFirstManagedCallers':lock_callers.most_common(12),'topLeaves':leaves.most_common(35)}
 all_results[case]=result
print(json.dumps(all_results,indent=2))
(root.parent.parent/'inclusive-assessment.json').write_text(json.dumps(all_results,indent=2)+'\n',encoding='utf-8')
