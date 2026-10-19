using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PositionalCallSemanticsTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def f(value,offset=2):\n    value+=offset\n    value*=3\n    return value\nprint(f(4),f(value=5,offset=1))", "18 18\n" },
        { "def f(value,saved=None):\n    saved=locals()\n    value=99\n    return saved,value\nprint(f(7))", "({'value': 7, 'saved': None}, 99)\n" },
        { "def keep(value):\n    def value(): return value\n    return value\nf=keep(7)\nkeep(9)\nprint(f() is f)", "True\n" },
        { "def f(value):\n    if value: return value*f(value-1)\n    return 1\nprint(f(6),f(value=4))", "720 24\n" },
        { "def f(value):\n    del value\n    value=9\n    return value\nprint(f(7),f(value=8))", "9 9\n" },
        { "def f(value): return value+10\ndef argument():\n    print('argument')\n    return -3\nfor target in [abs,f,None,abs]:\n    try: print(target(argument()))\n    except TypeError: print('not-callable')", "argument\n3\nargument\n7\nargument\nnot-callable\nargument\n3\n" },
        { "class C:\n    def __call__(self,value): return value+1\ntarget=C()\nfor i in range(2):\n    print(target(3))\n    C.__call__=lambda self,value: value+100", "4\n103\n" },
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
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
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
