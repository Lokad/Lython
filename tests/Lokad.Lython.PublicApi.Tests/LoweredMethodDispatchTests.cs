using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LoweredMethodDispatchTests
{
    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task ClassMethodsPreserveEffectsAndScopesInBothModes(string body)
    {
        var source = "events=[]\nclass Case:\n def run(self):\n" +
            string.Join('\n', body.Split('\n').Select(line => "  " + line)) +
            "\n  return 'ok'\nreturn Case().run()\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            var result = asynchronous ? await compiled.RunAsync(host) : compiled.Run(host);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("ok", result.ReturnValue);
        }
    }

    public static TheoryData<string> Bodies => new()
    {
        """
        def mark(name,value):
         events.append(name)
         return value
        assert (False and mark('bad',1)) is False
        assert (True or mark('bad',2)) is True
        assert mark('yes',7) if mark('condition',True) else mark('bad',3)
        assert (mark('left',3) < mark('middle',2) < mark('bad',4)) is False
        assert events == ['condition','yes','left','middle']
        """,
        """
        values=[1,2,3]
        a=[i*i for i in values if i%2]
        b={i+10 for i in values}
        d={i:i*i for i in values}
        assert a == [1,9] and b == {11,12,13} and d == {1:1,2:4,3:9}
        assert (*a,*values) == (1,9,1,2,3)
        assert [*a,4] == [1,9,4] and {*a,4} == {1,9,4}
        assert {**d,4:16} == {1:1,2:4,3:9,4:16}
        assert ((chosen := 5)+1) == 6 and chosen == 5
        assert (lambda x: x+chosen)(2) == 7
        """,
        """
        def outer():
         events.append('outer')
         return [1,2]
        def item(i):
         events.append(i)
         return i+10
        g=(item(i) for i in outer())
        assert events == ['outer']
        assert next(g) == 11 and events == ['outer',1]
        assert list(g) == [12] and events == ['outer',1,2]
        """,
        """
        def mark(name,value):
         events.append(name)
         return value
        def f(a,/,b=2,*,c=3): return a+b+c
        assert f(*mark('args',[1]),**mark('kwargs',{'c':4})) == 7
        assert f'{mark("value",12):{mark("spec","04d")}}' == '0012'
        assert events == ['args','kwargs','value','spec']
        """,
        """
        class Keys:
         def __getitem__(self,key):
          events.append(key)
          return key
        key=Keys()[1:5:2,...]
        assert key[0].start == 1 and key[0].stop == 5 and key[0].step == 2
        assert key[1] is Ellipsis and events[0] is key
        values=[1,2,3,4]
        assert values[1:4:2] == [2,4]
        values[1:3]=[9]
        del values[0]
        assert values == [9,4]
        """,
        """
        class Descriptor:
         def __get__(self,obj,owner):
          events.append('get')
          return obj._value
         def __set__(self,obj,value):
          events.append('set')
          obj._value=value
        class C:
         value=Descriptor()
        c=C()
        c.value=2
        c.value+=3
        assert c.value == 5 and events == ['set','get','set','get']
        """,
        """
        class C:
         def __enter__(self):
          events.append('enter')
          return self
         def __exit__(self,*args):
          events.append('exit')
          return False
        for i in range(3):
         try:
          with C():
           if i==0: continue
           if i==2: break
         finally: events.append(i)
        else: events.append('bad')
        assert events == ['enter','exit',0,'enter','exit',1,'enter','exit',2]
        try: raise ValueError('outer')
        except ValueError as e:
         try: raise TypeError('inner') from e
         except TypeError as inner: assert inner.__cause__ is e
        """,
        """
        import math
        type Number = int
        assert Number.__value__ is int and math.sqrt(4)==2
        def decorate(fn):
         events.append('decorate')
         return fn
        @decorate
        def f(value:int=3)->int: return value
        assert f()==3 and f.__annotations__ == {'value':int,'return':int}
        match {'value':4}:
         case {'value':value} if value>3: events.append(value)
        assert events == ['decorate',4]
        assert isinstance(2j,complex) and b'x'[0]==120 and ... is Ellipsis
        """
    };
}
