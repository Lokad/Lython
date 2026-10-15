with open('input.txt') as source:
    contents = source.read()
report = contents.upper()
with open('report.txt', 'w', newline='') as target:
    target.write(report)
print(len(report))
