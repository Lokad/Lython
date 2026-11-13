from pathlib import Path
import json,statistics
r=Path(__file__).parent
raw=json.loads((r/'retrieved/confirmation/confirmation.json').read_text(encoding='utf-8'));assert raw['complete'] and len(raw['jobs'])==6
rows=[]
for job in raw['jobs']:
 labels=list(job['identities'])
 values={k:[1000*s['response']['elapsedTicks']/s['response']['clockFrequency']/s['response']['completedInvocations']for s in job['samples']if s['engine']==k]for k in labels}
 med={k:statistics.median(v)for k,v in values.items()}
 cpu={k:statistics.median(1000*s['externalProcessCpuTicks']/s['externalCpuFrequency']/s['response']['completedInvocations']for s in job['samples']if s['engine']==k)for k in labels}
 iqr={k:100*(statistics.quantiles(v,n=4)[2]-statistics.quantiles(v,n=4)[0])/med[k]for k,v in values.items()}
 ratios=sorted(med[a]/med[b]for a in ['candidate','candidate-control']for b in ['CPython','CPython-control'])
 row=dict(case=job['case'],lane=job['lane'],medianMilliseconds=med,processCpuMedianMilliseconds=cpu,ratioRange=ratios,iqrPercent=iqr,lythonControlPercent=100*abs(med['candidate-control']/med['candidate']-1),cpythonControlPercent=100*abs(med['CPython-control']/med['CPython']-1),maxIqrPercent=max(iqr.values()))
 if 'reference' in med:
  row.update(lythonSourcePairElapsedPercent=sorted(100*(med[a]/med[b]-1)for a in ['candidate','candidate-control']for b in ['reference','reference-control']),lythonSourcePairWiderCpuPercent=sorted(100*(cpu[a]/cpu[b]-1)for a in ['candidate','candidate-control']for b in ['reference','reference-control']),referenceControlPercent=100*abs(med['reference-control']/med['reference']-1))
 rows.append(row)
 print(json.dumps(row),flush=True)
identity=json.loads((r/'identity.json').read_text(encoding='utf-8'))
(r/'summary.json').write_text(json.dumps(dict(complete=True,jobs=6,elapsedSeconds=raw['elapsedSeconds'],identity=identity,timing=rows,externalBaseline='CPython3.13.16',performanceQualified=False,stabilityMetricsAreNotDesignVetoes=True),indent=2)+'\n',encoding='utf-8',newline='\n')
