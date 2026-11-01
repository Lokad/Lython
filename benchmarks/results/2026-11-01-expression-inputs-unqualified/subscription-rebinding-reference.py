original=[]
token=original
def read(key):
 with open('events.txt','a') as target:target.write('item;')
 assert key is original
 return key
class Slot:
 def __get__(self,obj,owner):
  global token,Box
  assert obj is None and owner is Box
  with open('events.txt','a') as target:target.write('get;')
  token=['replacement']
  Box=int
  return read
class Box:__class_getitem__=Slot()
value=Box[token]
print(value is original,Box is int,token)
