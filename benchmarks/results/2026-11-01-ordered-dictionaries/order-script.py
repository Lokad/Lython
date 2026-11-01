import json
for size in [5, 6, 7, 8, 20]:
    data = {str(i): i for i in range(size)}
    keys = data.keys()
    data.pop('2')
    data['2'] = 99
    expected = [str(i) for i in range(size) if i != 2] + ['2']
    print(size, list(data) == expected, list(keys) == expected,
          list(data.values()) == [i for i in range(size) if i != 2] + [99],
          list(reversed(data)) == list(reversed(expected)))
    print(list(dict(data).items()) == list(data.items()), json.dumps(data).endswith('"2": 99}'))
    def keyword_order(**kwargs):
        return list(kwargs)
    print(keyword_order(**data) == expected, data.popitem() == ('2', 99))
