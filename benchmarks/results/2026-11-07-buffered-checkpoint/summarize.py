from pathlib import Path
import base64,json,statistics
p=Path(__file__).parent
raw=json.loads((p/'retrieved/confirmation/confirmation.json').read_text(encoding='utf-8'))
assert raw['complete']and len(raw['jobs'])==23
cases={c['id']:c for c in json.loads((p/'representative.json').read_text(encoding='utf-8'))['cases']}
external=[];pairs=[]
for job in raw['jobs']:
 labels=list(job['identities'])
 values={k:[1000*s['response']['elapsedTicks']/s['response']['clockFrequency']/s['response']['completedInvocations']for s in job['samples']if s['engine']==k]for k in labels}
 med={k:statistics.median(v)for k,v in values.items()}
 cpu={k:statistics.median(1000*s['externalProcessCpuTicks']/s['externalCpuFrequency']/s['response']['completedInvocations']for s in job['samples']if s['engine']==k)for k in labels}
 iqr={k:100*(statistics.quantiles(v,n=4)[2]-statistics.quantiles(v,n=4)[0])/med[k]for k,v in values.items()}
 fixture=json.loads(cases[job['case']]['fixtureJson'])
 row=dict(case=job['case'],lane=job['lane'],medianMilliseconds=med,processCpuMedianMilliseconds=cpu,iqrPercent=iqr,inputBytes=sum(len(base64.b64decode(v))for v in fixture['inputFiles'].values()))
 row['controlPercent']=100*abs(med['control']/med['candidate']-1)
 if 'CPython'in labels:
  row.update(ratioRange=sorted(med[k]/med['CPython']for k in ['candidate','control']),timeRangeMilliseconds=sorted(med[k]for k in ['candidate','control']),status='Diagnostic'if row['controlPercent']<2 and max(iqr.values())<3 else'Variable')
  external.append(row)
  print(row['lane'],row['case'],','.join(f'{v:.2f}x'for v in row['ratioRange']),row['status'],flush=True)
 else:
  row.update(referenceControlPercent=100*abs(med['reference-control']/med['reference']-1),changePercent={f'{a}/{b}':100*(med[a]/med[b]-1)for a in ['candidate','control']for b in ['reference','reference-control']})
  pairs.append(row)
  print(row['lane'],row['case'],'source pair',','.join(f'{v:+.2f}%'for v in row['changePercent'].values()),'spreads',round(row['controlPercent'],2),round(row['referenceControlPercent'],2),flush=True)
assert len(external)==23 and len(pairs)==0
identity=json.loads((p/'identity.json').read_text(encoding='utf-8'))
for name,rows in [('summary.json',external)]:
 (p/name).write_text(json.dumps(dict(complete=True,jobs=23,elapsedSeconds=raw['elapsedSeconds'],sourceRevision=identity['sourceRevision'],coreSHA256=identity['coreSHA256'],timing=rows),indent=2)+'\n',encoding='utf-8',newline='\n')
