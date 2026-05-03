values = ("x", 1.5, 2.5)
if values[1] < values[2] and len(values) == 3:
    write_text("/result.txt", str(values[1]))
