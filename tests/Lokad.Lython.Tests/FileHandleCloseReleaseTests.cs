using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Closing publishes intact bytes. Conservative publication storage remains
// funded until the execution owner relinquishes it, independently of CLR GC.
public sealed class FileHandleCloseReleaseTests
{
    [Fact]
    public void ClosedPublicationStaysFundedUntilOwnerCleanup()
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var handle = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite("/f.txt", context);
        handle.Write(PyString.FromString(new string('x', 4096)));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 64);
        handle.Exit();
        GC.KeepAlive(handle);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= 4096);
        Assert.Equal(new string('x', 4096), host.ReadText("/f.txt"));
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ClosedAsyncPublicationStaysFundedUntilOwnerCleanup()
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var handle = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite("/f.txt", context);
        handle.Write(PyString.FromString(new string('x', 4096)));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 64);
        await handle.ExitAsync();
        GC.KeepAlive(handle);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= 4096);
        Assert.Equal(new string('x', 4096), host.ReadText("/f.txt"));
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
