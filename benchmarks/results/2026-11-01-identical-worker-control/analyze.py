from pathlib import Path
import hashlib,json
root=Path(__file__).resolve().parent;raw=root/'retrieved/short-control-b54-20261101'
v=json.loads((raw/'micro.json').read_text(encoding='utf-8'))
assert v['state']=='Completed' and (raw/'exit-code.txt').read_text().strip()=='0'
left=v['identities']['baseline'];right=v['identities']['candidate']
assert left['processId']!=right['processId']
assert left['libraries']==right['libraries']
assert all(x is None for x in left['runtimeOverrides'].values()) and left['runtimeOverrides']==right['runtimeOverrides']
assert not left['serverGc'] and not right['serverGc']
assert all(json.loads((raw/n).read_text())['gate']['quiet'] for n in ('machine-before.json','machine-after.json'))
rows={r['engine']:r for r in v['summaries']}
for engine in ('baseline','candidate','CPython'):
    series=[a for a in v['attempts'] if a['engine']==engine and a['phase']=='measure']
    assert len(series)==7 and all(a['response']['status']=='Completed' for a in series)
delta=100*(rows['candidate']['medianMicroseconds']/rows['baseline']['medianMicroseconds']-1)
result=dict(performanceQualified=False,decision='Failed identical-binary stability gate; no retiming or speedup claim.',
            identicalCoreAndHostBits=True,normalWorkerTieringAndGc=True,summaries=v['summaries'],
            identicalWorkersElapsedDifferencePercent=delta,medianBoundPass=abs(delta)<=5,
            iqrBoundsPass=all(rows[n]['iqrFraction']<=.1 for n in ('baseline','candidate')),
            fullGoldensAndSevenRounds=True,quietGates=True,ownedServiceSeconds=9.020,
            rawSHA256=hashlib.sha256((root/'raw-results.tar.gz').read_bytes()).hexdigest(),
            limitations='Independent processes may differ in hash distribution, JIT/PGO, scheduling, caches or I/O. Source establishes possible hash variation, not the cause of this observation.')
assert not result['medianBoundPass'] and result['iqrBoundsPass']
(root/'summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2))
