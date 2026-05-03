import fnmatch
mkdir("/out")
for name in fnmatch.filter(listdir("/src"), "*.txt"):
    text = read_text(join_path("/src", name)).upper()
    write_text(join_path("/out", name), text)
