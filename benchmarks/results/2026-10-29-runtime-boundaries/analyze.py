"""Offline descriptive analysis only; no collection or sample rejection."""
import json
from pathlib import Path
import statistics

directory = Path(__file__).resolve().parent
source = json.loads((directory / 'observations.json').read_text(encoding='utf-8'))

def distribution(values):
    values = list(values)
    median = statistics.median(values)
    quartiles = statistics.quantiles(values, n=4, method='inclusive')
    return dict(count=len(values), median=median, minimum=min(values), maximum=max(values),
                iqrPercent=100 * (quartiles[2] - quartiles[0]) / median if median else None)

rows = []
for item in source['rows']:
    phases = [v for v in item['observations'] if 'phases' in v]
    public = [v for v in item['observations'] if v['kind'].startswith('public ')]
    assert len(phases) == 16 and len(public) == 48
    assert all(v['accountingAfterCleanup'] == item['compilationRetainedBytes'] for v in item['observations'])
    rows.append(dict(id=item['id'], compilationRetainedBytes=item['compilationRetainedBytes'],
        phases={name: dict(microseconds=distribution(v['phases'][name]['Microseconds'] for v in phases),
                           allocatedBytes=None if name == 'handoff' else distribution(v['phases'][name]['AllocatedBytes'] for v in phases))
                for name in phases[0]['phases']},
        hostMicroseconds=distribution(v['host']['elapsedUs'] for v in phases),
        executionAndPublicationExcludingHostUs=distribution(
            v['phases']['executionInclusive']['Microseconds'] + v['phases']['publishWriters']['Microseconds']
            - v['host']['elapsedUs'] for v in phases),
        retainedAfterExecutionBytes=distribution(v['retainedAfterExecutionBytes'] for v in phases),
        lanes={lane: dict(totalBeforeVerificationUs=distribution(v['totalBeforeVerificationUs'] for v in public if v['kind'] == 'public ' + lane),
                         processAllocatedBytes=distribution(v['processAllocatedBytes'] for v in public if v['kind'] == 'public ' + lane),
                         hostMicroseconds=distribution(v['host']['elapsedUs'] for v in public if v['kind'] == 'public ' + lane),
                         delayMicroseconds=distribution(v['host']['suspendedDelayUs'] for v in public if v['kind'] == 'public ' + lane),
                         incompleteAtPublicReturn=sum(v['taskIncomplete'] for v in public if v['kind'] == 'public ' + lane))
               for lane in ['sync', 'async-completed', 'async-delayed']}))

result = dict(performanceQualified=False, note='All observations retained. Descriptive wall-time and allocation distributions, not CPU shares. Exclusive values subtract matching nested host intervals per observation before summarizing.', rows=rows)
with (directory / 'summary.json').open('x', encoding='utf-8', newline='\n') as output:
    json.dump(result, output, indent=2)
    output.write('\n')
for row in rows:
    print(row['id'], 'context_us', round(row['phases']['context']['microseconds']['median'], 1),
          'guest+publication_us', round(row['executionAndPublicationExcludingHostUs']['median'], 1),
          'host_us', round(row['hostMicroseconds']['median'], 1),
          'guest_iqr', round(row['executionAndPublicationExcludingHostUs']['iqrPercent'], 1),
          'retained', row['retainedAfterExecutionBytes']['median'])
