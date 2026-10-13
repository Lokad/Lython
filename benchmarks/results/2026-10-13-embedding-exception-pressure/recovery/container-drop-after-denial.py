held = ['x' * 16000]
try:
    candidate = 'y' * 16000
except MemoryError:
    pass
del held[0]
candidate = 'z' * 16000
return len(candidate)
