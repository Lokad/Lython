def words_from_line(line:str)->list:
    return line.lower().split()

counts = {}
total = 0
with open('input.txt') as source:
    for line in source:
        for word in words_from_line(line):
            counts[word] = counts.get(word, 0) + 1
            total += 1
with open('report.txt', 'w', newline='') as target:
    for word in sorted(counts):
        target.write(word + '=' + str(counts[word]) + '\n')
print(total)
