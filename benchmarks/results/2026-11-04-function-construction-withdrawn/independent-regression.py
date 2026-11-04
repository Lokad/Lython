def make(value):
    def f(a=value, /, *args, key=value, **kw):
        local = a + key
        return local, args, kw
    return f
first = make(1)
second = make(2)
first.__defaults__ = (10,)
first.__kwdefaults__['key'] = 20
print(first(local=7, a=8, args=9, kw=10))
print(second())
print(first.__defaults__ is second.__defaults__, first.__kwdefaults__ is second.__kwdefaults__)
def reject(value):
    local = value
    return local
try: reject(1, local=2)
except TypeError: print('unexpected')
