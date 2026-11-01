class Key:
 def __hash__(self): return hash('x')
 def __eq__(self, other): return other == 'x'
key=Key()
data={key:1}
data['x']=2
print(len(data), list(data)[0] is key, data[key], data['x'])
