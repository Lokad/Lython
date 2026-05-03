mkdir("/out")
for name in listdir("/src"):
    if name != "skip.txt":
        text = read_text(join_path("/src", name))
        write_text(join_path("/out", basename(name)), text)
