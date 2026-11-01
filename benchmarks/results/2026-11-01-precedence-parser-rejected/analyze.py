from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parent;raw=root/'retrieved'
rows=[]
for mode,case in [('compile','scripts.'+c+'.large') for c in ('csv-summary-helpers','text-summary-helpers','config-edit')]+[('compile-run','scripts.config-edit.large'),('warm','scripts.text-summary-helpers.large')]:
    reports=[json.loads((raw/f'{phase}-{mode}-{case}.json').read_text()) for phase in ('before','after','before-control')]
    identity=json.loads((raw/'candidate-identity.json').read_text())
    for i,r in enumerate(reports):
        assert r['identities']['Lokad.Lython.dll'].lower()==identity['coreSHA256' if i==1 else 'baselineCoreSHA256']
        assert r['batch']==(4096 if mode=='compile' else 128)
        for name,digest in reports[0]['identities'].items():
            if name!='Lokad.Lython.dll':assert r['identities'][name]==digest
    observations=[r['observations'][0] for r in reports]
    values={metric:[r[metric] for r in observations] for metric in ('medianCpuMillisecondsPerJob','medianMilliseconds','medianAllocatedBytes')}
    pct=lambda a,b:100*(a/b-1)
    cpu=values['medianCpuMillisecondsPerJob'];allocation=values['medianAllocatedBytes']
    rows.append(dict(mode=mode,case=case,measurements=values,cpuChangesPercent=[pct(cpu[1],cpu[i]) for i in (0,2)],
        cpuControlDriftPercent=pct(cpu[2],cpu[0]),allocationControlDriftPercent=pct(allocation[2],allocation[0]),
        allCpuBatchesResolved=all(o['cpuGranularityResolved'] for o in observations)))
successes=[r for r in rows if r['mode']=='compile' and all(p<=-10 for p in r['cpuChangesPercent'])]
result=dict(decision='Reject and restore parser: only one of three compilation cases meets >=10% CPU target; fresh-config control drift invalid.',
    performanceQualified=False,qualifiedCompilationCases=len(successes),rows=rows,
    collection=json.loads((raw/'collection.json').read_text()),
    rawSHA256=hashlib.sha256((root/'raw-results.tar.gz').read_bytes()).hexdigest())
(root/'summary.json').write_text(json.dumps(result,indent=2)+'\n')
out=Path('benchmarks/results/2026-11-01-precedence-parser-rejected');out.mkdir(exist_ok=False)
for name in ('raw-results.tar.gz','summary.json','analyze.py','collect.py','candidate-identity.json','baseline-identity.json','pre-code-plan.md','pre-measurement-counts.md'):
    shutil.copyfile(root/name,out/name)
print(json.dumps({k:v for k,v in result.items() if k!='collection'},indent=2))
