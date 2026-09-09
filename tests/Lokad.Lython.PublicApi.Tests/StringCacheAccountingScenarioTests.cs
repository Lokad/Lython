using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG06: lazily allocated string caches pay for their footprint instead of
/// escaping beside the charged UTF-8 storage.
/// </summary>
public sealed class StringCacheAccountingScenarioTests
{
    [Fact]
    public async Task RuneIndexCacheIsCharged()
    {
        // MG06 probe: indexing a 400 KiB string builds an 800 KiB rune-offset
        // cache plus the decoded text, far above a 600 KiB budget.
        var script = new LythonEngine().Compile(
            """
            s = "é" * 200000
            return s[100]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 614400 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }
}
