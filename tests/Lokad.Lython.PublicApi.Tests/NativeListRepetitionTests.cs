using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeListRepetitionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargeInPlaceRepetitionPreservesOddPeriodAndAliases(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var script = new LythonEngine().Compile("""
            original = [[], {}, object()]
            items = original[:]
            items *= 6001
            return [len(items), [items[i] is original[i % 3]
                for i in [0, 1, 2, 4095, 4096, 4097, 8191, 8192,
                    12287, 12288, 16383, 16384, 18002]]]
            """, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            var value = Assert.IsType<List<object?>>(result.ReturnValue);
            Assert.Equal(new BigInteger(18003), value[0]);
            Assert.All(Assert.IsType<List<object?>>(value[1]), item => Assert.Equal(true, item));
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepetitionPreservesCyclesAliasesAndSharedBudgetCleanup(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile("""
            def check():
                source = []
                source.append(source)
                repeated = source * 3
                return [len(repeated), repeated is source, repeated[0] is source,
                        repeated[0] is repeated[1], len(source), len(source * 0),
                        len(source * -2)]
            return check()
            """, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        for (var batch = 0; batch < 2; batch++)
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new List<object?> { new BigInteger(3), false, true, true,
                    BigInteger.One, BigInteger.Zero, BigInteger.Zero }, result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
