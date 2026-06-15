__lython_file = open("/result.txt", "w")
__lython_file.write("")
__lython_file.close()
try:
    raise ValueError("bad")
except ValueError as err:
    __lython_file = open("/result.txt", "a")
    __lython_file.write(err.type)
    __lython_file.close()
finally:
    __lython_file = open("/result.txt", "a")
    __lython_file.write(",done")
    __lython_file.close()
try:
    open("/missing.txt").read()
except RuntimeError as err:
    __lython_file = open("/result.txt", "a")
    __lython_file.write(",")
    __lython_file.close()
    __lython_file = open("/result.txt", "a")
    __lython_file.write(err.type)
    __lython_file.close()
