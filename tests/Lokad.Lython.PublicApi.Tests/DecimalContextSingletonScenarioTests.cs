using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: module-level Default/Basic/Extended contexts are shared constants (free
/// by design like other shared metadata), while constructed contexts stay fresh.
/// Lock-in only; no source change needed (verified by isolated CPython comparison).
/// </summary>
public sealed class DecimalContextSingletonScenarioTests
{
    [Fact]
    public async Task SharedContextsKeepIdentity()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            checks = [decimal.DefaultContext is decimal.DefaultContext]
            checks.append(decimal.BasicContext is decimal.BasicContext)
            checks.append(decimal.ExtendedContext is decimal.ExtendedContext)
            checks.append(decimal.Context() is decimal.Context())
            checks.append(decimal.DefaultContext.prec)
            checks.append(decimal.BasicContext.prec)
            checks.append(decimal.ExtendedContext.prec)
            return checks
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true, false };
        expected.Add(new BigInteger(28));
        expected.Add(new BigInteger(9));
        expected.Add(new BigInteger(9));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SharedContextsRetainFree()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            ds = []
            i = 0
            while i < 2000:
                ds.append(decimal.DefaultContext)
                i = i + 1
            return len(ds)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(2000), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(2000), asyncResult.ReturnValue);
    }
}
