parts = []
if any([False, True]) and not ("z" in ["a", "b"]):
    parts.append("logic")
if min([3, 1, 2]) < max([3, 1, 2]):
    parts.append("compare")
if "a" in "cat":
    parts.append("text")
write_text("/result.txt", ",".join(parts))
