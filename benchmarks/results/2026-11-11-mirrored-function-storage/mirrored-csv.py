def main():
    processed=0
    parse_amount=int
    skip_note="skip"
    import csv


    def validated_record(record: dict, *, minimum=0, record_types=[]):
        nonlocal processed
        processed += 1
        if not record_types:
            class Record:
                def __init__(self, kind, amount):
                    self.kind = kind
                    self.amount = amount
            record_types.append(Record)
        amount = parse_amount(record['amount'])
        if amount < minimum:
            return None
        if record['note'] == skip_note:
            return None
        return record_types[0](record['kind'], amount)


    totals = {}
    accepted = 0
    with open('input.csv', newline='') as source:
        for row in csv.DictReader(source):
            item = validated_record(row, minimum=0)
            if item is None:
                continue
            kind, amount = item.kind, item.amount
            totals[kind] = totals.get(kind, 0) + amount
            accepted += 1
    with open('report.csv', 'w', newline='') as target:
        writer = csv.writer(target, lineterminator='\n')
        writer.writerow(['kind', 'total'])
        for kind in sorted(totals):
            writer.writerow([kind, totals[kind]])
    assert processed == 5120
    print(accepted)

main()
