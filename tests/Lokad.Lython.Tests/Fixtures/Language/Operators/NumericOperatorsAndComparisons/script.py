vals = []
vals.append(str(7 + 2 * 3))
vals.append(str(7 / 2))
vals.append(str(-7 // 3))
vals.append(str(-7 % 3))
vals.append(str(None is None))
vals.append(str("ab" in "zabz"))
vals.append(str("xy" not in "zabz"))
vals.append(str(2.0 == 2))
vals.append(str(2.5 > 2))
vals.append(str(list(enumerate(range(3, 6), 10))))

write_text("/out.txt", "\n".join(vals))
