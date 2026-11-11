from pathlib import Path
import collections,json
r=Path(__file__).resolve().parent/'b596';p=r/'retrieved/profiles';result={}
maps={}
for role in ['reference','candidate']:
 d=p/role;leaves=json.loads((d/'guest-cpu-leaves.json').read_text(encoding='utf-8'));record=json.loads((d/'record.json').read_text(encoding='utf-8'))
 result[role]=dict(guestCpuSeconds=leaves['cpuSeconds'],guestSamples=leaves['samples'],unknown=leaves['unknown'],completedInvocations=record['response']['completedInvocations'],allThreadCpuSeconds=record['threadCpuSeconds'],topLeaves=leaves['topLeafCpuSamples'])
 entries=collections.defaultdict(set)
 for line in(d/'perf.map').read_text(encoding='utf-8').splitlines():
  parts=line.split(' ',2)
  if len(parts)==3 and '[Lokad.Lython]'in parts[2]and 'OptimizedTier1'in parts[2]:entries[parts[2]].add(int(parts[1],16))
 maps[role]={n:sorted(v)for n,v in entries.items()}
names=set(maps['reference'])&set(maps['candidate']);different=[dict(method=n,reference=maps['reference'][n],candidate=maps['candidate'][n])for n in sorted(names)if maps['reference'][n]!=maps['candidate'][n]]
summary=dict(purpose='Bounded paired diagnostic profile after B595. One process/image with perf/JIT diagnostics; no normal-profile elapsed/CPU comparison or JIT-cause claim.',images=result,commonOptimizedManagedNames=len(names),sameSizeNames=len(names)-len(different),differentSizeMethods=different,assessment='Shared call/binding/proof/evaluation and dispatch remain material. The profile does not identify a new kernel cost or explain B595 async-search adversity. Different instrumentation/processes cannot settle normal-profile debt. Stop paired profiling here; keep original adverse evidence. Carry R250 as an unpromoted prototype into a coherent captured-input simplification, then explicitly decide material control debt at its delivery boundary.')
(r/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('Current profiles:',[(k,v['guestSamples'],v['guestCpuSeconds'],v['unknown'])for k,v in result.items()]);print('Common managed Tier1 names',len(names),'same extents',len(names)-len(different),'different extents',len(different));print('Different relevant extents:',[(x['method'].split('::')[-1],x['reference'],x['candidate'])for x in different if any(n in x['method']for n in ['GuardNativeReturn','EvaluateNativeReturn','TryExecuteNativeCall','ExecuteInstructionPrefix'])])
