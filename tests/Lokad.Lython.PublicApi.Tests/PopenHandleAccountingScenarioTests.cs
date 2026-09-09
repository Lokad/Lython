using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG21: retained Popen handles own the 128B constructed-value unit plus 64B
/// per created pipe-stream wrapper for the handle lifetime. Uncompleted
/// handles isolate wrapper growth: no output is ever captured.
/// </summary>
public sealed class PopenHandleAccountingScenarioTests
{
    private const string BuildHandles =
        """
        import subprocess
        ps = []
        i = 0
        while i < 500:
            p = subprocess.Popen(["tool"])
            ps.append(p)
            i = i + 1
        return len(ps)
        """;

    private const string BuildPipeHandles =
        """
        import subprocess
        ps = []
        i = 0
        while i < 500:
            p = subprocess.Popen(["tool"], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            ps.append(p)
            i = i + 1
        return [len(ps), ps[0].args]
        """;

    [Fact]
    public async Task ManyRetainedPopenHandlesStayCharged()
    {
        var script = new LythonEngine().Compile(BuildHandles);
        Assert.True(script.IsValid);
        // Calibration: pre-fix peak is 201664, so this fits; the 128B-per-handle
        // backstop pushes the post-fix peak (~265664) over.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 230000 };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ManyRetainedPipeStreamsStayCharged()
    {
        var script = new LythonEngine().Compile(BuildPipeHandles);
        Assert.True(script.IsValid);
        // Calibration: pre-fix peak is 201664, so this fits; handle plus two
        // 64B stream slots per Popen push the post-fix peak (~329664) over.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 270000 };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyPopenHandlesSucceed()
    {
        var script = new LythonEngine().Compile(BuildPipeHandles);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { new BigInteger(500), new List<object?> { "tool" } };
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableSubprocess();
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
