"""Bounded offline origin attribution from the two fixed B29 captures."""
import bisect, collections, gzip, json, re, subprocess, sys
from pathlib import Path
root=Path(sys.argv[1]).resolve()
for case in ['config-fresh']:
 folder=root/'perf'/case
 record=json.loads((folder/'record.json').read_text())
 guest=[r for r in record['threadCpuSeconds'] if r['name'].startswith('Lython executio') and r['seconds']>0]
 assert len(guest)==1,guest
 tid=guest[0]['tid'];ranges=[];malformed=[]
 for line in (folder/'perf.map').read_text().splitlines(keepends=True):
  parts=line.rstrip().split(' ',2)
  if len(parts)!=3 or not line.endswith('\n'):malformed.append(line);continue
  ranges.append((int(parts[0],16),int(parts[1],16),parts[2]))
 ranges.sort();starts=[r[0] for r in ranges]
 def symbol(address,fallback):
  i=bisect.bisect_right(starts,address)-1
  return ranges[i][2] if i>=0 and address<ranges[i][0]+ranges[i][1] else fallback
 raw=subprocess.check_output(['sudo','-n','perf','script','-i',str(folder/'perf.data'),'-F','pid,tid,ip,sym'],text=True,timeout=15)
 with gzip.open(folder/'saved-chains.txt.gz','wt') as f:f.write(raw)
 chains=[];current=None;frames=[]
 for line in raw.splitlines()+['']:
  header=re.fullmatch(r'\s*(\d+)/(\d+)\s*',line)
  frame=re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)',line)
  if header:current=header[2];frames=[]
  elif frame and current==tid:frames.append(symbol(int(frame[1],16),frame[2]))
  elif not line.strip() and current is not None:
   if current==tid:chains.append(frames)
   current=None;frames=[]
 leaf=collections.Counter();native=collections.Counter();wrappers=collections.Counter()
 for chain in chains:
  name=chain[0] if chain else '[empty chain]';leaf[name]+=1
  if 'ExecutableFrameInterpreter' in name and any(n in name for n in ['ExecuteUntilCall','ExecuteOperandValueOperation','ExecuteValueOperation','ExecuteSync']):wrappers[name]+=1
  if '__tls_get_addr' in name or 'JIT_CopiedWriteBarriers' in name or 'Monitor::' in name or name=='[unknown]':
   managed=[x for x in chain if '[Lokad.Lython]' in x]
   native[('tls' if '__tls_get_addr' in name else 'barrier' if 'JIT_CopiedWriteBarriers' in name else 'monitor' if 'Monitor::' in name else 'unknown',managed[0] if managed else '[missing Lython caller]')]+=1
 result=dict(purpose='Offline origin attribution, no new capture or timing',case=case,guestTid=tid,samples=len(chains),emptyChains=sum(not x for x in chains),malformedMapLines=malformed,allLeafCounts=leaf.most_common(),interpreterSelf=wrappers.most_common(),nativeFirstLythonOrigins=[dict(category=k[0],caller=k[1],samples=n) for k,n in native.most_common()],note='First recorded managed caller may omit inlined/missing frames; it is not exact causal or removable CPU attribution.')
 (folder/'native-origins.json').write_text(json.dumps(result,indent=2)+'\n')
 print(case,'offline origin derivation complete, samples',len(chains),flush=True)
