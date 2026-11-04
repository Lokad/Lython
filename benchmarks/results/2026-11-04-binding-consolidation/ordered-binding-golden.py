print(len([1, 2]))
if False:
    len = 17
import json as parser
namespace = globals()
print(parser.loads('3'))
namespace['parser'] = 'rebound module'
print(parser)
namespace['helper'] = lambda: 7
print(helper())
def helper():
    return 9
print(helper())
namespace['helper'] = lambda: 11
print(helper())
del namespace['helper']
try:
    helper
except NameError:
    print('missing helper')
class Widget:
    pass
namespace['Widget'] = 'rebound class'
print(Widget)
print([name for name in namespace if name in ('parser', 'helper', 'Widget')])
def local_order(p, *args, k=0, **kw):
    zebra = 1
    apple = 2
    zebra = 3
    return list(locals())
print(local_order(0, k=3))
class Manager:
    def __init__(self, value):
        self.value = value
    def __enter__(self):
        return self.value
    def __exit__(self, typ, value, tb):
        return False
def compound_order():
    with (manager := Manager(3)) as resource:
        first = resource
    try:
        before = 1
        raise ValueError()
    except ValueError as error:
        handler = 2
    finally:
        after = 3
    match (subject := 4):
        case capture if (guard := True):
            matched = 5
        case _:
            fallback = 0
    return list(locals())
print(compound_order())
