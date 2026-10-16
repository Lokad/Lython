using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ParameterLocalOrderTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def f(a,*args,b,**kw):return list(locals()),locals()\nprint(f(1,2,b=3,z=4))", "(['a', 'b', 'args', 'kw'], {'a': 1, 'b': 3, 'args': (2,), 'kw': {'z': 4}})\n" },
        { "def f(a,*args,b,**kw):return list(vars()),vars()\nprint(f(1,2,b=3,z=4))", "(['a', 'b', 'args', 'kw'], {'a': 1, 'b': 3, 'args': (2,), 'kw': {'z': 4}})\n" },
        { "def f(a,/,*args,b,c=5,**kw):return list(locals()),locals()\nprint(f(1,2,3,b=4,z=6))", "(['a', 'b', 'c', 'args', 'kw'], {'a': 1, 'b': 4, 'c': 5, 'args': (2, 3), 'kw': {'z': 6}})\n" },
        { "f=lambda a,*args,b,**kw:(list(locals()),locals())\nprint(f(1,2,b=3,z=4))", "(['a', 'b', 'args', 'kw'], {'a': 1, 'b': 3, 'args': (2,), 'kw': {'z': 4}})\n" },
        { "class Box:\n def f(self,a,*args,b,**kw):return list(locals())[1:],(a,args,b,kw)\nprint(Box().f(1,2,b=3,z=4))", "(['a', 'b', 'args', 'kw'], (1, (2,), 3, {'z': 4}))\n" },
        { "def outer(a,*args,b,**kw):\n saved=locals()\n def read():return a,args,b,kw\n b.append(9)\n return saved,read\nvalue=[]\nsaved,read=outer(1,2,b=value,z=4)\nprint(list(saved),saved['b'] is value,read(),saved)", "['a', 'b', 'args', 'kw'] True (1, (2,), [9], {'z': 4}) {'a': 1, 'b': [9], 'args': (2,), 'kw': {'z': 4}}\n" },
        { "def f(a,*args,b,**kw):\n if False:never=0\n first=locals()\n a=7\n del b\n second=vars()\n return list(first),list(second),first['a'],second['a']\nprint(f(1,2,b=3,z=4))", "(['a', 'b', 'args', 'kw'], ['a', 'args', 'kw', 'first'], 1, 7)\n" },
        { "def f(a=1,*args,b=2,**kw):return list(locals()),(a,args,b,kw)\nf.__defaults__=(7,)\nf.__kwdefaults__={'b':8}\nprint(f(z=4))", "(['a', 'b', 'args', 'kw'], (7, (), 8, {'z': 4}))\n" },
        { "def f(a,*args,b,**kw):\n yield list(locals()),(a,args,b,kw)\n a=7\n yield list(vars()),(a,args,b,kw)\nprint(list(f(1,2,b=3,z=4)))", "[(['a', 'b', 'args', 'kw'], (1, (2,), 3, {'z': 4})), (['a', 'b', 'args', 'kw'], (7, (2,), 3, {'z': 4}))]\n" },
        { "def f(a,*args,b,**kw):\n def read():return a\n saved=locals()\n return read,saved\nread,saved=f(1,2,b=3,z=4)\nprint(list(saved),read(),saved['a'])", "['a', 'b', 'args', 'kw', 'read'] 1 1\n" },
        { "def f(a,b=2,*,c=3):return list(locals()),locals()\nprint(f(1))", "(['a', 'b', 'c'], {'a': 1, 'b': 2, 'c': 3})\n" },
        { "def f(a,*args,**kw):return list(locals()),locals()\nprint(f(1,2,z=3))", "(['a', 'args', 'kw'], {'a': 1, 'args': (2,), 'kw': {'z': 3}})\n" },
        { "def f(*,x,y=2):return list(vars()),vars()\nprint(f(x=1))", "(['x', 'y'], {'x': 1, 'y': 2})\n" },
        { "def f(a,*args,b,**kw):return list(locals()),(a,args,b,kw)\nclass Box:pass\nBox.f=f\nprint(Box().f(2,b=3,z=4)[0])", "['a', 'b', 'args', 'kw']\n" },
        { "functions=[lambda a,*args,b,**kw:a,lambda a,*args,b,**kw:b,lambda a,*args,b,**kw:args,lambda a,*args,b,**kw:kw]\nfor f in functions:print(f(1,2,b=3,z=4))", "1\n3\n(2,)\n{'z': 4}\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task OrderedParameterSnapshotsMatchPython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
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
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapingSnapshotRemainsFundedAfterInvocationAndCompilationCleanup(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(
            "def f(a,*args,b,**kw):return locals()\nshared=[7]\nreturn f(shared,shared,b=shared,tag=shared)", budget);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        script.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        var snapshot = Assert.IsType<Dictionary<object, object?>>(result.ReturnValue);
        Assert.Equal(new object[] { "a", "b", "args", "kw" }, snapshot.Keys);
        var expected = new List<object?> { new System.Numerics.BigInteger(7) };
        Assert.Equal(expected, Assert.IsType<List<object?>>(snapshot["a"]));
        Assert.Equal(expected, Assert.IsType<List<object?>>(snapshot["b"]));
        Assert.Equal(expected, Assert.IsType<List<object?>>(Assert.Single(Assert.IsType<object?[]>(snapshot["args"]))));
        Assert.Equal(expected, Assert.IsType<List<object?>>(Assert.IsType<Dictionary<object, object?>>(snapshot["kw"])["tag"]));
        // All projected values have had their final use before ownership ends.
        result.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
