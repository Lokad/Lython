from pathlib import Path
import hashlib,json,statistics
r=Path(__file__).parent
identity=json.loads((r/'identity.json').read_text(encoding='utf-8'))
rows=[]
for job in ['sync-a','async-a','async-b','sync-b']:
 raw=json.loads((r/'retrieved/output'/job/'measurements.json').read_text(encoding='utf-8'))
 assert all(raw[n]for n in ['allGoldensPassed','allBalancesZero','ownedFixturesRemoved','imageIsolationVerified','normalGcAndTiering'])
 assert raw['catalogSHA256'].lower()==identity['catalogSHA256']
 for case in raw['observations']:
  for role,values in case['identities'].items():
   assert values['Lokad.Lython.dll'].lower()==identity['coreSHA256']['candidate'if role=='candidate'else'reference']
  roles={role:{key:statistics.median(s[field]/s['jobs']for s in case['samples']if s['image']==role)
   for key,field in [('runOnlyMs','executionMilliseconds'),('fullEnvelopeMs','elapsedMilliseconds'),('widerProcessCpuMs','cpuMilliseconds'),('allocatedBytes','allocatedBytes')]}
   for role in ['before','before-control','candidate']}
  changes={control:{key:100*(roles['candidate'][key]/roles[control][key]-1)for key in roles[control]}for control in ['before','before-control']}
  spreads={key:100*(roles['before-control'][key]/roles['before'][key]-1)for key in roles['before']}
  row=dict(job=job,case=case['caseId'],medians=roles,candidateChangesPercent=changes,unchangedControlSpreadPercent=spreads)
  rows.append(row)
  print(job,case['caseId'].split('.')[1], 'Run',*[round(changes[c]['runOnlyMs'],2)for c in changes],
        'envelope',*[round(changes[c]['fullEnvelopeMs'],2)for c in changes],'control',round(spreads['runOnlyMs'],2))
summary=dict(complete=True,jobs=4,rows=rows,archiveSHA256=hashlib.sha256((r/'observations.tgz').read_bytes()).hexdigest(),
 boundary='Warm Run/awaited RunAsync includes mediated I/O and projection; excludes startup, compilation, fixture reset, validation and result disposal. Full envelope adds reset/validation/result disposal. No real async suspension. CPU is whole-process shared CoreLib/GC/JIT, not isolated guest CPU.',
 limits='Unchanged short driver retained. All rounds retained, including pronounced early tier/JIT activity and unstable identical controls. Ranges are pairings of process medians, not confidence intervals or steady-state qualification.',
 sourcePromotion=False)
(r/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8',newline='\n')
