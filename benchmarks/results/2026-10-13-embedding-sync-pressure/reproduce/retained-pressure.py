held = 'x' * 16000
caught = 0
for i in range(128):
    try:
        candidate = 'y' * 16000
    except MemoryError:
        caught += 1
return [caught, len(held)]
