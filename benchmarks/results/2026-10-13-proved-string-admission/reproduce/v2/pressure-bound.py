held = 'x' * 32000
caught = 0
exposed = 0
kept = []
for i in range(128):
    try:
        candidate = 'y' * 32000
    except MemoryError as error:
        caught += 1
return [caught, len(held), len(kept), exposed]
