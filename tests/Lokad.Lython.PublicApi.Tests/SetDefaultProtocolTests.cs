using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SetDefaultProtocolTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "hash-once", """
            events=[]
            class Key:
                def __hash__(self):
                    events.append('hash')
                    return 1
            key=Key()
            d=DICTIONARY
            print(d.setdefault(key,7),events)
            events.clear()
            print(d.setdefault(key,9),events)
            """, "7 ['hash']\n7 ['hash']\n" },
        { "collision-miss", """
            events=[]
            class Key:
                def __init__(self,name):self.name=name
                def __hash__(self):return 1
                def __eq__(self,other):
                    events.append((self.name,other.name))
                    return False
            first=Key('first')
            second=Key('second')
            d=DICTIONARY
            d[first]=3
            print(d.setdefault(second,7),len(d),events)
            """, "7 2 [('first', 'second')]\n" },
        { "equal-key-alias", """
            events=[]
            class Key:
                def __init__(self,name):self.name=name
                def __hash__(self):return 1
                def __eq__(self,other):
                    events.append((self.name,other.name))
                    return True
            first=Key('first')
            second=Key('second')
            value=[]
            d=DICTIONARY
            d[first]=value
            print(d.setdefault(second,[]) is value,next(iter(d)) is first,events)
            """, "True True [('first', 'second')]\n" },
        { "value-change", """
            class Key:
                def __hash__(self):return 1
                def __eq__(self,other):
                    d[first]=99
                    return True
            first=Key()
            second=Key()
            d=DICTIONARY
            d[first]=3
            print(d.setdefault(second,7),len(d))
            """, "99 1\n" },
        { "clear-in-equality", """
            class Key:
                def __hash__(self):return 1
                def __eq__(self,other):
                    d.clear()
                    return True
            first=Key()
            second=Key()
            d=DICTIONARY
            d[first]=3
            print(d.setdefault(second,7),len(d),next(iter(d)) is second)
            """, "7 1 True\n" },
        { "replace-in-equality", """
            class Key:
                def __init__(self,name):self.name=name
                def __hash__(self):return 1
                def __eq__(self,other):
                    if self.name=='old':
                        d.clear()
                        d[replacement]=99
                    return True
            first=Key('old')
            second=Key('query')
            replacement=Key('replacement')
            d=DICTIONARY
            d[first]=3
            print(d.setdefault(second,7),len(d),next(iter(d)) is replacement)
            """, "99 1 True\n" },
        { "insert-in-hash", """
            events=[]
            class Key:
                def __init__(self):self.insert=True
                def __hash__(self):
                    events.append('hash')
                    if self.insert:
                        self.insert=False
                        d[self]=99
                    return 1
            key=Key()
            d=DICTIONARY
            print(d.setdefault(key,7),len(d),events)
            """, "99 1 ['hash', 'hash']\n" },
        { "hash-failure", """
            class Key:
                def __hash__(self):raise ValueError('hash failed')
            d=DICTIONARY
            try:d.setdefault(Key(),7)
            except ValueError:print('failed',len(d))
            """, "failed 0\n" },
        { "native-default-alias", """
            d=DICTIONARY
            value=[]
            print(d.setdefault('x',value) is value,d.setdefault('x',[]) is value)
            value.append(7)
            print(d['x'],d.setdefault('none') is None,len(d))
            """, "True True\n[7] True 2\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task SetDefaultPreservesPythonKeyCallbacksAndAliases(string name, string source, string expected)
    {
        foreach (var dictionary in new[] { "{}", "collections.defaultdict(list)" })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using (var script = new LythonEngine().Compile("import collections\n" + source.Replace("DICTIONARY", dictionary), budget))
            {
                Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
                var baseline = budget.ReservedBytes;
                foreach (var asynchronous in new[] { false, true })
                {
                    using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                    {
                        Assert.True(result.Success, name + ": " + result.Failure?.Message);
                        Assert.Equal(expected, result.StandardOutput);
                    }
                    Assert.Equal(baseline, budget.ReservedBytes);
                }
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
