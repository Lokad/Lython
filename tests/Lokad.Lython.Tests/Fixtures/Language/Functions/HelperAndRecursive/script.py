def emit_upper(path):
    return read_text(path).upper()

def drain(items):
    if len(items) == 0:
        return None
    append_text("/result.txt", items.pop())
    drain(items)

write_text("/result.txt", emit_upper("/input.txt"))
drain(["x", "y"])
