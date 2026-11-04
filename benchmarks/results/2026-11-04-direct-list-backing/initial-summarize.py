from pathlib import Path
import json, statistics
p=Path(__file__).parent
r=json.loads((p/'retrieved/confirmation/confirmation.json').read_text())
out=dict(complete=r['complete'],elapsedSeconds=r.get('elapsedSeconds'),timing=[],allocation={})
for job in r['jobs']:
 labels=['before-control','candidate','after-control']
 groups={label:[s for s in job['samples'] if s['engine']==label] for label in labels}
 values={label:[1000*s['response']['elapsedTicks']/s['response']['clockFrequency']/s['response']['completedInvocations'] for s in rows] for label,rows in groups.items()}
 m={label:statistics.median(rows) for label,rows in values.items()}
 cpu={label:statistics.median([1000*s['externalProcessCpuTicks']/s['externalCpuFrequency']/s['response']['completedInvocations'] for s in rows]) for label,rows in groups.items()}
 row=dict(case=job['case'],lane=job['lane'],medianMilliseconds=m,cpuMilliseconds=cpu,
  iqrPercent={label:(statistics.quantiles(rows,n=4)[2]-statistics.quantiles(rows,n=4)[0])/m[label]*100 for label,rows in values.items()},
  deltaPercent=(m['candidate']/m['before-control']-1)*100,
  versusAfterPercent=(m['candidate']/m['after-control']-1)*100,
  oldControlPercent=(m['after-control']/m['before-control']-1)*100)
 out['timing'].append(row)
 print(row['lane'],row['case'].split('.')[1],*[round(row[k],2) for k in ['deltaPercent','versusAfterPercent','oldControlPercent']])
for api in ['sync','async']:
 report=json.loads((p/f'retrieved/confirmation/allocation/{api}/measurements.json').read_text())
 rows=[]
 for item in report['observations']:
  groups={label:[s['allocatedBytes']/s['jobs'] for s in item['samples'] if s['image']==label] for label in ['before','candidate','before-control']}
  m={label:statistics.median(values) for label,values in groups.items()}
  row=dict(case=item['caseId'],medianBytesPerJob=m,deltaPercent=(m['candidate']/m['before']-1)*100,
   versusControlPercent=(m['candidate']/m['before-control']-1)*100,admittedPeaks=item['admittedPeaks'])
  rows.append(row)
  print('allocation',api,row['case'].split('.')[1],round(row['deltaPercent'],2))
 out['allocation'][api]=rows
(p/'summary.json').write_text(json.dumps(out,indent=2)+'\n')
