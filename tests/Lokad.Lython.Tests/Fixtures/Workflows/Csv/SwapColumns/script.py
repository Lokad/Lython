import csv
lines = read_text("/input.csv").splitlines()
rows = csv.reader(lines)
writer = csv.writer()
for row in rows:
    writer.writerow([row[1], row[0]])
write_text("/output.csv", writer.getvalue())
