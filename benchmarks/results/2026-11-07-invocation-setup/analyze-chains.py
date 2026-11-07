"""Offline reuse of the existing perf-map/call-chain analysis; no new capture."""
from pathlib import Path
import bisect,collections,json,re,tarfile
root=Path(__file__).parent
with tarfile.open(root/'observations.tgz') as archive:
 def read(name):return archive.extractfile('perf/config-warm/'+name).read().decode('utf-8')
 record=json.loads(read('record.json'));tids=set(record['guestTids'])
 ranges=[];malformed=[]
 for line in read('perf.map').splitlines(keepends=True):
  parts=line.rstrip().split(' ',2)
  if len(parts)==3 and line.endswith('\n'):ranges.append((int(parts[0],16),int(parts[1],16),parts[2]))
  else:malformed.append(line)
 ranges.sort();starts=[row[0] for row in ranges]
 def symbol(address,fallback):
  i=bisect.bisect_right(starts,address)-1
  return ranges[i][2] if i>=0 and address<ranges[i][0]+ranges[i][1] else fallback
 chains=[];current=None;frames=[]
 for line in read('perf-stacks.txt').splitlines()+['']:
  header=re.fullmatch(r'\s*(\d+)/(\d+)\s*',line)
  frame=re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)',line)
  if header:current=header[2];frames=[]
  elif frame and current in tids:frames.append(symbol(int(frame[1],16),frame[2]))
  elif not line.strip() and current is not None:
   if current in tids:chains.append(frames)
   current=None;frames=[]
 inclusive=collections.Counter();leaves=collections.Counter();monitor=collections.Counter()
 for chain in chains:
  if chain:leaves.update([chain[0]])
  inclusive.update(set(x for x in chain if '[Lokad.Lython]' in x))
  if chain and ('Monitor::Enter' in chain[0] or 'Monitor::Exit' in chain[0]):monitor.update([' -> '.join(chain[:7])])
 output=dict(samples=len(chains),guestTids=sorted(tids),emptyChains=sum(not c for c in chains),
  singleFrameChains=sum(len(c)==1 for c in chains),malformedMapLines=malformed,
  topLeaf=leaves.most_common(),topInclusive=inclusive.most_common(),monitorChains=monitor.most_common(),
  note='Inclusive chains overlap; incomplete managed/native stacks, inlining and sample skid remain. No causal saved-CPU or timing claim.')
 (root/'guest-call-chains.json').write_text(json.dumps(output,indent=2)+'\n',encoding='utf-8')
 print(json.dumps(dict(samples=output['samples'],empty=output['emptyChains'],single=output['singleFrameChains'],
  malformed=len(malformed),leaves=output['topLeaf'][:16],inclusive=output['topInclusive'][:12]),indent=2))
