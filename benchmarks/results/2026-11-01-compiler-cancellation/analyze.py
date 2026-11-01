from pathlib import Path
import hashlib,json,shutil,subprocess
root=Path(__file__).resolve().parent;raw=root/'retrieved'
identity=json.loads((raw/'candidate-identity.json').read_text());rows=[]
for mode,case in [('compile','scripts.'+c+'.large') for c in ('csv-summary-helpers','text-summary-helpers')]+[('warm','scripts.text-summary-helpers.large')]:
    reports=[json.loads((raw/f'{phase}-{mode}-{case}.json').read_text()) for phase in ('before','after','before-control')]
    for i,r in enumerate(reports):
        assert r['identities']['Lokad.Lython.dll'].lower()==identity['coreSHA256' if i==1 else 'baselineCoreSHA256']
        assert r['batch']==(4096 if mode=='compile' else 128)
        for name,digest in reports[0]['identities'].items():
            if name!='Lokad.Lython.dll':assert r['identities'][name]==digest
    observations=[r['observations'][0] for r in reports]
    values={metric:[r[metric] for r in observations] for metric in ('medianCpuMillisecondsPerJob','medianMilliseconds','medianAllocatedBytes')}
    pct=lambda a,b:100*(a/b-1)
    cpu=values['medianCpuMillisecondsPerJob'];wall=values['medianMilliseconds'];allocation=values['medianAllocatedBytes']
    rows.append(dict(mode=mode,case=case,measurements=values,cpuChangesPercent=[pct(cpu[1],cpu[i]) for i in (0,2)],
        elapsedChangesPercent=[pct(wall[1],wall[i]) for i in (0,2)],cpuControlDriftPercent=pct(cpu[2],cpu[0]),
        allocationControlDriftPercent=pct(allocation[2],allocation[0]),allCpuBatchesResolved=all(o['cpuGranularityResolved'] for o in observations)))
collection=json.loads((raw/'collection.json').read_text());assert collection['totalWallSeconds']<60 and collection['quietGates'] and collection['allChildrenJoined']
result=dict(decision='Retain cancellation correction; diagnostic costs only, no performance target or speedup claim.',
    performanceQualified=False,rows=rows,collection=collection,
    limitation='The unchanged warm runtime has a material CPU change without elapsed improvement. No runtime gain is inferred. Measured patch precedes three further lookahead checks.',
    rawSHA256=hashlib.sha256((root/'raw-results.tar.gz').read_bytes()).hexdigest())
(root/'summary.json').write_text(json.dumps(result,indent=2)+'\n')
(root/'final-source.patch').write_bytes(subprocess.check_output(['git','diff','--binary','HEAD','--','src']))
out=Path('benchmarks/results/2026-11-01-compiler-cancellation');out.mkdir(exist_ok=False)
for name in ('raw-results.tar.gz','summary.json','analyze.py','collect.py','candidate-identity.json','baseline-identity.json','pre-code-plan.md'):
    shutil.copyfile(root/name,out/name)
import gzip
(out/'final-source.patch.gz').write_bytes(gzip.compress((root/'final-source.patch').read_bytes(),mtime=0))
print(json.dumps(result,indent=2))
