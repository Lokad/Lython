using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NestedHashProtocolTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["slice bounds and direct members", """
            events=[]
            class Key:
             def __init__(self,name):self.name=name
             def __hash__(self):
              events.append(self.name)
              return 17
            value=slice(Key('start'),Key('stop'),Key('step'))
            first=hash(value)
            print(value.__hash__()==first,slice.__hash__(value)==first)
            print(events)
            """, "True True\n['start', 'stop', 'step', 'start', 'stop', 'step', 'start', 'stop', 'step']\n"];
        yield return ["native slice hash consistency", """
            for value in [slice(None),slice(1,2,3),slice((1,2),None,-1)]:
             print(value.__hash__()==hash(value),hash(value)==hash(slice(value.start,value.stop,value.step)))
            """, "True True\nTrue True\nTrue True\n"];
        yield return ["slice keys through tuples and cached calls", """
            from functools import cache
            events=[]
            class Key:
             def __hash__(self):return 17
             def __eq__(self,other):return isinstance(other,Key)
            left=slice(Key(),3)
            right=slice(Key(),3)
            d={(left,):'kept'}
            print(d[(right,)],right in {left})
            @cache
            def value(item):
             events.append('called')
             return 7
            print(value(left),value(right),events)
            """, "kept True\n7 7 ['called']\n"];
        yield return ["dataclass reads precede field hashes", """
            from dataclasses import dataclass
            events=[]
            class Key:
             def __init__(self,name):self.name=name
             def __hash__(self):
              events.append('hash:'+self.name)
              return 17
            @dataclass(unsafe_hash=True)
            class Row:
             a:object
             b:object
             def __getattribute__(self,name):
              if name in ('a','b'):events.append('get:'+name)
              return object.__getattribute__(self,name)
            row=Row(Key('a'),Key('b'))
            events.clear()
            hash(row)
            print(events)
            """, "['get:a', 'get:b', 'hash:a', 'hash:b']\n"];
        yield return ["generated methods retain defining fields", """
            from dataclasses import dataclass
            @dataclass(unsafe_hash=True)
            class Base:
             a:int
            @dataclass(unsafe_hash=True)
            class Child(Base):
             b:int
            row=Child(1,2)
            print(Base.__hash__(row)==hash((row.a,)),Child.__hash__(row)==hash((row.a,row.b)))
            """, "True True\n"];
        yield return ["hash inclusion is independent of comparison inclusion", """
            from dataclasses import dataclass,field
            @dataclass(unsafe_hash=True)
            class Row:
             a:int
             b:object=field(hash=False)
             c:int=field(compare=False,hash=True)
            print(hash(Row(1,[],3))==hash((1,3)))
            @dataclass(frozen=True)
            class Empty:pass
            print(hash(Empty())==hash(()))
            """, "True\nTrue\n"];
        yield return ["later attribute failure precedes the first field hash", """
            from dataclasses import dataclass
            events=[]
            class Key:
             def __hash__(self):
              events.append('hash')
              return 17
            @dataclass(unsafe_hash=True)
            class Row:
             a:object
             b:object
             def __getattribute__(self,name):
              if name in ('a','b'):events.append('get:'+name)
              if name=='b':raise ValueError('field failed')
              return object.__getattribute__(self,name)
            try:hash(Row(Key(),1))
            except ValueError as error:print(str(error))
            print(events)
            """, "field failed\n['get:a', 'get:b']\n"];
        yield return ["guest MemoryError stays catchable and stops later hashes", """
            from dataclasses import dataclass
            events=[]
            class Key:
             def __init__(self,name):self.name=name
             def __hash__(self):
              events.append(self.name)
              raise MemoryError('guest')
            @dataclass(unsafe_hash=True)
            class Row:
             a:object
             b:object
            for target in [slice(Key('a'),Key('b')),Row(Key('a'),Key('b'))]:
             events.clear()
             try:hash(target)
             except MemoryError as error:print(str(error))
             print(events)
            """, "guest\n['a']\nguest\n['a']\n"];
        yield return ["explicit object hash ignores a dataclass None slot", """
            from dataclasses import dataclass
            @dataclass
            class Row:
             value:object
            row=Row([])
            print(isinstance(object.__hash__(row),int))
            try:hash(row)
            except TypeError:print('unhashable')
            """, "True\nunhashable\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ContextualHashesPreservePythonEffects(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
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
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task NestedSlicesRespectManagedAdmissionAfterHostIO(bool asynchronous, bool tuple)
    {
        var source = "with open('/ready') as stream:stream.read()\nvalue=1\nfor _ in range(depth):value=slice(value,None)\ntry:print(isinstance(" +
            (tuple ? "hash((value,))" : "hash(value)") + ",int))\nexcept (RecursionError,RuntimeError):print('bounded')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var delayed = new DelayedLythonHost(); delayed.SeedFile("/ready", "ready");
        var mock = new MockLythonHost(); mock.SeedFile("/ready", "ready");
        // One level beyond the current 512-level managed structural bound.
        foreach (var depth in new[] { 513, 2 })
        {
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["depth"] = depth } };
            using (var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(mock, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(depth == 513 ? "bounded\n" : "True\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
    }
}
