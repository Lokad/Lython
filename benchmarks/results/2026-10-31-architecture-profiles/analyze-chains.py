"""One offline pass over existing perf call chains; no new collection."""
import bisect,collections,json,re,subprocess
from pathlib import Path
root=Path('/home/lokad/lython-bench-checks/architecture-20261031-07c150a6')
for case in ['csv-warm','text-warm','json-warm']:
 folder=root/'perf'/case
 record=json.loads((folder/'record.json').read_text())
 tid=str(max(record['threadCpuSeconds'],key=lambda x:x['seconds'])['tid'])
 ranges=[]
 for line in (folder/'perf.map').read_text().splitlines(keepends=True):
  p=line.rstrip().split(' ',2)
  if len(p)==3 and line.endswith('\n'):ranges.append((int(p[0],16),int(p[1],16),p[2]))
 ranges.sort();starts=[r[0] for r in ranges]
 def symbol(address,fallback):
  i=bisect.bisect_right(starts,address)-1
  return ranges[i][2] if i>=0 and address<ranges[i][0]+ranges[i][1] else fallback
 raw=subprocess.check_output(['sudo','-n','perf','script','-i',str(folder/'perf.data'),'-F','pid,tid,ip,sym'],text=True,timeout=15)
 chains=[];current=None;frames=[]
 for line in raw.splitlines()+['']:
  header=re.fullmatch(r'\s*(\d+)/(\d+)\s*',line)
  frame=re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)',line)
  if header:current=header[2];frames=[]
  elif frame and current==tid:frames.append(symbol(int(frame[1],16),frame[2]))
  elif not line.strip() and current is not None:
   if current==tid:chains.append(frames)
   current=None;frames=[]
 inclusive=collections.Counter();monitor=collections.Counter()
 for chain in chains:
  inclusive.update(set(x for x in chain if '[Lokad.Lython]' in x))
  if chain and ('Monitor::Enter' in chain[0] or 'Monitor::Exit' in chain[0]):
   monitor.update([' -> '.join(chain[:6])])
 result=dict(case=case,thread=tid,samples=len(chains),emptyChains=sum(not c for c in chains),oneFrameChains=sum(len(c)==1 for c in chains),topInclusive= inclusive.most_common(30),monitorCallChains=monitor.most_common(20),note='Offline DWARF call chains may be incomplete across managed/native transitions; missing chains remain explicit. No exact CPU share or recapture.')
 (folder/'call-chains.json').write_text(json.dumps(result,indent=2)+'\n')
 print(case,'samples',len(chains),'empty',result['emptyChains'],'single',result['oneFrameChains'],flush=True)
