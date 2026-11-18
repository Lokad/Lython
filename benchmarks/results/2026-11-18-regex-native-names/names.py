import re
p=re.compile(r"(?P<x>a)(b)(?P<ééééééééé名>c)(?P<abcdefghijklmnopqrstuvwxyz_abcdef>d)")
m=p.fullmatch('abcd')
assert p.groups==4
assert list(p.groupindex.items())==[('x',1),('ééééééééé名',3),('abcdefghijklmnopqrstuvwxyz_abcdef',4)]
assert m.groups()==('a','b','c','d')
assert m.group('ééééééééé名')=='c'
assert m.lastindex==4 and m.lastgroup=='abcdefghijklmnopqrstuvwxyz_abcdef'
assert m.expand(r'\g<x>:\g<ééééééééé名>:\g<abcdefghijklmnopqrstuvwxyz_abcdef>')=='a:c:d'
re.purge()
assert m.groupdict()=={'x':'a','ééééééééé名':'c','abcdefghijklmnopqrstuvwxyz_abcdef':'d'}
assert not re.compile('(a)(b)').groupindex
assert re.compile('(a)(b)').fullmatch('ab').groupdict()=={}
assert re.compile('abcd').groups==0
assert re.compile('abcd').fullmatch('abcd').groupdict()=={}
n=re.fullmatch(r'(?P<outer>(?P<ééééééééé名>a))','a')
assert n.lastindex==1 and n.lastgroup=='outer'
print('native names: short, unnamed, long ASCII, long UTF-8, aliases, closure order')
