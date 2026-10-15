def wanted(line):
    return line.startswith('NEEDLE:')

scanned = 0
match = ''
with open('input.txt') as source:
    for line in source:
        scanned += 1
        if wanted(line):
            match = line
            break
with open('matches.txt', 'w', newline='') as target:
    target.write(match)
print(scanned)
