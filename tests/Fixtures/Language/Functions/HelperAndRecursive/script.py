def emit_upper(path):
    return open(path).read().upper()

def drain(items):
    if len(items) == 0:
        return None
    __lython_file = open("/result.txt", "a")
    __lython_file.write(items.pop())
    __lython_file.close()
    drain(items)

__lython_file = open("/result.txt", "w")
__lython_file.write(emit_upper("/input.txt"))
__lython_file.close()
drain(["x", "y"])
