held = 'x' * 16000
try:
    candidate = 'y' * 16000
except MemoryError:
    pass
held = None
candidate = 'z' * 16000
return len(candidate)
