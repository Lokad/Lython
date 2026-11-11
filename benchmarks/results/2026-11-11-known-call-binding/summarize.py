from pathlib import Path
import json
import statistics

root = Path(__file__).parent
for phase in ['warm-sync', 'warm-async', 'compile']:
    path = root / phase / 'measurements.json'
    if not path.exists() or not path.stat().st_size:
        continue
    data = json.loads(path.read_text(encoding='utf-8'))
    assert data['allGoldensPassed'] and data['allBalancesZero'] and data['ownedFixturesRemoved']
    rows = []
    for item in data['observations']:
        medians = {label: {field: statistics.median(s[field] / s['jobs'] for s in item['samples'] if s['image'] == label)
                         for field in ['elapsedMilliseconds', 'executionMilliseconds', 'cpuMilliseconds', 'allocatedBytes']}
                   for label in ['before', 'candidate', 'before-control']}
        changes = {label: {field: (100 * (medians['candidate'][field] / medians[label][field] - 1)
                                  if medians[label][field] else None) for field in medians[label]}
                   for label in ['before', 'before-control']}
        spread = {field: (100 * abs(medians['before-control'][field] / medians['before'][field] - 1)
                          if medians['before'][field] else None) for field in medians['before']}
        rows.append(dict(case=item['caseId'], medians=medians, candidateChangePercent=changes,
                         unchangedControlSpreadPercent=spread, admittedPeaks=item['admittedPeaks']))
        allocation = [v['allocatedBytes'] for v in changes.values()]
        elapsed = [v['elapsedMilliseconds'] for v in changes.values()]
        print(phase, item['caseId'], 'allocation', ', '.join(f'{v:+.2f}%' for v in allocation),
              'diagnostic elapsed', ', '.join(f'{v:+.2f}%' for v in elapsed),
              'control', f"{spread['elapsedMilliseconds']:.2f}%", flush=True)
    (root / phase / 'summary.json').write_text(json.dumps(dict(phase=phase, performanceQualified=False,
        warmupsPerImage=data['warmupsPerImage'], rounds=data['rounds'], batch=data['batch'],
        normalGcAndTiering=data['normalGcAndTiering'], allGoldensPassed=True, allBalancesZero=True,
        ownedFixturesRemoved=True, rows=rows), indent=2) + '\n', encoding='utf-8')
