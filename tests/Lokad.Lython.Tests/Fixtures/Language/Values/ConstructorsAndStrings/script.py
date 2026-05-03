text = "  hello  "
raw = r"\n"
triple = """alpha
beta"""

vals = []
vals.append(str(bool(0)))
vals.append(str(bool(1)))
vals.append(str(int("42")))
vals.append(str(float("2.5")))
vals.append(str(list((1, 2))))
vals.append(str(tuple([3, 4])))
vals.append(str(dict([("k", 5)])))
vals.append(text.strip())
vals.append(text.lstrip())
vals.append(text.rstrip())
vals.append(str("alphabet".find("pha")))
vals.append("{0}-{1}".format("A", 3))
vals.append(raw)
vals.append(triple.splitlines()[1])

write_text("/out.txt", "\n".join(vals))
