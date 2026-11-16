using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IteratorScalarIdentityTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def values():\n value=int('1000')\n yield value\n yield value\ng=values()\nfirst=next(g)\nsecond=next(g)\nprint(first,second,first is second)", "1000 1000 True\n" },
        { "integer=int('1000')\npairs=[(integer,),(integer,)]\ng=(pair[0] for pair in pairs)\nfirst=next(g)\nsecond=next(g)\nprint(first is integer,second is first)", "True True\n" },
        { "seen=[]\nclass Start:\n def __add__(self,value):\n  seen.append(value)\n  return value\nresult=sum((value+1000 for value in range(1)),Start())\nprint(result,result is seen[0],len(seen))", "1000 True 1\n" },
        { "seen=[]\ng=(value+1000 for value in range(3))\ndef key(value):\n seen.append(value)\n if len(seen)==1:print('skip',next(g))\n return value\nwinner=max(g,key=key)\nprint(winner,winner is seen[-1])", "skip 1001\n1002 True\n" },
        { "print(list(value for value in [None,0,False]))\nprint(sum(value for value in [False,1,True]))\nprint(sum(value+0 for value in [9223372036854775807,1,-2]))", "[None, 0, False]\n2\n9223372036854775806\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NativeAndObjectConsumersPreserveObservedValues(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var result = script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
