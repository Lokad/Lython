from collections import namedtuple
import time
first = namedtuple('Point', 'x')
second = namedtuple('Point', 'x')
data = {str(i): i for i in range(8)}
for index, cls in enumerate([first, second, time.struct_time]):
    data[cls] = index
print([data[cls] for cls in [first, second, time.struct_time]])
print(len({first, second, time.struct_time}), hash(first) == hash(first))
del data[first]
data[first] = 99
print(list(data)[-1] is first, data[second], data[first])
