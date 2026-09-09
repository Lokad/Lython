using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG06: retained dynamically rendered strings pay for their bytes instead of
/// escaping beside the charged source values.
/// </summary>
public sealed class BytesReprAccountingScenarioTests
{
    [Fact]
    public async Task RetainedBytesReprsStayCharged()
    {
        // MG06 probe: 100 retained reprs of 10 KiB payloads hold megabytes
        // of escape-expanded text under a 256 KiB budget.
        var script = new LythonEngine().Compile(
            """
            data = bytes(10000)
            out = []
            for i in range(100):
                out.append(repr(data))
            return len(out)
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
