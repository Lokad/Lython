from pathlib import Path
import json,statistics
p=Path(__file__).resolve().parent
r=json.loads((p/'retrieved/confirmation/confirmation.json').read_text())
out=dict(complete=r['complete'],elapsedSeconds=r['elapsedSeconds'],timing=[],allocation={})
for job in r['jobs']:
 labels=['before-control','current','candidate','after-control']
 groups={k:[s for s in job['samples'] if s['engine']==k] for k in labels}
 values={k:[1000*s['response']['elapsedTicks']/s['response']['clockFrequency']/s['response']['completedInvocations'] for s in v] for k,v in groups.items()}
 med={k:statistics.median(v) for k,v in values.items()}
 cpu={k:statistics.median([1000*s['externalProcessCpuTicks']/s['externalCpuFrequency']/s['response']['completedInvocations'] for s in v]) for k,v in groups.items()}
 row=dict(case=job['case'],lane=job['lane'],medianMilliseconds=med,cpuMilliseconds=cpu,
  iqrPercent={k:100*(statistics.quantiles(v,n=4)[2]-statistics.quantiles(v,n=4)[0])/med[k] for k,v in values.items()},
  candidateVsOldPercent=100*(med['candidate']/med['before-control']-1),candidateVsAfterPercent=100*(med['candidate']/med['after-control']-1),candidateVsCurrentPercent=100*(med['candidate']/med['current']-1),
  currentVsOldPercent=100*(med['current']/med['before-control']-1),currentVsAfterPercent=100*(med['current']/med['after-control']-1),oldControlPercent=100*(med['after-control']/med['before-control']-1))
 out['timing'].append(row)
 print(job['lane'],job['case'].split('.')[1],{k:round(row[k],2) for k in ['candidateVsOldPercent','candidateVsAfterPercent','candidateVsCurrentPercent','currentVsOldPercent','currentVsAfterPercent','oldControlPercent']})
for api in ['sync','async']:
 a=json.loads((p/f'retrieved/confirmation/allocation/{api}/measurements.json').read_text())
 rows=[]
 for item in a['observations']:
  groups={k:[s['allocatedBytes']/s['jobs'] for s in item['samples'] if s['image']==k] for k in ['before','candidate','before-control']}
  med={k:statistics.median(v) for k,v in groups.items()}
  row=dict(case=item['caseId'],medianBytesPerJob=med,deltaPercent=100*(med['candidate']/med['before']-1),versusControlPercent=100*(med['candidate']/med['before-control']-1),admittedPeaks=item['admittedPeaks'])
  rows.append(row)
  print('allocation',api,row['case'].split('.')[1],round(row['deltaPercent'],2),round(row['versusControlPercent'],2))
 out['allocation'][api]=rows
(p/'summary.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8',newline='\n')
