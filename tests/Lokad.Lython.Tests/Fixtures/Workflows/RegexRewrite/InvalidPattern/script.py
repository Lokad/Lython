import re
text = read_text("/input.txt")
write_text("/output.txt", re.sub("(", "x", text))
