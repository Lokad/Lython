using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG04: small containers pay for their backing storage, so ordinary records
/// cannot accumulate outside the memory budget.
/// </summary>
public sealed class SmallCollectionAccountingScenarioTests
{
    [Fact]
    public async Task NestedSmallListsStayCharged()
    {
        // MG04 probe: 5,000 six-item lists hold 30,000 reference slots in
        // small backing arrays alone, far above a 256 KiB budget.
        var script = new LythonEngine().Compile(
            """
            return [[0, 1, 2, 3, 4, 5] for i in range(5000)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }
}
