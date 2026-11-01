from pathlib import Path
import gzip,hashlib,json,shutil
root=Path(__file__).resolve().parent;raw=root/'retrieved'
identity=json.loads((raw/'candidate-identity.json').read_text())
reports=[json.loads((raw/f'{p}-warm-scripts.csv-summary-helpers.large.json').read_text()) for p in ('before','after','before-control')]
for i,r in enumerate(reports):
    assert r['identities']['Lokad.Lython.dll'].lower()==identity['coreSHA256' if i==1 else 'baselineCoreSHA256']
    assert r['batch']==128 and r['warmups']==4 and r['rounds']==7
    for name,digest in reports[0]['identities'].items():
        if name!='Lokad.Lython.dll':assert r['identities'][name]==digest
observations=[r['observations'][0] for r in reports]
values={m:[o[m] for o in observations] for m in ('medianCpuMillisecondsPerJob','medianMilliseconds','medianAllocatedBytes')}
pct=lambda a,b:100*(a/b-1)
cpu=values['medianCpuMillisecondsPerJob'];alloc=values['medianAllocatedBytes'];wall=values['medianMilliseconds']
collection=json.loads((raw/'collection.json').read_text())
assert collection['totalWallSeconds']<40 and collection['quietGates'] and collection['allChildrenJoined']
result=dict(decision='Retain architectural deletion and semantic correction; possible warm CPU cost remains visible, no speedup claim.',
    performanceQualified=False,measurements=values,cpuChangesPercent=[pct(cpu[1],cpu[i]) for i in (0,2)],
    elapsedChangesPercent=[pct(wall[1],wall[i]) for i in (0,2)],cpuControlDriftPercent=pct(cpu[2],cpu[0]),
    allocationControlDriftPercent=pct(alloc[2],alloc[0]),allCpuBatchesResolved=all(o['cpuGranularityResolved'] for o in observations),
    collection=collection,rawSHA256=hashlib.sha256((root/'raw-results.tar.gz').read_bytes()).hexdigest())
result['controlBoundsPass']=abs(result['cpuControlDriftPercent'])<=5 and abs(result['allocationControlDriftPercent'])<=.2
assert result['controlBoundsPass'] and result['allCpuBatchesResolved']
(root/'summary.json').write_text(json.dumps(result,indent=2)+'\n')
out=Path('benchmarks/results/2026-11-01-expression-route');out.mkdir(exist_ok=False)
for n in ('raw-results.tar.gz','summary.json','analyze.py','collect.py','candidate-identity.json','baseline-identity.json','pre-code-plan.md','goldens.json','before-cases.json','before-probes.jsonl'):
    shutil.copyfile(root/n,out/n)
(out/'source.patch.gz').write_bytes(gzip.compress((root/'source.patch').read_bytes(),mtime=0))
(out/'before-tests.log.gz').write_bytes(gzip.compress((root/'before-tests.log').read_bytes(),mtime=0))
print(json.dumps(result,indent=2))
