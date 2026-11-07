import math
for start in [True, False, 1234567890123456789, 1.5, -0.0]:
    result = math.prod([], start=start)
    print(result is start, type(result).__name__, repr(result))
print(math.prod([True, False, 2]), math.prod([2, 3.5], start=2), math.prod([-2, -3], start=-1))
print(math.prod([1 << 63, 2]), math.prod([1 << 63, -2]))
