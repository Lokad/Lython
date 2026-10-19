using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CounterIntegerAdmissionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefusedCounterUpdateCannotResumeOrReturnSuccess(bool asynchronous, bool finallyReturn)
    {
        var body = finallyReturn ? "try:\n update(delta)\nfinally:\n return 0"
            : "try:\n update(delta)\nexcept MemoryError:\n print('caught')";
        var source = "from collections import Counter\ncounts = Counter(x=1 << 524288)\n"
            + "delta = {'x':1}\nupdate = counts.update\nprint('ready')\n" + body + "\nprint('after')\nreturn counts['x']";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost();
        ILythonMemoryReservation? padding = null;
        host.OnStandardOutputWrite = () =>
        {
            if (padding is null)
                Assert.True(budget.TryReserve(1024 * 1024 - budget.ReservedBytes - 8192, out padding));
        };
        try
        {
            using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.False(result.Success);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.True(result.FailureAttribution?.Requested >= 65536);
            Assert.Equal("ready\n", result.StandardOutput);
            Assert.NotNull(padding);
        }
        finally { padding?.Dispose(); }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedCountAliasesStayFundedAndSequentialRunsReturnToBaseline(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            from collections import Counter
            counts = Counter(x=1 << 524288)
            counts.update(x=1)
            return [counts['x'], counts['x']]
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        for (var i = 0; i < 2; i++)
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var aliases = Assert.IsType<List<object?>>(result.ReturnValue);
                Assert.Equal(2, aliases.Count);
                Assert.Equal((BigInteger.One << 524288) + 1, Assert.IsType<BigInteger>(aliases[0]));
                Assert.Equal(aliases[0], aliases[1]);
                Assert.True(budget.ReservedBytes - baseline >= 65536);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
