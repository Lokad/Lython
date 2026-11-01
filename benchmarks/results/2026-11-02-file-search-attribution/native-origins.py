"""Offline B79 attribution from saved events only; precise declaring-assembly classification."""
import bisect, collections, gzip, json, re, sys
from pathlib import Path
root=Path(sys.argv[1]).resolve()
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


for case in ['file-search']:
 folder=root/'perf'/case
 record=json.loads((folder/'record.json').read_text())
 tid=record['guestTid'];ranges=[];malformed=[]
 for line in (folder/'perf.map').read_text().splitlines(keepends=True):
  parts=line.rstrip().split(' ',2)
  if len(parts)!=3 or not line.endswith('\n'):malformed.append(line);continue
  ranges.append((int(parts[0],16),int(parts[1],16),parts[2]))
 ranges.sort();starts=[r[0] for r in ranges]
 def symbol(address,fallback):
  i=bisect.bisect_right(starts,address)-1
  return ranges[i][2] if i>=0 and address<ranges[i][0]+ranges[i][1] else fallback
 raw=gzip.decompress((folder/'saved-chains.txt.gz').read_bytes()).decode('utf-8')
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
   managed=[x for x in chain if core_method(x)]
   native[('tls' if '__tls_get_addr' in name else 'barrier' if 'JIT_CopiedWriteBarriers' in name else 'monitor' if 'Monitor::' in name else 'unknown',managed[0] if managed else '[missing Lython caller]')]+=1
 result=dict(purpose='Offline origin attribution, no new capture or timing',case=case,guestTid=tid,samples=len(chains),emptyChains=sum(not x for x in chains),malformedMapLines=malformed,allLeafCounts=leaf.most_common(),interpreterSelf=wrappers.most_common(),nativeFirstLythonOrigins=[dict(category=k[0],caller=k[1],samples=n) for k,n in native.most_common()],note='First recorded managed caller may omit inlined/missing frames; it is not exact causal or removable CPU attribution.')
 (folder/'native-origins.json').write_text(json.dumps(result,indent=2)+'\n')
 print(case,'offline origin derivation complete, samples',len(chains),flush=True)
