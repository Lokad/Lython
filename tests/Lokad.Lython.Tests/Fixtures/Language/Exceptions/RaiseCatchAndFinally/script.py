write_text("/result.txt", "")
try:
    raise ValueError("bad")
except ValueError as err:
    append_text("/result.txt", err.type)
finally:
    append_text("/result.txt", ",done")
try:
    read_text("/missing.txt")
except RuntimeError as err:
    append_text("/result.txt", ",")
    append_text("/result.txt", err.type)
