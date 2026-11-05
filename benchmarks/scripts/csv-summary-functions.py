def accumulate(record:dict, totals:dict)->bool:
    amount = int(record['amount'])
    if amount < 0:
        return False
    kind = record['kind']
    totals[kind] = totals.get(kind, 0) + amount
    return True


import csv

totals = {}
accepted = 0
with open('input.csv', newline='') as source:
    for row in csv.DictReader(source):
        if accumulate(row, totals):
            accepted += 1
with open('report.csv', 'w', newline='') as target:
    writer = csv.writer(target, lineterminator='\n')
    writer.writerow(['kind', 'total'])
    for kind in sorted(totals):
        writer.writerow([kind, totals[kind]])
print(accepted)
