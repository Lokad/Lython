using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// N36: successful close releases Lython-owned write buffers even while the closed
/// handle stays referenced; only the 64B handle shell remains charged.
/// </summary>
public sealed class FileHandleCloseReleaseTests
{
    [Fact]
    public void CloseReleasesWriteBufferWhileHandleStaysReferenced()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var handle = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite("/f.txt", context);
        handle.Write(PyString.FromString(new string('x', 4096)));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 64);
        handle.Exit();
        GC.KeepAlive(handle);
        Assert.Equal(64, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public async Task CloseAsyncReleasesWriteBufferWhileHandleStaysReferenced()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var handle = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite("/f.txt", context);
        handle.Write(PyString.FromString(new string('x', 4096)));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 64);
        await handle.ExitAsync();
        GC.KeepAlive(handle);
        Assert.Equal(64, context.MemoryGovernor.CurrentCommittedBytes);
    }
}