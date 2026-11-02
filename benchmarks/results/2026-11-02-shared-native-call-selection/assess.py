import json,pathlib,statistics
root=pathlib.Path(__file__).parent
rows=[]
for phase in ['confirmation','csv-cpu-confirmation']:
    path=root/'retrieved'/phase/'confirmation.json'
    if not path.exists():continue
    data=json.loads(path.read_text())
    assert data['complete']
    for job in data['jobs']:
        assert job['workersJoinedAndFixturesRemoved']
        values={}
        for label in {s['engine'] for s in job['samples']}:
            samples=[s for s in job['samples'] if s['engine']==label]
            elapsed=[s['response']['elapsedTicks']/s['response']['clockFrequency']*1000/s['response']['completedInvocations'] for s in samples]
            q=statistics.quantiles(elapsed,n=4,method='inclusive')
            value={'medianMilliseconds':statistics.median(elapsed),'iqrPercent':100*(q[2]-q[0])/statistics.median(elapsed)}
            if 'externalProcessCpuTicks' in samples[0]:
                ticks=sum(s['externalProcessCpuTicks'] for s in samples)
                jobs=sum(s['response']['completedInvocations'] for s in samples)
                value.update(weightedCpuMilliseconds=ticks/samples[0]['externalCpuFrequency']*1000/jobs,totalCpuTicks=ticks,completedInvocations=jobs)
            values[label]=value
        candidate=values['candidate']
        result={'phase':phase,'block':job['block'],'case':job['case'],'values':values,
            'elapsedDeltaPercent':100*(candidate['medianMilliseconds']/values['baseline']['medianMilliseconds']-1),
            'controlElapsedDeltaPercent':100*(values['before-control']['medianMilliseconds']/values['baseline']['medianMilliseconds']-1)}
        if 'weightedCpuMilliseconds' in candidate:
            result.update(cpuDeltaPercent=100*(candidate['weightedCpuMilliseconds']/values['baseline']['weightedCpuMilliseconds']-1),
                currentCpuControlPercent=100*(values['before-control']['weightedCpuMilliseconds']/values['baseline']['weightedCpuMilliseconds']-1),
                legacyCpuDeltaPercent=100*(candidate['weightedCpuMilliseconds']/values['legacy']['weightedCpuMilliseconds']-1),
                legacyCpuControlPercent=100*(values['legacy-control']['weightedCpuMilliseconds']/values['legacy']['weightedCpuMilliseconds']-1))
        elif 'legacy' in values:result['legacyElapsedDeltaPercent']=100*(candidate['medianMilliseconds']/values['legacy']['medianMilliseconds']-1)
        rows.append(result)
        print(phase,job['block'],job['case'],json.dumps({k:round(v,3) for k,v in result.items() if k.endswith('Percent')}))
(root/'assessment.json').write_text(json.dumps(rows,indent=2))
