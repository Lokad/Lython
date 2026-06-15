items = ["a", "skip", "b", "stop", "c"]
__lython_file = open("/result.txt", "w")
__lython_file.write("")
__lython_file.close()
for item in items:
    if item == "skip":
        continue
    if item == "stop":
        break
    __lython_file = open("/result.txt", "a")
    __lython_file.write(item)
    __lython_file.close()
pending = ["x", "y"]
while len(pending) != 0:
    __lython_file = open("/result.txt", "a")
    __lython_file.write(pending.pop())
    __lython_file.close()
