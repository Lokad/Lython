using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StatisticsReductionTests
{
    public static TheoryData<string, string> Cases => new()
    {
        { "print(statistics.correlation([1e100,2e100,3e100],[1e100,2e100,3e100]))", "1.0\n" },
        { "print(statistics.correlation([1e-100,2e-100,3e-100],[1e-100,2e-100,3e-100]))", "1.0\n" },
        { "print(statistics.covariance([1.,2.,3.],[2.,4.,6.]))", "2.0\n" },
        { "print(statistics.fmean([1e16,1.,-1e16]))", "0.3333333333333333\n" },
        { "print(statistics.fmean([1e308,1e308]))", "OverflowError\n" },
        { "print(statistics.fmean([1.7976931348623157e308,1e292]))", "OverflowError\n" },
        { "print(statistics.fmean([1.7976931348623157e308,1e292,-1.7976931348623157e308]))", "OverflowError\n" },
        { "print(statistics.covariance([1e308,1e308],[1.,2.]))", "OverflowError\n" },
        { "print(statistics.correlation([1.,1.],[2.,3.]))", "StatisticsError\n" },
        { "print(math.isnan(statistics.fmean([float('nan'),1.])))", "True\n" },
        { "print(statistics.fmean([float('inf'),float('-inf'),float('nan')]))", "ValueError\n" },
        { "print(statistics.fmean([1e308,1e308],weights=[1.,1.]))", "inf\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ReductionsPreserveFloatingResultsAndErrors(string statement, string expected)
    {
        using var script = new LythonEngine().Compile("import statistics,math\ntry:\n " + statement + "\nexcept Exception as e:\n print(type(e).__name__)");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FmeanStopsAtOverflowAndPreparesWeightsBeforeData(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import statistics
            def values():
                try:
                    yield 1e308
                    yield 1e308
                    print('unreachable tail')
                finally: print('closed')
            retained=values()
            try: statistics.fmean(retained)
            except OverflowError: print('overflow')
            retained.close()
            def weights():
                print('weights')
                yield 1.
            try: statistics.fmean([],weights=weights())
            except statistics.StatisticsError: print('empty')
            def data():
                print('data')
                yield 1.
                print('extra')
                yield 2.
                print('unreachable data tail')
            try: statistics.fmean(data(),weights=[1.])
            except statistics.StatisticsError: print('mismatch')
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("overflow\nclosed\nweights\nempty\ndata\nextra\nmismatch\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FmeanUsesBoundedScratchAndReturnsSharedAllowance(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import statistics\nreturn int(statistics.fmean(range(20000)))");
        var budget = new LythonMemoryBudget(32768);
        for (var pass = 0; pass < 2; pass++)
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(9999), result.ReturnValue);
                Assert.True(result.PeakExecutionMemoryBytes < 32768);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
