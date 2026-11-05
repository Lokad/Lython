"""Offline caller attribution from the existing immutable D43 capture; no retiming."""
from pathlib import Path
import bisect,collections,json,re,sys
root=Path(__file__).resolve().parent
capture=Path(sys.argv[1])  # Extracted immutable D43 perf/ directory.
output={}
for case in ('text','ordinary'):
 folder=capture/case;record=json.loads((folder/'record.json').read_text(encoding='utf-8'))
 ranges=[]
 for line in (folder/'perf.map').read_text(encoding='utf-8').splitlines(keepends=True):
  parts=line.rstrip().split(' ',2)
  if len(parts)==3 and line.endswith('\n'):
   try:ranges.append((int(parts[0],16),int(parts[1],16),parts[2]))
   except ValueError:pass
 ranges.sort();starts=[r[0] for r in ranges]
 def resolve(address,original):
  i=bisect.bisect_right(starts,address)-1
  return ranges[i][2] if i>=0 and address<ranges[i][0]+ranges[i][1] else original
 total=0;callers=collections.Counter();paths=collections.Counter();selected=0
 for block in (folder/'perf-stacks.txt').read_text(encoding='utf-8').split('\n\n'):
  lines=block.strip().splitlines()
  if not lines:continue
  h=re.match(r'(\d+)/(\d+)',lines[0])
  if not h or h[2] not in record['guestTids']:continue
  names=[]
  for line in lines[1:]:
   m=re.match(r'\s*([0-9a-f]+) (.*)',line)
   if m:names.append(resolve(int(m[1],16),m[2]))
  if not names:continue
  total+=1
  if 'IsInstanceOfInterface' not in names[0].split('(',1)[0]:continue
  selected+=1
  managed=[n for n in names[1:] if '[Lokad.Lython]' in n]
  callers[managed[0] if managed else '[unresolved]']+=1
  paths['\n'.join(managed[:4]) or '[unresolved]']+=1
 output[case]=dict(totalGuestSamples=total,interfaceLeaves=selected,firstLythonCallers=callers.most_common(),paths=paths.most_common())
assert output['text']['totalGuestSamples']==783 and output['ordinary']['totalGuestSamples']==1022
(root/'caller-assessment.json').write_text(json.dumps(output,indent=2)+'\n',encoding='utf-8')
for case,data in output.items():
 print(case,data['interfaceLeaves'],'of',data['totalGuestSamples'])
 for name,count in data['firstLythonCallers']:print(count,name.split('(',1)[0])
