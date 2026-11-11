from pathlib import Path
import json,statistics
r=Path(__file__).parent
raw=json.loads((r/'retrieved/confirmation/confirmation.json').read_text(encoding='utf-8'))
assert raw['complete']and len(raw['jobs'])==6
rows=[]
for job in raw['jobs']:
 labels=list(job['identities'])
 assert set(labels)=={'reference','reference-control','candidate','candidate-control'}
 values={k:[1000*s['response']['elapsedTicks']/s['response']['clockFrequency']/s['response']['completedInvocations']for s in job['samples']if s['engine']==k]for k in labels}
 med={k:statistics.median(v)for k,v in values.items()}
 cpu={k:statistics.median(1000*s['externalProcessCpuTicks']/s['externalCpuFrequency']/s['response']['completedInvocations']for s in job['samples']if s['engine']==k)for k in labels}
 iqr={k:100*(statistics.quantiles(v,n=4)[2]-statistics.quantiles(v,n=4)[0])/med[k]for k,v in values.items()}
 changes=lambda values:sorted(100*(values[a]/values[b]-1)for a in ['candidate','candidate-control']for b in ['reference','reference-control'])
 row=dict(case=job['case'],lane=job['lane'],medianMilliseconds=med,processCpuMedianMilliseconds=cpu,iqrPercent=iqr,
     changePercentRange=changes(med),processCpuChangePercentRange=changes(cpu),referenceControlPercent=100*abs(med['reference-control']/med['reference']-1),candidateControlPercent=100*abs(med['candidate-control']/med['candidate']-1))
 rows.append(row)
 print(row['case'],row['lane'],'elapsed',*[round(v,2)for v in row['changePercentRange']],'wider CPU',*[round(v,2)for v in row['processCpuChangePercentRange']],'controls',round(row['referenceControlPercent'],2),round(row['candidateControlPercent'],2),'maxIQR',round(max(iqr.values()),2),flush=True)
identity=json.loads((r/'identity.json').read_text(encoding='utf-8'))
(r/'summary.json').write_text(json.dumps(dict(complete=True,jobs=6,elapsedSeconds=raw['elapsedSeconds'],sourceRevision=identity['sourceRevision'],coreSHA256=identity['coreSHA256'],timing=rows,externalBaseline=False),indent=2)+'\n',encoding='utf-8')
