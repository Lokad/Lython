import json,statistics
from pathlib import Path
r=Path(__file__).parent
q=lambda a,p: sorted(a)[int((len(a)-1)*p)]
result={}
for api in ['sync','async']:
 v=json.loads((r/'retrieved/observations'/api/'measurements.json').read_text())
 rows=[]
 for o in v['observations']:
  images={}
  for label in ['before','candidate','before-control']:
   a=[s for s in o['samples'] if s['image']==label]
   elapsed=[s['elapsedMilliseconds']/s['jobs'] for s in a]
   images[label]={'elapsedMs':statistics.median(elapsed),'elapsedIqrPercent':100*(q(elapsed,.75)-q(elapsed,.25))/statistics.median(elapsed),'cpuMs':statistics.median(s['cpuMilliseconds']/s['jobs'] for s in a),'runOnlyMs':statistics.median(s['executionMilliseconds']/s['jobs'] for s in a),'allocatedBytes':statistics.median(s['allocatedBytes']/s['jobs'] for s in a)}
  delta={label:{k:100*(images[label][k]/images['before'][k]-1) for k in ['elapsedMs','cpuMs','runOnlyMs','allocatedBytes']} for label in ['candidate','before-control']}
  rows.append({'case':o['caseId'],'values':images,'changePercent':delta,'admittedPeaks':o['admittedPeaks'],'allGoldensPassed':o['allGoldensPassed'],'allBalancesZero':o['allBalancesZero']})
 result[api]=rows
(r/'assessment.json').write_text(json.dumps(result,indent=2)+'\n')
for api,rows in result.items():
 print(api)
 for row in rows:
  a=row['values']; d=row['changePercent']
  print(row['case'],*[round(a[k]['elapsedMs'],5) for k in ['before','candidate','before-control']],'elapsed/cpu/alloc delta',*[round(d['candidate'][k],3) for k in ['elapsedMs','cpuMs','allocatedBytes']],'controls',*[round(d['before-control'][k],3) for k in ['elapsedMs','cpuMs','allocatedBytes']])
