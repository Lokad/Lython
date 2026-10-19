def check():
    source = []
    source.append(source)
    repeated = source * 3
    return [len(repeated), repeated is source, repeated[0] is source,
            repeated[0] is repeated[1], len(source), len(source * 0),
            len(source * -2)]
import json
print(json.dumps(check()))
