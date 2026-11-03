import json,pathlib,statistics
root=pathlib.Path(__file__).parent
data=json.loads((root/'retrieved/allocation-confirmation/observations.json').read_text())
assert all(data[x] for x in ['allGoldensPassed','allBalancesZero','ownedFixturesRemoved','imageIsolationVerified','sharedCoreLib'])
rows=[]
for observation in data['observations']:
    values={}
    for image in ['before','candidate','before-control']:
        samples=[s for s in observation['samples'] if s['image']==image]
        values[image]={'medianAllocatedBytes':statistics.median(s['allocatedBytes']/s['jobs'] for s in samples),
            'diagnosticMedianElapsedMs':statistics.median(s['elapsedMilliseconds']/s['jobs'] for s in samples),
            'diagnosticMedianProcessCpuMs':statistics.median(s['cpuMilliseconds']/s['jobs'] for s in samples)}
    rows.append({'case':observation['caseId'],'images':values,
        'allocationPercent':100*(values['candidate']['medianAllocatedBytes']/values['before']['medianAllocatedBytes']-1),
        'controlAllocationPercent':100*(values['before-control']['medianAllocatedBytes']/values['before']['medianAllocatedBytes']-1)})
for row in rows:print(row['case'],row['allocationPercent'],row['controlAllocationPercent'],row['images'])
(root/'allocation-assessment.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8',newline='\n')
