using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ConstructorSemanticsTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["ordinary and inherited initializer", """
            class A:
             def __init__(self,x=7):self.x=x
            class B(A):pass
            print(A().x,B(9).x)
            """, "7 9\n"];
        yield return ["selected class survives argument rebinding", """
            class A:
             def __init__(self,x):self.x=x
            Selected=A
            def argument():
             global A
             A=17
             return 9
            print(Selected(argument()).x)
            """, "9\n"];
        yield return ["unrelated allocator return skips init", """
            class A:
             def __new__(cls,x):print('new',x);return 7
             def __init__(self,x):raise AssertionError('wrong init')
            print(A(9))
            """, "new 9\n7\n"];
        yield return ["returned subtype owns initializer", """
            class A:
             def __new__(cls):print('new');return object.__new__(B)
             def __init__(self):print('A init')
            class B(A):
             def __init__(self):print('B init')
            print(type(A()).__name__)
            """, "new\nB init\nB\n"];
        yield return ["staticmethod initializer receives original args", """
            class A:
             @staticmethod
             def __init__(x):print('init',x)
            A(7)
            """, "init 7\n"];
        yield return ["classmethod initializer receives class", """
            class A:
             @classmethod
             def __init__(cls,x):print(cls.__name__,x)
            A(7)
            """, "A 7\n"];
        yield return ["initializer descriptor binds instance", """
            class D:
             def __get__(self,obj,owner):
              print(obj is None,owner.__name__)
              def initialize(x):print('init',x)
              return initialize
            class A:__init__=D()
            A(7)
            """, "False A\ninit 7\n"];
        yield return ["allocator changes initializer before selection", """
            class A:
             def __new__(cls,x):
              print('new')
              cls.__init__=replacement
              return object.__new__(cls)
             def __init__(self,x):raise AssertionError('old init')
            def replacement(self,x):print('replacement',x)
            A(9)
            """, "new\nreplacement 9\n"];
        yield return ["keyword defaults and variadics", """
            class A:
             def __init__(self,x=2,*extra,flag=3,**rest):print(x,extra,flag,rest)
            A()
            A(7,8,9,flag=4,other=5)
            """, "2 () 3 {}\n7 (8, 9) 4 {'other': 5}\n"];
        yield return ["expanded arguments keep evaluation order and receiver separate", """
            events=[]
            def argument(label,value):events.append(label);return value
            class A:
             def __init__(self,x,/,*extra,flag,**rest):
              self.value=(x,extra,flag,rest)
            a=A(*argument('pos',[7,8]),flag=argument('flag',9),**argument('map',{'self':10}))
            print(events,a.value)
            """, "['pos', 'flag', 'map'] (7, (8,), 9, {'self': 10})\n"];
        yield return ["constructor binding failures leave body unentered", """
            events=[]
            class A:
             def __init__(self,x,/,*,flag):events.append((x,flag))
            try:A(x=1,flag=2)
            except TypeError:print('positional-only')
            try:A(1)
            except TypeError:print('missing')
            try:A(1,flag=2,other=3)
            except TypeError:print('unexpected')
            try:A(1,flag=2,**{'flag':3})
            except TypeError:print('duplicate')
            A(1,flag=2)
            print(events)
            """, "positional-only\nmissing\nunexpected\nduplicate\n[(1, 2)]\n"];
        yield return ["non None initializer and non callable init", """
            class A:
             def __init__(self):return 7
            try:A()
            except TypeError:print('bad return')
            class B:__init__=17
            try:B()
            except TypeError:print('bad callable')
            """, "bad return\nbad callable\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ConstructorsPreservePythonBindingAndEffects(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
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
        Assert.Equal(0, budget.ReservedBytes);
    }
}
