import json

with open('numbers.json') as source:
    data = json.load(source)
planned = sum(data['planned'])
actual = sum(data['actual'])
report = {'count': len(data['planned']), 'planned': planned,
          'actual': actual, 'variance': actual - planned}
with open('report.json', 'w', newline='') as target:
    json.dump(report, target, sort_keys=True, separators=(',', ':'))
    target.write('\n')
print(report['count'])
