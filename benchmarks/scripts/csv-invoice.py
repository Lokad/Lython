import csv


def rounded_basis_points(amount, rate):
    return (amount * rate + 5000) // 10000


def price_line(row):
    quantity = int(row['quantity'])
    unit_cents = int(row['unit_cents'])
    discount = int(row['discount_bps'])
    tax_rate = int(row['tax_bps'])
    net = rounded_basis_points(quantity * unit_cents, 10000 - discount)
    tax = rounded_basis_points(net, tax_rate)
    return quantity, net, tax, net + tax


totals = {}
count = 0
grand_total = 0
with open('input.csv', newline='') as source:
    for row in csv.DictReader(source):
        quantity, net, tax, gross = price_line(row)
        category = row['category']
        previous = totals.get(category, (0, 0, 0, 0))
        totals[category] = (
            previous[0] + quantity,
            previous[1] + net,
            previous[2] + tax,
            previous[3] + gross,
        )
        count += 1
        grand_total += gross
with open('report.csv', 'w', newline='') as target:
    writer = csv.writer(target, lineterminator='\n')
    writer.writerow(['category', 'quantity', 'net_cents', 'tax_cents', 'gross_cents'])
    for category in sorted(totals):
        writer.writerow([category, *totals[category]])
print(count, grand_total)
