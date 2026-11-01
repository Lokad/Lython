from pathlib import Path
import hashlib,json
root=Path(__file__).resolve().parent;raw=root/'retrieved/shared-process-control-b55-20261101'
v=json.loads((raw/'control.json').read_text(encoding='utf-8'))
assert (raw/'exit-code.txt').read_text().strip()=='0'
for n in ('imageIsolationVerified','normalGcAndTiering','allGoldensPassed','bothBalancesZero','ownedFixturesRemoved'):assert v[n]
assert all(x is None for x in v['runtimeOverrides'].values()) and not v['serverGc']
assert v['identicalFiles']['Lokad.Lython.dll'].lower()=='6e51564be11be5702d215c3e1022fc114832aa0ce1a706a9f699de294203df02'
assert all(json.loads((raw/n).read_text())['gate']['quiet'] for n in ('machine-before.json','machine-after.json'))
series={}
for name in ('first','second'):
    samples=[s for s in v['samples'] if s['image']==name]
    assert len(samples)==7 and all(s['jobs']==32 for s in samples)
    values=sorted(s['elapsedMilliseconds']/s['jobs'] for s in samples)
    series[name]=dict(medianMilliseconds=values[3],relativeIqr=(values[4]+values[5]-values[1]-values[2])/(2*values[3]),allBatchMilliseconds=[s['elapsedMilliseconds'] for s in samples])
delta=100*(series['second']['medianMilliseconds']/series['first']['medianMilliseconds']-1)
result=dict(performanceQualified=False,decision='One private same-process identical-image control passes; supports a bounded internal trial, not causality or external throughput.',
            series=series,elapsedDifferencePercent=delta,medianBoundPass=abs(delta)<=5,iqrBoundsPass=all(s['relativeIqr']<=.1 for s in series.values()),
            allBatchesResolved=all(t>100 for s in series.values() for t in s['allBatchMilliseconds']),allGoldensAndOwnershipGates=True,quietGates=True,
            serviceSeconds=9.998,hashWitness=v['hashWitness'],rawSHA256=hashlib.sha256((root/'raw-results.tar.gz').read_bytes()).hexdigest(),
            limitations='Two isolated images coexist with shared CoreLib/GC/hashing. Matching hashes do not identify the cause of B54; this one control does not establish broad repeatability or a CPython ratio.')
assert result['medianBoundPass'] and result['iqrBoundsPass'] and result['allBatchesResolved']
(root/'summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('Control difference percent',delta,'relative IQR',[s['relativeIqr'] for s in series.values()])
