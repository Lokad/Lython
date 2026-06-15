import re
text = open("/input.txt").read()
__lython_file = open("/output.txt", "w")
__lython_file.write(re.sub("(", "x", text))
__lython_file.close()
