events = []
class Key:
    def __index__(self):
        events.append('index')
        return 0
def attempt(target):
    events.clear()
    try:
        target[Key()] = 9
    except TypeError:
        print(type(target).__name__, events)
for target in [(1,), 'a', b'a', range(1), 1, None]:
    attempt(target)
