using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Completed host writes relinquish Lython's private staging. The closed shell
// remains batch-owned; the host owns its persisted copy independently.
public sealed class FileHandleCloseReleaseTests
{
    [Fact]
    public void ClosedPublicationReleasesPrivateStagingAndKeepsItsShellFunded()
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var handle = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite("/f.txt", context);
        var opened = context.MemoryGovernor.CurrentCommittedBytes;
        handle.Write(PyString.FromString(new string('x', 4096)));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 64);
        handle.Exit();
        GC.KeepAlive(handle);
        Assert.Equal(opened, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(new string('x', 4096), host.ReadText("/f.txt"));
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ClosedAsyncPublicationReleasesPrivateStagingAndKeepsItsShellFunded()
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var handle = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite("/f.txt", context);
        var opened = context.MemoryGovernor.CurrentCommittedBytes;
        handle.Write(PyString.FromString(new string('x', 4096)));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 64);
        await handle.ExitAsync();
        GC.KeepAlive(handle);
        Assert.Equal(opened, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(new string('x', 4096), host.ReadText("/f.txt"));
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
