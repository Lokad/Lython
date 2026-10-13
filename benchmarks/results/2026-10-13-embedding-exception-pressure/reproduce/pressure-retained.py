held = 'x' * 32000
caught = 0
exposed = 0
kept = []
for i in range(128):
    try:
        candidate = 'y' * 32000
    except MemoryError as error:
        caught += 1
        message = str(error)
        assert len(error.args) == 1
        assert len(message) > 0
        exposed += 1
        if len(kept) < 8:
            kept.append(error)
return [caught, len(held), len(kept), exposed]
