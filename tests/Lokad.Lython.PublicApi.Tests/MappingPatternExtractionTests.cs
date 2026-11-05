using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MappingPatternExtractionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["rest excludes Python-equal custom key", """
            class Key:
             def __hash__(self):return 1
             def __eq__(self,other):return isinstance(other,Key)
            class Keys:first=Key()
            source={Key():10,'other':20}
            match source:
             case {Keys.first:value,**rest}:print(value,len(rest),list(rest.values()))
            """, "10 1 [20]\n"];
        yield return ["dynamic custom duplicate", """
            class Key:
             def __hash__(self):return 1
             def __eq__(self,other):return isinstance(other,Key)
            class Keys:first=Key();second=Key()
            source={Key():10,'other':20}
            try:
             match source:
              case {Keys.first:x,Keys.second:y}:print('matched')
            except ValueError:print('duplicate')
            """, "duplicate\n"];
        yield return ["all keys precede value patterns", """
            events=[]
            class D:
             def __init__(self,label,value):self.label=label;self.value=value
             def __get__(self,obj,owner):events.append(self.label);return self.value
            class Keys:a=D('key-a',1);b=D('key-b',2);expected=D('value',10)
            source={1:10,2:20}
            match source:
             case {Keys.a:Keys.expected,Keys.b:value}:print(value)
            print(events)
            """, "20\n['key-a', 'key-b', 'value']\n"];
        yield return ["short subjects skip keys", """
            events=[]
            class D:
             def __get__(self,obj,owner):events.append('key');return 1
            class Keys:a=D();b=D()
            source={1:10}
            match source:
             case {Keys.a:x,Keys.b:y}:print('matched')
             case _:print('short')
            print(events)
            """, "short\n[]\n"];
        yield return ["numeric duplicate and earlier missing key", """
            class Keys:a=1;b=True
            try:
             match {1:10,2:20}:
              case {Keys.a:x,Keys.b:y}:print('matched')
            except ValueError as error:print(error)
            match {2:20,3:30}:
             case {Keys.a:x,Keys.b:y}:print('matched')
             case _:print('missing')
            """, "mapping pattern checks duplicate key (True)\nmissing\n"];
        yield return ["captured values survive mutation", """
            class D:
             def __get__(self,obj,owner):source[2]=99;return 10
            class Keys:a=1;b=2;expected=D()
            source={1:10,2:20}
            match source:
             case {Keys.a:Keys.expected,Keys.b:value}:print(value,source[2])
            """, "20 99\n"];
        yield return ["rest deletion propagates missing key", """
            class D:
             def __get__(self,obj,owner):source.clear();return 10
            class Keys:a=1;expected=D()
            source={1:10,2:20}
            try:
             match source:
              case {Keys.a:Keys.expected as value,**rest}:print('matched')
            except KeyError as error:print('removed',error)
            print(source)
            """, "removed 1\n{}\n"];
        yield return ["empty patterns and shallow rest aliases", """
            source={'a':[1]}
            match source:
             case {}:print('empty')
            match source:
             case {**rest}:print(rest==source,rest is source,rest['a'] is source['a'])
            match {}:
             case {**rest}:print(rest)
            """, "empty\nTrue False True\n{}\n"];
        yield return ["custom hash calls and duplicate representation", """
            events=[]
            class Key:
             def __hash__(self):events.append('hash');return 1
             def __eq__(self,other):return isinstance(other,Key)
             def __repr__(self):return 'Key()'
            key=Key()
            class Keys:a=key;b=Key()
            source={key:10,'other':20}
            events.clear()
            match source:
             case {Keys.a:value,**rest}:print(value,list(rest.values()),events)
            events.clear()
            try:
             match source:
              case {Keys.a:x,Keys.b:y}:print('matched')
            except ValueError as error:print(error)
            """, "10 [20] ['hash', 'hash', 'hash', 'hash']\nmapping pattern checks duplicate key (Key())\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ExtractionFollowsPython(string name, string source, string expected)
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
