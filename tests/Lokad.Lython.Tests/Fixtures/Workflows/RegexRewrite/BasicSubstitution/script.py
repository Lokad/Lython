import re
text = open("/input.txt").read()
text = re.sub("alpha", "omega", text)
__lython_file = open("/output.txt", "w")
__lython_file.write(text)
__lython_file.close()
