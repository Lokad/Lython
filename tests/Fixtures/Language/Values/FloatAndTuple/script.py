values = ("x", 1.5, 2.5)
if values[1] < values[2] and len(values) == 3:
    __lython_file = open("/result.txt", "w")
    __lython_file.write(str(values[1]))
    __lython_file.close()
