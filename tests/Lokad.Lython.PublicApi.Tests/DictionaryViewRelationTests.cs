using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryViewRelationTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["unhashable values and relations", """
            left={'a':[1],'b':{'x':2}}
            right={'a':[1],'b':{'x':2},'c':[]}
            print(left.items()==right.items(),left.items()!=right.items(),left.items()<right.items(),left.items()<=right.items(),right.items()>left.items(),right.items()>=left.items())
            print(left.items().__eq__(left.items()),left.items().__ne__(right.items()),left.items().__le__(right.items()),right.items().__ge__(left.items()))
            print(('a',[1]) in left.items(),left.items().__contains__(('b',{'x':2})))
            """, "False True True True True True\nTrue True True True\nTrue True\n"];
        yield return ["custom keys and self-view effects", """
            events=[]
            class Key:
             def __init__(self,n):self.n=n
             def __hash__(self):events.append(('hash',self.n));return 1
             def __eq__(self,other):events.append(('eq',self.n,other.n));return True
            left={Key(1):10}
            right={Key(2):20}
            events.clear()
            print(left.keys()==right.keys(),events)
            events.clear()
            print(left.keys().__eq__(right.keys()),events)
            view=left.keys()
            events.clear()
            print(view==view,events)
            """, "True [('hash', 1), ('eq', 2, 1)]\nTrue [('hash', 1), ('eq', 2, 1)]\nTrue [('hash', 1)]\n"];
        yield return ["item shapes and tuple families", """
            from collections import namedtuple
            Pair=namedtuple('Pair','key value')
            source={'a':[1]}
            view=source.items()
            print(['a',[1]] in view,('a',) in view,{'a':[1]} in view,Pair('a',[1]) in view)
            try:print(([],1) in view)
            except TypeError:print('unhashable key')
            print(view.__eq__([]),view.__lt__([]))
            """, "False False False True\nunhashable key\nNotImplemented NotImplemented\n"];
        yield return ["native keys and mixed set operands", """
            source={'a':1,'b':2}
            keys=source.keys()
            items=source.items()
            print(keys=={'a','b'},{'a','b'}==keys,keys<{'a','b','c'},{'a','b','c'}>keys)
            print(items=={('a',1),('b',2)},{('a',1),('b',2)}==items)
            print(source.keys()==source.items(),source.items()==source.keys())
            print(keys.__eq__({'a','b'}),keys.__le__({'a','b'}),items.__ge__({('a',1)}))
            """, "True True True True\nTrue True\nFalse False\nTrue True True\n"];
        yield return ["ChainMap view combinations", """
            from collections import ChainMap
            left=ChainMap({'a':[1]},{'b':[]})
            right={'a':[1],'b':[]}
            print(left.keys()==right.keys(),right.keys()==left.keys(),left.items()==right.items(),right.items()==left.items())
            print(left.items().__eq__(right.items()),right.items().__eq__(left.items()))
            print(('a',[1]) in left.items(),left.items().__contains__(('b',[])))
            print(left.items()<=right.items(),left.keys()>={'a'})
            """, "True True True True\nTrue NotImplemented\nTrue True\nTrue True\n"];
        yield return ["value comparison direction and truth", """
            events=[]
            class Truth:
             def __bool__(self):events.append('truth');return True
            class Value:
             def __init__(self,n):self.n=n
             def __eq__(self,other):events.append((self.n,other.n));return Truth()
            left={'a':Value('left')}
            right={'a':Value('right')}
            print(left.items()==right.items(),events)
            events.clear()
            print(left.items().__eq__(right.items()),events)
            events.clear()
            print(('a',left['a']) in right.items(),events)
            """, "True [('right', 'left'), 'truth']\nTrue [('right', 'left'), 'truth']\nTrue [('right', 'left'), 'truth']\n"];
        yield return ["source mutation is observable", """
            class Value:
             def __eq__(self,other):left.clear();return True
            left={'a':Value()}
            right={'a':Value()}
            try:print(left.items()==right.items())
            except RuntimeError:print('mutation')
            print(len(left),len(right))
            """, "mutation\n0 1\n"];
        yield return ["native ordering and empty views", """
            left={1:2,3:4}
            right={1:2,3:4,5:6}
            for a,b in [(left.keys(),right.keys()),(left.items(),right.items())]:
             print(a==b,a!=b,a<b,a<=b,a>b,a>=b)
             print(a.__eq__(b),a.__ne__(b),a.__lt__(b),a.__le__(b),a.__gt__(b),a.__ge__(b))
            print({}.items()=={}.items(),{}.keys()<=set(),set()>={}.items())
            """, "False True True True False False\nFalse True True True False False\nFalse True True True False False\nFalse True True True False False\nTrue True True\n"];
        yield return ["ChainMap reflected equality direction", """
            from collections import ChainMap
            events=[]
            class Value:
             def __init__(self,n):self.n=n
             def __eq__(self,other):events.append((self.n,other.n));return True
            left={'a':Value('native')}
            right=ChainMap({'a':Value('chain')})
            print(left.items()==right.items(),events)
            events.clear()
            print(right.items()==left.items(),events)
            events.clear()
            print(left.items().__eq__(right.items()),events)
            """, "True [('native', 'chain')]\nTrue [('native', 'chain')]\nNotImplemented []\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ViewsFollowPython(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = await script.RunAsync(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
