import re

text = "alpha beta alpha"
vals = []

m = re.search("beta", text)
vals.append(m.group())
vals.append(str(m.start()))
vals.append(str(m.end()))
vals.append(str(re.match("alpha", text) is not None))
vals.append(str(re.fullmatch("alpha beta alpha", text) is not None))
vals.append(str(len(list(re.findall("alpha", text)))))
parts = re.split(" ", text)
vals.append(parts[1])
vals.append(re.escape("a.b"))

write_text("/out.txt", "\n".join(vals))
