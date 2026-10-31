import bisect,collections,json,re,subprocess
from pathlib import Path
root=Path('/home/lokad/lython-bench-checks/numeric-cpu-20261031-0268ebff')
for case in ['integer-warm','float-warm']:
 folder=root/'perf'/case;r=json.loads((folder/'record.json').read_text());tid=next(x['tid'] for x in r['threadCpuSeconds'] if x['name'].startswith('Lython executio'))
 maps=[]
 for line in (folder/'perf.map').read_text().splitlines(keepends=True):
  p=line.rstrip().split(' ',2)
  if len(p)==3 and line.endswith('\n'):maps.append((int(p[0],16),int(p[1],16),p[2]))
 maps.sort();starts=[x[0] for x in maps]
 def symbol(address,name):
  i=bisect.bisect_right(starts,address)-1
  return maps[i][2] if i>=0 and address<maps[i][0]+maps[i][1] else name
 text=subprocess.check_output(['sudo','-n','perf','script','-i',str(folder/'perf.data'),'-F','pid,tid,ip,sym'],text=True,timeout=15)
 chains=[];active=None;frames=[]
 for line in text.splitlines()+['']:
  h=re.fullmatch(r'\s*(\d+)/(\d+)\s*',line);f=re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)',line)
  if h:active=h[2];frames=[]
  elif f and active==tid:frames.append(symbol(int(f[1],16),f[2]))
  elif not line.strip() and active is not None:
   if active==tid:chains.append(frames)
   active=None;frames=[]
 stats=collections.Counter();first=collections.Counter();numberBudget=collections.Counter()
 for chain in chains:
  joined='\n'.join(chain);number=any(s in joined for s in ['ReadJsonNumberOrConstantAsync','ConvertJsonNumberTextAsync','TryMatchJsonNumber'])
  for label,yes in [('json', 'JsonModule' in joined),('array','ReadJsonArrayAsync' in joined),('number',number),('number-budget',number and any(s in joined for s in ['MemoryGovernor','LythonMemoryBudget','Monitor::'])),('number-stack',number and any(s in joined for s in ['ExecutionGuards','EnsureInterpreterStack'])),('sum',any(s in joined for s in ['::Sum(','+SumState::']))]:
   if yes:stats[label]+=1
  managed=next((s for s in chain if '[Lokad.Lython]' in s),'<none>');first[managed]+=1
  if number and any(s in joined for s in ['MemoryGovernor','LythonMemoryBudget','Monitor::']):numberBudget[' -> '.join(chain[:5])]+=1
 result=dict(case=case,samples=len(chains),inclusiveCategories=stats,firstManaged=first.most_common(18),numberBudgetChains=numberBudget.most_common(),note='Overlapping inclusive categories, approximate CPU samples; inlined/missing frames and unknown leaves are not assigned exact costs.')
 (folder/'number-origins.json').write_text(json.dumps(result,indent=2)+'\n')
 print(case,len(chains),dict(stats))
 for sym,n in first.most_common(8):print(n,sym)
