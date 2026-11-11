import difflib

with open('before.py', encoding='utf-8') as source:
    before = source.readlines()
with open('after.py', encoding='utf-8') as source:
    after = source.readlines()
with open('change.patch', 'w', encoding='utf-8', newline='') as target:
    target.writelines(difflib.unified_diff(before, after,
                                         fromfile='before.py', tofile='after.py'))
print(len(before), len(after))
