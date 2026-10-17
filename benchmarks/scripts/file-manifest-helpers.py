def checksum(data:bytes)->int:
    return sum(data)

import json
import os

manifest = []
os.makedirs('outbox', exist_ok=True)
for name in sorted(os.listdir('inbox')):
    with open('inbox/' + name, 'rb') as source:
        data = source.read()
    with open('outbox/' + name, 'wb') as target:
        target.write(data)
    manifest.append({'name': name, 'size': len(data), 'checksum': checksum(data)})
with open('manifest.json', 'w', newline='') as target:
    json.dump(manifest, target, sort_keys=True, separators=(',', ':'))
    target.write('\n')
print(len(manifest))
