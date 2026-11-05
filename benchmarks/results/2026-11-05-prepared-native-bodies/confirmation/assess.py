from pathlib import Path
import datetime, hashlib, json, statistics, tarfile

root = Path(__file__).resolve().parent
out = root / 'retrieved'; out.mkdir(exist_ok=True)
with tarfile.open(root / 'observations.tgz') as archive:
    archive.extractall(out, filter='data')
data = json.loads((out / 'comparison.json').read_text(encoding='utf-8-sig'))
assert all(data[k] for k in ('allGoldensPassed', 'allBalancesZero', 'ownedFixturesRemoved', 'imageIsolationVerified'))
assert not data['serverGc'] and not any(data['runtimeOverrides'].values())
rows = []
for observation in data['observations']:
    sets = {label: [s for s in observation['samples'] if s['image'] == label]
            for label in ('before', 'candidate', 'before-control')}
    metrics = {}
    for label, samples in sets.items():
        times = [s['elapsedMilliseconds'] / s['jobs'] for s in samples]
        q1, _, q3 = statistics.quantiles(times, n=4, method='inclusive')
        median = statistics.median(times)
        metrics[label] = dict(milliseconds=median, iqrPercent=100 * (q3 - q1) / median,
                              processAllocatedBytes=statistics.median(s['allocatedBytes'] / s['jobs'] for s in samples),
                              minimumBatchMilliseconds=min(s['elapsedMilliseconds'] for s in samples))
    before, candidate, control = (metrics[k] for k in ('before', 'candidate', 'before-control'))
    changes = [100 * (candidate['milliseconds'] / old['milliseconds'] - 1) for old in (before, control)]
    allocations = [100 * (candidate['processAllocatedBytes'] / old['processAllocatedBytes'] - 1) for old in (before, control)]
    row = dict(case=observation['caseId'], phase=observation['mode'], metrics=metrics,
               candidateElapsedChangePercent=changes, candidateProcessAllocationChangePercent=allocations,
               unchangedSpreadPercent=100 * abs(control['milliseconds'] / before['milliseconds'] - 1))
    row['admittedPeaks'] = observation['admittedPeaks']
    rows.append(row)
    print(row['case'], row['phase'], 'ms', round(candidate['milliseconds'], 6),
          'change', [round(v, 2) for v in changes], 'spread', round(row['unchangedSpreadPercent'], 2),
          'IQR', {k:round(v['iqrPercent'],2) for k,v in metrics.items()},
          'allocation', [round(v,2) for v in allocations])
for name in ('machine-before.json', 'machine-after.json'):
    assert json.loads((out/name).read_text(encoding="utf-8"))['gate']['quiet']
assert (out/'exit-code.txt').read_text(encoding="utf-8").strip() == '0'
elapsed = (datetime.datetime.fromisoformat((out/'stopped.utc').read_text(encoding="utf-8").strip().replace('Z','+00:00'))
           - datetime.datetime.fromisoformat((out/'started.utc').read_text(encoding="utf-8").strip().replace('Z','+00:00'))).total_seconds()
summary = dict(performanceQualified=False, purpose='Whole native preparation versus cf3dc9cd;two same-bit CSV/JSON confirmation jobs;allocation/elapsed/compile separated,peaks initial untimed setup only',
               rows=rows, ownedCollectionSeconds=elapsed,
               allGoldenBalanceFixtureIdentityQuietGatesPassed=True,
               archiveSHA256=hashlib.sha256((root/'observations.tgz').read_bytes()).hexdigest())
(root/'assessment.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('Complete owned collection seconds',elapsed,'all gates/goldens/identity/balances/fixtures pass')
