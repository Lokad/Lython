using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PositionalCallSemanticsTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def f(): return 7\nprint(f())", "7\n" },
        { "def f(a,b,c,d): return a,b,c,d\nprint(f(1,2,3,4))", "(1, 2, 3, 4)\n" },
        { "def f(a,b=2): return a,b\nf.__defaults__=(9,)\nprint(f(1),f(1,3))", "(1, 9) (1, 3)\n" },
        { "def f(a,b=2): return a,b\ntry: f()\nexcept TypeError: print('missing')\ntry: f(1,2,3)\nexcept TypeError: print('extra')\nprint(f(4))", "missing\nextra\n(4, 2)\n" },
        { "def old(value): return value+1\ndef newer(value): return value+100\nf=old\ndef argument():\n    global f\n    f=newer\n    return 3\nprint(f(argument()),f(3))", "4 103\n" },
        { "def keep(value):\n    def read(): return value\n    return read\na=[]\none=keep(a)\ntwo=keep([])\na.append(7)\nprint(one() is a,one(),two())", "True [7] []\n" },
        { "def f(value): return locals()['value']\nprint(f(9))\ndef g(*values,**named): return values,named\nprint(g(1,2,x=3))", "9\n((1, 2), {'x': 3})\n" },
        { "def f(a,*,b=2): return a,b\nprint(f(1),f(3,b=4))", "(1, 2) (3, 4)\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task CallsPreserveBindingAliasesAndTargetOrder(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
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
}
