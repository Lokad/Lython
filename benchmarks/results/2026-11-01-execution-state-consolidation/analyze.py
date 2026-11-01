from pathlib import Path
import hashlib,json,shutil

root=Path(__file__).resolve().parent
raw=root/'retrieved'
identity=json.loads((raw/'candidate-identity.json').read_text())
rows=[]
for case in ('text','csv'):
    reports=[json.loads((raw/f'{phase}-{case}-summary-helpers.large.json').read_text())
             for phase in ('before','after','before-control')]
    for i,report in enumerate(reports):
        expected=identity['candidateCoreSHA256' if i==1 else 'baselineCoreSHA256'].upper()
        assert report['identities']['Lokad.Lython.dll']==expected
        assert report['warmups']==4 and report['rounds']==7 and report['batch']==128
        assert report['observations'][0]['cpuGranularityResolved']
        for name,digest in reports[0]['identities'].items():
            if name!='Lokad.Lython.dll': assert report['identities'][name]==digest
    observations=[v['observations'][0] for v in reports]
    values={metric:[v[metric] for v in observations] for metric in
            ('medianCpuMillisecondsPerJob','medianMilliseconds','medianAllocatedBytes')}
    def pct(a,b): return 100*(a/b-1)
    cpu=values['medianCpuMillisecondsPerJob']; allocation=values['medianAllocatedBytes']
    cpu_drift=abs(pct(cpu[2],cpu[0])); allocation_drift=abs(pct(allocation[2],allocation[0]))
    assert cpu_drift<=5 and allocation_drift<=.2
    rows.append(dict(case=case,measurements=values,cpuControlDriftPercent=cpu_drift,
        allocationControlDriftPercent=allocation_drift,
        candidateCpuChangesPercent=[pct(cpu[1],cpu[i]) for i in (0,2)],
        candidateWallChangesPercent=[pct(values['medianMilliseconds'][1],values['medianMilliseconds'][i]) for i in (0,2)],
        candidateAllocationChangesPercent=[pct(allocation[1],allocation[i]) for i in (0,2)]))
assert any(all(p<=-5 for p in row['candidateCpuChangesPercent']) for row in rows)
assert all(all(p<=5 for p in row['candidateCpuChangesPercent']) and
           all(p<=2 for p in row['candidateAllocationChangesPercent']) for row in rows)
compile_control=json.loads((raw/'compile-control.json').read_text())
assert compile_control['state']=='Completed'
collection=json.loads((raw/'collection.json').read_text())
assert collection['totalWallSeconds']<=90 and collection['quietGates'] and collection['processesJoined']
result=dict(decision='Retain simplification; fixed process-CPU criterion passes. No qualified latency or CPython speedup.',
    boundary='Warm complete public Run, fixture reset/mediated I/O/full goldens/result disposal; compile/startup excluded',
    performanceQualified=False,controlsPass=True,rows=rows,
    compileControl=compile_control['summaries'],collection=collection,
    rawSHA256=hashlib.sha256((root/'raw-results.tar.gz').read_bytes()).hexdigest())
(root/'summary.json').write_text(json.dumps(result,indent=2)+'\n')
out=Path('benchmarks/results/2026-11-01-execution-state-consolidation')
out.mkdir(exist_ok=False)
for name in ('raw-results.tar.gz','summary.json','run-jobs.py','collect.py','analyze.py','candidate-identity.json'):
    shutil.copyfile(root/name,out/name)
(out/'pre-code-plan.md').write_text((root/'pre-code-plan.md').read_text().split('**B44 —')[1].split('Next deliverable:')[0].join(['# B44 —','']),newline='\n')
print(json.dumps(result,indent=2))
