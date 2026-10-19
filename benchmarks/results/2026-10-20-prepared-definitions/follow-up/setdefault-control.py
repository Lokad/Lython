d = {'x': 7}
fallback = []
print(d.setdefault('x', fallback), d.setdefault('y', fallback) is fallback)
fallback.append(3)
print(d['y'])
try:
    d.setdefault(key='x', default=9)
except TypeError:
    print('positional-only')
try:
    d.setdefault([])
except TypeError:
    print('unhashable')
