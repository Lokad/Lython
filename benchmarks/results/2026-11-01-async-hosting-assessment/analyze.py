from pathlib import Path
import hashlib,json
root=Path(__file__).resolve().parent
raw=root/'retrieved/async-hosting-b50-20261101'
identity=json.loads((root/'worker-identity.json').read_text(encoding='utf-8'))['coreSHA256']
pct=lambda a,b:100*(a/b-1)
result=dict(performanceQualified=False,decision='CSV supports testing one shared ordinary instruction loop; text control is inconclusive, no retiming.',cases={})
for case in ('scripts.csv-summary-helpers.large','scripts.text-summary-helpers.large'):
    reports=[json.loads((raw/f'{phase}-{case}.json').read_text(encoding='utf-8')) for phase in ('sync','async','sync-control')]
    assert len({json.dumps(r['identities'],sort_keys=True) for r in reports})==1
    for r in reports:
        assert r['identities']['Lokad.Lython.dll'].lower()==identity
        assert r['batch']==64 and r['warmups']==4 and r['rounds']==7
    obs=[r['observations'][0] for r in reports]
    values={m:[o[m] for o in obs] for m in ('medianCpuMillisecondsPerJob','medianMilliseconds','medianAllocatedBytes')}
    cpu=values['medianCpuMillisecondsPerJob'];alloc=values['medianAllocatedBytes'];wall=values['medianMilliseconds']
    item=dict(measurements=values,asyncCpuChangesPercent=[pct(cpu[1],cpu[i]) for i in (0,2)],
              asyncElapsedChangesPercent=[pct(wall[1],wall[i]) for i in (0,2)],
              cpuControlDriftPercent=pct(cpu[2],cpu[0]),allocationControlDriftPercent=pct(alloc[2],alloc[0]),
              allCpuBatchesResolved=all(o['cpuGranularityResolved'] for o in obs))
    item['controlBoundsPass']=abs(item['cpuControlDriftPercent'])<=5 and abs(item['allocationControlDriftPercent'])<=.2
    item['materialGap']=item['controlBoundsPass'] and item['allCpuBatchesResolved'] and all(v>=15 for v in item['asyncCpuChangesPercent'])
    result['cases'][case]=item
result['collection']=json.loads((raw/'collection.json').read_text(encoding='utf-8'))
assert result['collection']['totalWallSeconds']<60 and result['collection']['quietGates'] and result['collection']['allChildrenJoined']
assert result['cases']['scripts.csv-summary-helpers.large']['materialGap']
profile=raw/'perf/csv-warm-async'
record=json.loads((profile/'record.json').read_text(encoding='utf-8'))
leaves=json.loads((profile/'guest-cpu-leaves.json').read_text(encoding='utf-8'))
assert record['exitCode']==0
result['profile']=dict(captureSeconds=record['response']['elapsedTicks']/record['response']['clockFrequency'],
                       guestSamples=leaves['samples'],unknown=leaves['unknown'],allThreadCpuSeconds=record['threadCpuSeconds'],
                       limitations='Self samples are not removable/causal fractions; inlining, missing frames and ongoing tiering remain.')
result['rawSHA256']=hashlib.sha256((root/'raw-results.tar.gz').read_bytes()).hexdigest()
(root/'summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
for c,item in result['cases'].items():print(c,item['asyncCpuChangesPercent'],'CPU drift',item['cpuControlDriftPercent'],'controls',item['controlBoundsPass'])
