using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MethodExposureTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "import math\nmethods=[math.sqrt for i in range(2)]\nprint(methods[0] is methods[1])\nprint(methods[0](9))", "True\n3.0\n" },
        { "value='abc'\nmethods=[]\nfor i in range(2): methods.append(value.upper)\nprint(methods[0] is methods[1])\nprint(methods[0](),methods[1]())", "False\nABC ABC\n" },
        { "value='abc'\nmethods=[value.upper for i in range(2)]\nprint(methods[0] is methods[1])\nprint(methods[0](),methods[1]())", "False\nABC ABC\n" },
        { "items=[]\nmethods=[]\nfor i in range(2): methods.append(items.append)\nprint(methods[0] is methods[1])\nmethods[0](1)\nmethods[1](2)\nprint(items)", "False\n[1, 2]\n" },
        { "class Holder:\n    value='abc'\n    methods=[]\n    for i in range(2): methods.append(value.upper)\nprint(Holder.methods[0] is Holder.methods[1])\nprint(Holder.methods[0](),Holder.methods[1]())", "False\nABC ABC\n" },
        { "class C:\n    def f(self): return 1\nx=C()\nold=x.f\nC.f=lambda self: 2\nprint(old is x.f,old(),x.f())", "False 1 2\n" },
        { "items=[1]\nother=[2]\nmethods=[items.pop,other.pop]\nitems=None\nother=None\nprint(methods[0](),methods[1]())\nprint(methods[0] is methods[1])", "1 2\nFalse\n" },
        { "class C:\n    def old(self,x): return x+1\n    def new(self,x): return x+100\n    f=old\nvalue=C()\ndef argument():\n    C.f=C.new\n    return 3\nprint(value.f(argument()),value.f(3))", "4 103\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ExposedMethodsPreserveIdentityReceiverAndLookupOrder(string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost())
                : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
