from pathlib import Path
import hashlib,json,statistics,tarfile
root=Path(__file__).parent;receipt=json.loads((root/'archive-receipt.json').read_text(encoding='utf-8'))
assert hashlib.sha256((root/'observations.tgz').read_bytes()).hexdigest()==receipt['archiveSHA256']
retrieved=root/'retrieved';retrieved.mkdir(exist_ok=True)
with tarfile.open(root/'observations.tgz') as t:t.extractall(retrieved,filter='data')
r=json.loads((retrieved/'confirmation/confirmation.json').read_text(encoding='utf-8'));assert r['complete'] and len(r['jobs'])==5
rows=[]
for j in r['jobs']:
 samples={label:[s for s in j['samples'] if s['engine']==label] for label in ['candidate','control','reference','CPython']}
 values={label:[1000*s['response']['elapsedTicks']/s['response']['clockFrequency']/s['response']['completedInvocations'] for s in ss] for label,ss in samples.items()}
 med={label:statistics.median(v) for label,v in values.items()}
 cpu={label:statistics.median(1000*s['externalProcessCpuTicks']/s['externalCpuFrequency']/s['response']['completedInvocations'] for s in ss) for label,ss in samples.items()}
 row=dict(case=j['case'],lane=j['lane'],medianMilliseconds=med,cpuMilliseconds=cpu,delta={label:100*(med[label]/med['reference']-1) for label in ['candidate','control']},cpuDelta={label:100*(cpu[label]/cpu['reference']-1) for label in ['candidate','control']},ratio={label:med[label]/med['CPython'] for label in ['candidate','control']},controlDifferencePercent=100*(med['control']/med['candidate']-1),iqrPercent={label:100*(statistics.quantiles(v,n=4)[2]-statistics.quantiles(v,n=4)[0])/med[label] for label,v in values.items()})
 rows.append(row);print(row['case'],row['lane'],'delta',{k:round(v,2) for k,v in row['delta'].items()},'control',round(row['controlDifferencePercent'],2),'CPU',{k:round(v,2) for k,v in row['cpuDelta'].items()},'ratio',{k:round(v,3) for k,v in row['ratio'].items()})
result=dict(status='R14 explicit dictionary context; controlled diagnostic source pair',elapsedSeconds=r['elapsedSeconds'],timing=rows,limitations=['No allocation/RSS/embedding-latency measurement','One brief service-status SSH observation during collection; retained, no data excluded','External process CPU has coarse 10ms tick resolution; inclusive useful/native costs overlap'])
(root/'assessment.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
(root/'terminal-status.txt').write_text('MainPID=0\nResult=success\nLoadState=not-found\nActiveState=inactive\nexit-code=0\n',encoding='utf-8')
