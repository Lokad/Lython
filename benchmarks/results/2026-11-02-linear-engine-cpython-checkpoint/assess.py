import json,pathlib,statistics
root=pathlib.Path(__file__).parent
data=json.loads((root/'retrieved/confirmation/confirmation.json').read_text())
assert data['complete'] and len(data['jobs'])==12
rows=[]
for job in data['jobs']:
    assert job['workersJoinedAndFixturesRemoved']
    values={}
    for label in ['baseline','candidate','CPython']:
        samples=[s['response']['elapsedTicks']/s['response']['clockFrequency']*1000/s['response']['completedInvocations']
            for s in job['samples'] if s['engine']==label]
        q=statistics.quantiles(samples,n=4,method='inclusive')
        values[label]={'medianMilliseconds':statistics.median(samples),'iqrPercent':100*(q[2]-q[0])/statistics.median(samples)}
    row={'case':job['case'],'lane':job['lane'],'values':values,
        'LythonOverCPython':values['candidate']['medianMilliseconds']/values['CPython']['medianMilliseconds'],
        'unchangedControlPercent':100*(values['baseline']['medianMilliseconds']/values['candidate']['medianMilliseconds']-1)}
    rows.append(row);print(json.dumps(row))
(root/'assessment.json').write_text(json.dumps(rows,indent=2))
