using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PrimitiveStorageLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundedBooleanTurnoverKeepsItsLiveBudget(bool asynchronous)
    {
        var result = await Run("""
            flags = [True] * 64
            for i in range(2048):
                flags[0] = bool(i & 1)
                del flags[-1]
                flags.append(bool(i & 1))
                assert flags[0] is bool(i & 1)
            assert len(set(flags)) == 1
            assert len({True, 1, False, 0}) == 2
            return len(flags)
            """, asynchronous, 65536);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(64), result.ReturnValue);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 65536);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedBooleanSlotsNeedTheirNumericStorageAllowance(bool asynchronous)
    {
        var result = await Run("""
            flags = [bool(i & 1) for i in range(4096)]
            return len(flags)
            """, asynchronous, 262144);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }

    private static async Task<LythonExecutionResult> Run(string body, bool asynchronous, long cap)
    {
        var source = "with open('/ready', 'r') as stream:\n    stream.read()\n" + body;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = cap };
        if (asynchronous)
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/ready", "ready");
            var result = await script.RunAsync(host, options);
            Assert.True(host.CompletedAsynchronously > 0);
            return result;
        }
        var synchronousHost = new MockLythonHost();
        synchronousHost.SeedFile("/ready", "ready");
        return script.Run(synchronousHost, options);
    }
}
