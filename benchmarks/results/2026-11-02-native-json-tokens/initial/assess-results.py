import json,pathlib,statistics
root=pathlib.Path(__file__).parent
data=json.loads((root/'retrieved/confirmation/confirmation.json').read_text())
assert data['complete'] and len(data['jobs'])==8
rows=[]
for job in data['jobs']:
    assert job['workersJoinedAndFixturesRemoved']
    values={}
    for label in {s['engine'] for s in job['samples']}:
        samples=[s for s in job['samples'] if s['engine']==label]
        elapsed=[s['response']['elapsedTicks']/s['response']['clockFrequency']*1000/s['response']['completedInvocations'] for s in samples]
        q=statistics.quantiles(elapsed,n=4,method='inclusive')
        ticks=sum(s['externalProcessCpuTicks'] for s in samples)
        jobs=sum(s['response']['completedInvocations'] for s in samples)
        values[label]={'medianMilliseconds':statistics.median(elapsed),'iqrPercent':100*(q[2]-q[0])/statistics.median(elapsed),
            'weightedCpuMilliseconds':ticks/samples[0]['externalCpuFrequency']*1000/jobs,'totalCpuTicks':ticks,'completedInvocations':jobs}
    candidate=values['candidate'];baseline=values['baseline'];control=values['before-control']
    deltas={name:100*(candidate[metric]/baseline[metric]-1) for name,metric in [('elapsedPercent','medianMilliseconds'),('cpuPercent','weightedCpuMilliseconds')]}
    deltas.update({name:100*(control[metric]/baseline[metric]-1) for name,metric in [('controlElapsedPercent','medianMilliseconds'),('controlCpuPercent','weightedCpuMilliseconds')]})
    if 'legacy' in values:
        deltas['legacyCpuPercent']=100*(candidate['weightedCpuMilliseconds']/values['legacy']['weightedCpuMilliseconds']-1)
        deltas['legacyControlCpuPercent']=100*(values['legacy-control']['weightedCpuMilliseconds']/values['legacy']['weightedCpuMilliseconds']-1)
    row={'block':job['block'],'case':job['case'],'lane':job['lane'],'values':values,'deltas':deltas};rows.append(row)
    print(job['block'],job['case'],job['lane'],json.dumps({k:round(v,3) for k,v in deltas.items()}))
(root/'assessment.json').write_text(json.dumps(rows,indent=2))
