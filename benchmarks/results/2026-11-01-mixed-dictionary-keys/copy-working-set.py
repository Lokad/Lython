from collections import defaultdict
original={str(i):i for i in range(256)}
cloned=defaultdict(int,original)
print(len(cloned),sum(cloned.values()))
