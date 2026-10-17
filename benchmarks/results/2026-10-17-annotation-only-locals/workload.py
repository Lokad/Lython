def parse_amount(value:str)->int:
    result:int
    result=int(value)
    return result

import csv

totals = {}
accepted = 0
with open('input.csv', newline='') as source:
    for row in csv.DictReader(source):
        amount = parse_amount(row['amount'])
        if amount >= 0:
            kind = row['kind']
            totals[kind] = totals.get(kind, 0) + amount
            accepted += 1
with open('report.csv', 'w', newline='') as target:
    writer = csv.writer(target, lineterminator='\n')
    writer.writerow(['kind', 'total'])
    for kind in sorted(totals):
        writer.writerow([kind, totals[kind]])
print(accepted)
