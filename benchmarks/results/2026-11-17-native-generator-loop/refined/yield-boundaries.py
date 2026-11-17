f = lambda: (yield 7)
g = f()
print(next(g))
try:
    g.send(3)
except StopIteration as error:
    print(error.value)
print(sum(f()))

def nested():
    yield (yield 2)
g = nested()
try:
    sum(g)
except TypeError:
    print('consumer error')
print(next(g, 'end'))
