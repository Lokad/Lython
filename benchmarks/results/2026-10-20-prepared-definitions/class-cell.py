class Outer:
    def make(self):
        def __inner(value=(yield 'default')):
            return __class__, value
        yield __inner


g = Outer().make()
print(next(g))
function = g.send(7)
g.close()
print(function()[0] is Outer, function()[1])
