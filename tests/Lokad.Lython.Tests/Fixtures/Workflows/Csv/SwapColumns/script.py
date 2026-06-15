import csv
lines = open("/input.csv").read().splitlines()
rows = csv.reader(lines)
writer = csv.writer()
for row in rows:
    writer.writerow([row[1], row[0]])
__lython_file = open("/output.csv", "w")
__lython_file.write(writer.getvalue())
__lython_file.close()
