using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// M05: bytes.partition builds its result tuple beside self-tracked item
// slices, and the raw callable bypasses every funnel, so the container
// adopts at its factory with refund; drops reclaim on sweep.
public sealed class BytesPartitionAccountingTests
{
    [Fact]
    public void PartitionTupleAdoptsContainer()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var receiver = new PyBytes(new byte[] { 97, 98, 99 }, context.MemoryGovernor, span);
        var separator = new PyBytes(new byte[] { 98 }, context.MemoryGovernor, span);
        Assert.True(LythonRuntime.BytesMembers.TryGetMember(receiver, "partition", out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        var before = context.State.CallTemporaries.Count;
        var result = Assert.IsType<PyTuple>(callable.Invoke(
            new CallArgumentValue[] { CallArgumentValue.Positional(separator) }, span, context));
        Assert.Equal(3, result.Count);
        // The two item slices self-track through CreateBytes; the tuple container
        // adopts here. The aliased receiver and separator add nothing new.
        Assert.Equal(before + 3, context.State.CallTemporaries.Count);
        GC.KeepAlive(result);
        GC.KeepAlive(receiver);
        GC.KeepAlive(separator);
    }
}
