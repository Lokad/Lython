using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryValueLifetimeTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "d={'a':1000}\nfor key in ('a','a'): d[key]=d.get(key,0)+1\ncopy=d.copy();other=dict(d)\nvalue=d['a']\nprint(value,copy['a'] is value,other['a'] is value,list(d.values())[0] is value)\nfor key in ('a',): d[key]=d.get(key,0)+1\nprint(value,copy['a'],d['a'],value is d['a'])", "1002 True True True\n1002 1002 1003 False\n" },
        { "d={'a':1000,'b':2000,'c':3000}\nfor key in ('a','b','c'): d[key]=d.get(key,0)+1\ndel d['a']\nprint(list(d.items()))\nd['x']=None;d['y']=False;d['z']='value'\nprint(list(d.items()))\nold=d['b'];d['b']=None\nprint(old,d.pop('c'),d.setdefault('b',9),list(d.items()))", "[('b', 2001), ('c', 3001)]\n[('b', 2001), ('c', 3001), ('x', None), ('y', False), ('z', 'value')]\n2001 3001 None [('b', None), ('x', None), ('y', False), ('z', 'value')]\n" },
        { "d={'a':1000}\nfor key in ('a','a'): d[key]=d.get(key,0)+1\nfor i in range(20): d[str(i)]=i\ncopy=d.copy()\nprint(d['a'],copy['a'] is d['a'],len(d))\nd.clear()\nprint(copy['a'],len(d))", "1002 True 21\n1002 0\n" },
        { "d={'a':(1<<55)-1,'b':-(1<<55)}\nfor key in ('a','a'): d[key]=d.get(key,0)+1\nfor key in ('b','b'): d[key]=d.get(key,0)+-1\nprint(d)\nd['a']=1<<100\nfor key in ('a','a'): d[key]=d.get(key,0)+1\nprint(d['a']==(1<<100)+2)", "{'a': 36028797018963969, 'b': -36028797018963970}\nTrue\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task UpdatesCopiesAndRemovalPreserveValuesIdentityAndFunding(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
