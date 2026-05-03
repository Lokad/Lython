import re
text = read_text("/input.txt")
text = re.sub("alpha", "omega", text)
write_text("/output.txt", text)
