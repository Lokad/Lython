mkdir("/work")
write_text("/work/log.txt", cwd())
append_text("/work/log.txt", "\n")
copy("/seed.txt", "/work/copy.txt")
move("/work/copy.txt", "/work/moved.txt")
if exists("/work/moved.txt") == True:
    append_text("/work/log.txt", basename(dirname("/work/moved.txt")))
remove("/empty.txt")
