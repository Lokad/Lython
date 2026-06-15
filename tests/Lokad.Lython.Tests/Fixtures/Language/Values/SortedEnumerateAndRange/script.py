parts = []
for pair in enumerate(sorted(["b", "a"])):
    parts.append(str(pair[0]))
    parts.append(pair[1])
for value in range(2, 5):
    parts.append(str(value))
__lython_file = open("/result.txt", "w")
__lython_file.write(",".join(parts))
__lython_file.close()
