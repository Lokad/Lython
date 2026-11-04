def note(message):
    with open('events.txt','a',newline='') as target:
        target.write(message+'\n')
def read():
    note('default')
    with open('number.txt') as source:
        return int(source.read())
events=[]
def stamp(value):
    events.append(value)
    return value
def f(a=stamp(1),b=read(),*,c=stamp(3),d=read()):
    return a,b,c,d
print(f(),events)
