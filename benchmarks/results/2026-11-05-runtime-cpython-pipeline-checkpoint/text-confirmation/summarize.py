from pathlib import Path
import json,statistics
p=Path(__file__).parent
r=json.loads((p/'retrieved/confirmation/confirmation.json').read_text(encoding='utf-8'))
summary=dict(complete=r['complete'],elapsedSeconds=r.get('elapsedSeconds'),timing=[])
for j in r['jobs']:
 labels=['candidate','control','CPython','reference']
 rows={label:[s for s in j['samples'] if s['engine']==label] for label in labels}
 vals={label:[1000*s['response']['elapsedTicks']/s['response']['clockFrequency']/s['response']['completedInvocations'] for s in samples] for label,samples in rows.items()}
 med={label:statistics.median(v) for label,v in vals.items()}
 row=dict(case=j['case'],lane=j['lane'],medianMilliseconds=med,ratio=med['candidate']/med['CPython'],referenceDeltaPercent=(med['candidate']/med['reference']-1)*100,controlPercent=100*(med['control']/med['candidate']-1),iqrPercent={label:100*(statistics.quantiles(v,n=4)[2]-statistics.quantiles(v,n=4)[0])/med[label] for label,v in vals.items()})
 summary['timing'].append(row)
 print(row['lane'],row['case'].split('.')[1],*[round(row[k],3) for k in ['ratio','controlPercent']], 'IQR', {k:round(v,2) for k,v in row['iqrPercent'].items()})
(p/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8',newline='\n')
