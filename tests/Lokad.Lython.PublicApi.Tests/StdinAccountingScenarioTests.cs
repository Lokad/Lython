using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG21: standard-input reads fail closed under the execution budget: the
/// host returns the whole line or stream, the host-byte limit applies, and
/// the governed decode charges the payload. A 200KiB line fails a 64KiB
/// budget and reads back at 1MiB, in both modes and through both input()
/// and sys.stdin entry points.
/// </summary>
public sealed class StdinAccountingScenarioTests
{
    private static MockLythonHost SeededHost()
    {
        var host = new MockLythonHost();
        host.SeedStandardInput(new string('x', 200000) + "\n");
        return host;
    }

    [Fact]
    public async Task OversizedInputLineRespectsBudget()
    {
        var script = new LythonEngine().Compile("v = input()\nreturn len(v)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(SeededHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(SeededHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task OversizedStdinReadRespectsBudget()
    {
        var script = new LythonEngine().Compile("import sys\ndata = sys.stdin.read()\nreturn len(data)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(SeededHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(SeededHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyStdinReadsBack()
    {
        var script = new LythonEngine().Compile("v = input()\nreturn len(v)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new BigInteger(200000);
        var sync = script.Run(SeededHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(SeededHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}