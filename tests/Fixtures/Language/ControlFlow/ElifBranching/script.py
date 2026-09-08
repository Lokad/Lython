value = 2

if value == 1:
    result = "one"
elif value == 2:
    result = "two"
else:
    result = "other"

__lython_file = open("/out.txt", "w")
__lython_file.write(result)
__lython_file.close()
