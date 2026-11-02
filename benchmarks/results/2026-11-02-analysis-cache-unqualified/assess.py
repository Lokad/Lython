from pathlib import Path
import json,statistics,hashlib
r=Path(__file__).resolve().parent;d=r/'retrieved'
v=json.loads((d/'comparison.json').read_text(encoding='utf-8'))
res=[];qualified=all(v[k] for k in ['allBalancesZero','allGoldensPassed','imageIsolationVerified','ownedFixturesRemoved'])
for x in v['observations']:
 sets={label:sorted(y['elapsedMilliseconds'] for y in x['samples'] if y['image']==label) for label in ['before','candidate','before-control']}
 med={k:statistics.median(vals) for k,vals in sets.items()}
 spread={k:100*(statistics.quantiles(vals,n=4,method='inclusive')[2]-statistics.quantiles(vals,n=4,method='inclusive')[0])/med[k] for k,vals in sets.items()}
 gains={k:100*(1-med['candidate']/med[k]) for k in ['before','before-control']}
 drift=100*abs(med['before-control']/med['before']-1)
 minimum=min(y for vals in sets.values() for y in vals)
 ok=(min(gains.values())>=10 if x['mode']=='compile' else min(gains.values())>=-3) and drift<=3 and max(spread.values())<=10 and minimum>50
 qualified &= ok
 z=dict(case=x['caseId'],mode=x['mode'],medianBatchMilliseconds=med,iqrPercent=spread,candidateGainPercent=gains,originalDriftPercent=drift,minimumBatchMilliseconds=minimum,passes=ok)
 res.append(z);print(json.dumps(z))
for n in ['machine-before.json','machine-after.json']:
 g=json.loads((d/n).read_text(encoding='utf-8'))['gate'];print(n,'quiet=',g['quiet']);qualified &= g['quiet']
print('QUALIFIED',qualified)
(r/'assessment.json').write_text(json.dumps({'qualified':qualified,'quartiles':'inclusive linear interpolation','observations':res,'rawArchiveSHA256':hashlib.sha256((r/'evidence.tar.gz').read_bytes()).hexdigest()},indent=2)+'\n',encoding='utf-8')
