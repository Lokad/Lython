count = 2000
import json
class SetEncoder(json.JSONEncoder):
    def default(self, obj):
        if isinstance(obj, set):
            return sorted(obj)
        return super().default(obj)
n = 0
for _ in range(count):
    if json.dumps({1}, cls=SetEncoder) == "[1]":
        n = n + 1
return n
