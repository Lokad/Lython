from pathlib import Path
import hashlib,json,tarfile
root=Path(__file__).resolve().parent
with tarfile.open(root/'raw-results.tar.gz') as t:t.extractall(root/'retrieved',filter='data')
raw=root/'retrieved/shared-dispatch-b51-20261101'
identity=json.loads((root/'candidate-identity.json').read_text(encoding='utf-8'))
pct=lambda a,b:100*(a/b-1)
result=dict(performanceQualified=False,decision='Rejected: shared instruction loop misses the prospective CPU and elapsed targets; restore original production source.',cases={})
for mode,case in (('warm-async','scripts.csv-summary-helpers.large'),('warm','scripts.csv-summary-helpers.large'),('warm','scripts.text-summary-helpers.large')):
    reports=[json.loads((raw/f'{phase}-{mode}-{case}.json').read_text(encoding='utf-8')) for phase in ('before','candidate','before-control')]
    for i,r in enumerate(reports):
        assert r['identities']['Lokad.Lython.dll'].lower()==identity['coreSHA256' if i==1 else 'baselineCoreSHA256']
        assert r['batch']==64 and r['warmups']==4 and r['rounds']==7
        for name,sha in reports[0]['identities'].items():
            if name!='Lokad.Lython.dll':assert r['identities'][name]==sha
    obs=[r['observations'][0] for r in reports]
    values={m:[o[m] for o in obs] for m in ('medianCpuMillisecondsPerJob','medianMilliseconds','medianAllocatedBytes')}
    cpu=values['medianCpuMillisecondsPerJob'];wall=values['medianMilliseconds'];alloc=values['medianAllocatedBytes']
    item=dict(measurements=values,cpuChangesPercent=[pct(cpu[1],cpu[i]) for i in (0,2)],
              elapsedChangesPercent=[pct(wall[1],wall[i]) for i in (0,2)],
              cpuControlDriftPercent=pct(cpu[2],cpu[0]),elapsedControlDriftPercent=pct(wall[2],wall[0]),
              allocationControlDriftPercent=pct(alloc[2],alloc[0]),allCpuBatchesResolved=all(o['cpuGranularityResolved'] for o in obs))
    item['controlBoundsPass']=abs(item['cpuControlDriftPercent'])<=5 and abs(item['elapsedControlDriftPercent'])<=5 and abs(item['allocationControlDriftPercent'])<=.2
    result['cases'][mode+'/'+case]=item
result['collection']=json.loads((raw/'collection.json').read_text(encoding='utf-8'))
assert result['collection']['totalWallSeconds']<60 and result['collection']['quietGates'] and result['collection']['allChildrenJoined']
assert all(x['controlBoundsPass'] and x['allCpuBatchesResolved'] for x in result['cases'].values())
target=result['cases']['warm-async/scripts.csv-summary-helpers.large']
result['materialityPass']=all(v<=-7.5 for v in target['cpuChangesPercent']) and all(v<=-3 for v in target['elapsedChangesPercent'])
result['synchronousRegressionsPass']=all(all(v<=5 for v in c['cpuChangesPercent']+c['elapsedChangesPercent']) for k,c in result['cases'].items() if k.startswith('warm/'))
assert not result['materialityPass']
result['rawSHA256']=hashlib.sha256((root/'raw-results.tar.gz').read_bytes()).hexdigest()
(root/'summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
for k,c in result['cases'].items():print(k,'CPU',c['cpuChangesPercent'],'elapsed',c['elapsedChangesPercent'],'allocation',c['measurements']['medianAllocatedBytes'])
