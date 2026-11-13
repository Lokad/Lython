using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeTupleReturnTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def pair(value,shared=[]): return value,shared\npayload=[]\na,b=pair(payload)\nc,d=pair(payload)\nb.append('kept')\nprint(a is payload,a is c,b is d,d)", "True True True ['kept']\n" },
        { "def pair(): return 1,9\nbox=[0,0]\nindex,box[index]=pair()\nprint(index,box)", "1 [0, 9]\n" },
        { "def pair(value): return value,value\nfor unused in range(2):\n a,b=pair([])\nprint(a is b)", "True\n" },
        { "def many(value): return value,2,3,4,5,6,7,8\nfor unused in range(2):\n a,b,c,d,e,f,g,h=many([])\nprint(a,b,c,d,e,f,g,h)", "[] 2 3 4 5 6 7 8\n" },
        { "def first(a,b,c,d,e,f,g,h): return (a,)\npayload=[]\nkept,=first(payload,2,3,4,5,6,7,8)\nprint(kept is payload)", "True\n" },
        { "def pair(): return 1,2\na,b=kept=pair()\nfirst,*rest=pair()\nprint(type(kept).__name__,kept,a,b,first,rest)", "tuple (1, 2) 1 2 1 [2]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task TupleConsumersKeepPythonValuesAndTargetOrder(string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
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

    [Fact]
    public async Task PartiallyExecutedTupleProducerTransfersOnceThroughActualIo()
    {
        const string source = """
            def read_tail():
                with open('/input.txt') as stream:
                    return stream.read()
            def pair(value):
                return value.lower(),read_tail()
            first,second=pair('RÉSUMÉ😀')
            return first,second
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/input.txt", "tail😀");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                Assert.Equal(new object?[] { "résumé😀", "tail😀" }, values);
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
