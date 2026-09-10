using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: stat modified_at owns its fresh text per access instead of escaping.
/// Twenty thousand retained timestamps must exceed a 3MB budget in both
/// modes; pre-fix they fit beside the already-charged stat boxes.
/// </summary>
public sealed class StatModifiedAtAccountingScenarioTests
{
    private const long TimestampBudgetBytes = 3145728;

    private static MockLythonHost SeededHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/d/f0", "x");
        return host;
    }

    [Fact]
    public async Task ManyRetainedTimestampsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            i = 0
            while i < 20000:
                objs.append(os.stat("/d/f0").modified_at)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = TimestampBudgetBytes };
        var sync = script.Run(SeededHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= TimestampBudgetBytes);

        var asyncResult = await script.RunAsync(SeededHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= TimestampBudgetBytes);
    }

    [Fact]
    public async Task StatTimestampsBehave()
    {
        var script = new LythonEngine().Compile("""
            import os
            st = os.stat("/d/f0")
            return [st.exists, st.size, st.modified_at]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, new BigInteger(1), "1970-01-01T00:00:00.0000000+00:00" };
        var sync = script.Run(SeededHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(SeededHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}