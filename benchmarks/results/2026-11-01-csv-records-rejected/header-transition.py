import csv
events=[]
class Key:
 def __hash__(self):
  events.append('hash')
  return 1
fields=['a','a']
r=csv.DictReader(['1,2','3,4','5,6'],fieldnames=fields)
first=next(r)
key=Key()
fields[0]=key
fields[1]='b'
second=next(r)
r.fieldnames=['c','c']
third=next(r)
print(first,list(second.values()),third)
print(list(second)[0] is key,second[key],bool(events),r.line_num)
