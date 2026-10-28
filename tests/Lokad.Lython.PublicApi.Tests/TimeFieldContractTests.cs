using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TimeFieldContractTests
{
    [Theory]
    [InlineData("time.strptime('02 29','%m %d')", "(1900, 2, 29, 0, 0, 0, 0, 60, -1)\n")]
    [InlineData("time.strptime('2016-12-31 23:59:60','%Y-%m-%d %H:%M:%S')", "(2016, 12, 31, 23, 59, 60, 5, 366, -1)\n")]
    [InlineData("time.strptime('61','%S')", "(1900, 1, 1, 0, 0, 61, 0, 1, -1)\n")]
    [InlineData("time.strptime('2023-366','%Y-%j')", "(2024, 1, 1, 0, 0, 0, 0, 366, -1)\n")]
    [InlineData("time.strptime('Sun 2024-01-02','%a %Y-%m-%d')", "(2024, 1, 2, 0, 0, 0, 6, 2, -1)\n")]
    public async Task ParsingPreservesFieldsInsteadOfRequiringDatetime(string expression, string expected)
    {
        using var script = new LythonEngine().Compile("import time\nprint(tuple(" + expression + "))");
        var budget = new LythonMemoryBudget(1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedFormatterPreservesLeapSecondsAndDatetimeValidation(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import time,datetime
            t=time.strptime('2016-12-31 23:59:60','%Y-%m-%d %H:%M:%S')
            assert time.strftime('%Y-%m-%d %H:%M:%S %G-W%V-%u %j',t) == '2016-12-31 23:59:60 2016-W52-6 366'
            assert time.strftime('%c|%x|%X',t) == 'Sat Dec 31 23:59:60 2016|12/31/16|23:59:60'
            assert time.strftime('%Y-%m-%d %H:%M:%S',time.strptime('02 29','%m %d')) == '1900-02-29 00:00:00'
            for text,format in [('02 29','%m %d'),('60','%S'),('61','%S')]:
                try:datetime.datetime.strptime(text,format)
                except ValueError:pass
                else:raise AssertionError('datetime must validate fields')
            print('ok')
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ok\n", result.StandardOutput);
    }
}
