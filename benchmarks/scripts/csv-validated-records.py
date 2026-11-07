import csv


def validated_record(record: dict, *, minimum=0):
    amount = int(record['amount'])
    if amount < minimum:
        return None
    if record['note'] == 'skip':
        return None
    return record['kind'], amount


totals = {}
accepted = 0
with open('input.csv', newline='') as source:
    for row in csv.DictReader(source):
        item = validated_record(row, minimum=0)
        if item is None:
            continue
        kind, amount = item
        totals[kind] = totals.get(kind, 0) + amount
        accepted += 1
with open('report.csv', 'w', newline='') as target:
    writer = csv.writer(target, lineterminator='\n')
    writer.writerow(['kind', 'total'])
    for kind in sorted(totals):
        writer.writerow([kind, totals[kind]])
print(accepted)
