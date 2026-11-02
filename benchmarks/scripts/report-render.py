import json


def render(record):
    return f"| {record['name']:<20.12s} | {record['state']:^8s} | {record['amount']:>8,d} |\n"


with open('records.json') as source:
    records = json.load(source)
written = 0
with open('report.md', 'w', newline='') as target:
    target.write('# Shipment report\n\n| Name | State | Amount |\n| --- | --- | ---: |\n')
    for record in records:
        target.write(render(record))
        written += 1
print(written)
