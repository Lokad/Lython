using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Counts are observable Python callbacks, never interpreter checkpoints/fuel.
public sealed class HashQueryCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["dictionary queries and removals", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            d={key:7}
            print(d[key],d.get(key),key in d)
            print(d.pop(key))
            d[key]=8
            del d[key]
            print(events)
            """, "7 7 True\n7\n['hash', 'hash', 'hash', 'hash', 'hash', 'hash', 'hash']\n"];
        yield return ["set queries and removals", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            s={key}
            print(key in s)
            s.add(key)
            s.remove(key)
            s.add(key)
            s.discard(key)
            print(events)
            """, "True\n['hash', 'hash', 'hash', 'hash', 'hash', 'hash']\n"];
        yield return ["stored set pop has no hash callback", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            s={key}
            key.fail=True
            print(s.pop() is key,len(s),events)
            """, "True 0 ['hash']\n"];
        yield return ["native dictionary copies reuse stored hashes", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            d={key:7}
            copied=d.copy()
            constructed=dict(d)
            print(len(copied),len(constructed),events)
            """, "1 1 ['hash']\n"];
        yield return ["tuple key queries", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            d={(key,):7}
            print(d[(key,)],d.get((key,)),(key,) in d)
            del d[(key,)]
            print(events)
            """, "7 7 True\n['hash', 'hash', 'hash', 'hash', 'hash']\n"];
        yield return ["dictionary callback failures preserve storage", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            d={key:7}
            key.fail=True
            def drop():del d[key]
            for action in [lambda:d[key],lambda:d.get(key),lambda:key in d,lambda:d.pop(key),drop]:
             try:action()
             except ValueError as error:print(str(error))
            key.fail=False
            print(len(d),d[key])
            """, "hash failed\nhash failed\nhash failed\nhash failed\nhash failed\n1 7\n"];
        yield return ["set callback failures preserve storage", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            s={key}
            key.fail=True
            for action in [lambda:key in s,lambda:s.remove(key),lambda:s.discard(key)]:
             try:action()
             except ValueError as error:print(str(error))
            key.fail=False
            print(len(s))
            """, "hash failed\nhash failed\nhash failed\n1\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task QueriesInvokeHashAndPropagateFailures(string name, string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
        using var synchronous = script.Run(new MockLythonHost());
        Assert.True(synchronous.Success, name + ": " + synchronous.Failure?.Message);
        Assert.Equal(expected, synchronous.StandardOutput);
        using var asynchronous = await script.RunAsync(new MockLythonHost());
        Assert.True(asynchronous.Success, name + ": " + asynchronous.Failure?.Message);
        Assert.Equal(expected, asynchronous.StandardOutput);
    }
}
