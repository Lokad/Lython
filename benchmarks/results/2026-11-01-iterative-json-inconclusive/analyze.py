from pathlib import Path
import hashlib,json,tarfile
root=Path(__file__).resolve().parent
with tarfile.open(root/'raw-results.tar.gz') as t:t.extractall(root/'retrieved',filter='data')
raw=root/'retrieved/iterative-json-b53-20261101'
identity=json.loads((root/'candidate-identity.json').read_text(encoding='utf-8'))
pct=lambda a,b:100*(a/b-1)
result=dict(performanceQualified=False,decision='Inconclusive comparison: fixed60s deadline, missing final controls and large numeric process-CPU drift. Remove prototype; retain independent tests.',cases={})
for case in ('scripts.numeric-report.integer.large','scripts.numeric-report.float.large','scripts.json-summary-helpers.large','scripts.csv-summary-helpers.large'):
    reports=[]
    for phase in ('before','candidate','before-control'):
        p=raw/f'{phase}-{case}.json'
        if not p.exists():reports.append(None);continue
        r=json.loads(p.read_text(encoding='utf-8'))
        assert r['identities']['Lokad.Lython.dll'].lower()==identity['coreSHA256' if phase=='candidate' else 'baselineCoreSHA256']
        assert r['warmups']==128 and r['rounds']==7
        assert r['batch']==(256 if 'numeric-report.' in case else 64)
        reports.append(r)
    obs=[None if r is None else r['observations'][0] for r in reports]
    values={m:[None if o is None else o[m] for o in obs] for m in ('medianCpuMillisecondsPerJob','medianMilliseconds','medianAllocatedBytes')}
    cpu=values['medianCpuMillisecondsPerJob'];wall=values['medianMilliseconds'];alloc=values['medianAllocatedBytes']
    item=dict(measurements=values,allControlsPresent=all(o is not None for o in obs),allSavedCpuBatchesResolved=all(o['cpuGranularityResolved'] for o in obs if o is not None),
              cpuChangesPercent=[pct(cpu[1],cpu[i]) if cpu[i] is not None else None for i in (0,2)],
              elapsedChangesPercent=[pct(wall[1],wall[i]) if wall[i] is not None else None for i in (0,2)])
    if cpu[2] is not None:
        item.update(cpuControlDriftPercent=pct(cpu[2],cpu[0]),elapsedControlDriftPercent=pct(wall[2],wall[0]),allocationControlDriftPercent=pct(alloc[2],alloc[0]))
        item['controlBoundsPass']=abs(item['cpuControlDriftPercent'])<=5 and abs(item['elapsedControlDriftPercent'])<=5 and abs(item['allocationControlDriftPercent'])<=.2
    else:item['controlBoundsPass']=False
    result['cases'][case]=item
result['partialCollection']=json.loads((raw/'collection.json').read_text(encoding='utf-8'))
assert len(result['partialCollection']['processes'])==10
assert not (raw/'machine-after.json').exists()
result['postQuietGatePresent']=False
result['ownedService']='Failed/exit-code at60.083s; MainPID0, empty ControlGroup verified separately. Timeout child killed/reaped; owned fixture content/path/UID verified before removal; lease free.'
result['rawSHA256']=hashlib.sha256((root/'raw-results.tar.gz').read_bytes()).hexdigest()
(root/'summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
for name,c in result['cases'].items():print(name,'CPU drift',c.get('cpuControlDriftPercent'),'elapsed',c['elapsedChangesPercent'],'complete',c['allControlsPresent'])
