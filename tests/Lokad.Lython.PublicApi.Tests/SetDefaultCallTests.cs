using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SetDefaultCallTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "receiver-before-arguments", """
            d={}
            old=d
            fallback=[]
            def argument():
                global d
                d={'x':99}
                return 'x'
            print(d.setdefault(argument(),fallback) is fallback,old['x'] is fallback,d['x'])
            """, "True True 99\n" },
        { "dynamic-member", """
            class Custom:
                def __getattribute__(self,name):
                    print('lookup',name)
                    return lambda key,default:(key,default)
            def argument(name,value):
                print('argument',name)
                return value
            print(Custom().setdefault(argument('key','x'),argument('default',7)))
            """, "lookup setdefault\nargument key\nargument default\n('x', 7)\n" },
        { "not-callable", """
            class Custom:setdefault={}
            def argument():
                print('argument')
                return 'x'
            try:Custom().setdefault(argument(),7)
            except TypeError:print('not callable')
            """, "argument\nnot callable\n" },
        { "exposed-identity", """
            d={}
            print(d.setdefault is d.setdefault,d.setdefault.__self__ is d)
            method=d.setdefault
            fallback=[]
            print(method('x',fallback) is fallback,d.setdefault('x',[]) is fallback)
            """, "False True\nTrue True\n" },
        { "invalid-forms", """
            d={}
            for call in (lambda:d.setdefault(),lambda:d.setdefault(1,2,3),lambda:d.setdefault(key='x')):
                try:call()
                except TypeError:print('invalid')
            print(d)
            """, "invalid\ninvalid\ninvalid\n{}\n" },
        { "expanded-exposed-unbound", """
            events=[]
            class Key:
                def __hash__(self):
                    events.append('hash')
                    return 1
            key=Key()
            d={}
            method=d.setdefault
            for call in (lambda:method(key,7),lambda:d.setdefault(*(key,8)),lambda:dict.setdefault(d,key,9)):
                events.clear()
                print(call(),events)
            """, "7 ['hash']\n7 ['hash']\n7 ['hash']\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task OrdinaryAndGeneralCallsPreservePythonBehavior(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join(";", script.Diagnostics));
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
