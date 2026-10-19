events = []
class Key:
    def __hash__(self):
        events.append('hash')
        return 1

key = Key()
d = {}
print(d.setdefault(key, 7), events)
events.clear()
print(d.setdefault(key, 9), events)
