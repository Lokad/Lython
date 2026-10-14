using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// bytes.partition funds its result tuple and item slices at construction.
// Their estimates remain with the finite execution owner.
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

        var result = Assert.IsType<PyTuple>(callable.Invoke(
            new CallArgumentValue[] { CallArgumentValue.Positional(separator) }, span, context));
        Assert.Equal(3, result.Count);
        GC.KeepAlive(result);
        GC.KeepAlive(receiver);
        GC.KeepAlive(separator);
    }
}
