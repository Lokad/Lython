import json

totals = {}
accepted = 0
with open('input.jsonl') as source:
    for line in source:
        if line.strip():
            record = json.loads(line)
            amount = record['amount']
            if amount >= 0:
                kind = record['kind']
                totals[kind] = totals.get(kind, 0) + amount
                accepted += 1
with open('report.json', 'w', newline='') as target:
    json.dump(totals, target, sort_keys=True, ensure_ascii=False, separators=(',', ':'))
    target.write('\n')
print(accepted)
