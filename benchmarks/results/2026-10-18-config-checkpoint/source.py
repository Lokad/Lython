import json

with open('config.json') as source:
    config = json.load(source)
config['enabled'] = True
config['retries'] += 1
config['paths'].append('generated')
with open('config.json', 'w', newline='') as target:
    json.dump(config, target, ensure_ascii=False, sort_keys=True, separators=(',', ':'))
    target.write('\n')
print(len(config['paths']))
