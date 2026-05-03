items = ["a", "skip", "b", "stop", "c"]
write_text("/result.txt", "")
for item in items:
    if item == "skip":
        continue
    if item == "stop":
        break
    append_text("/result.txt", item)
pending = ["x", "y"]
while len(pending) != 0:
    append_text("/result.txt", pending.pop())
