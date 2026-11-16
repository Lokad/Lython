using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RangeSlotTransportTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def identity(value):return value\nfor item in range(1000,1003):\n result=identity(item)\n print(result is item)",
            "True\nTrue\nTrue\n" },
        { "def identity(value):return value\ndef check():\n for item in range(1000,1003):\n  result=identity(item)\n  print(result is item)\ncheck()",
            "True\nTrue\nTrue\n" },
        { "rows=[]\nfunctions=[]\nfor item in range(1000,1003):\n alias=item\n rows.append((alias,item is alias))\n functions.append(lambda:item)\nprint(rows,[f() for f in functions])",
            "[(1000, True), (1001, True), (1002, True)] [1002, 1002, 1002]\n" },
        { "def make():\n for item in range(1000,1003):\n  yield item\n  yield item\nvalues=make()\nfirst=next(values)\nsecond=next(values)\nprint(first is second,list(values))",
            "True [1001, 1001, 1002, 1002]\n" },
        { "print([item for item in range(9223372036854775806,9223372036854775807,2)])",
            "[9223372036854775806]\n" },
        { "print([item for item in range(-9223372036854775807,-9223372036854775808,-2)])",
            "[-9223372036854775807]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ObservedIdentitiesAndMachineBoundaryValuesRemainExact(string source, string expected)
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

    [Fact]
    public async Task ModuleRangeAliasesSurviveRealIoAndReturnFunding()
    {
        const string source = """
            rows=[]
            for item in range(1000,1003):
                alias=item
                with open('ready') as stream:
                    stream.read()
                rows.append((item,alias,item is alias))
            print(rows)
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/ready", "ready");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("[(1000, 1000, True), (1001, 1001, True), (1002, 1002, True)]\n", result.StandardOutput);
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.Equal(3, host.ClosedFileCount);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
