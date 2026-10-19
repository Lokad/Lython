using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeListRepetitionTests
{
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
