using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: DecimalTuple and Context renderer outputs own their payload like other
/// scalar renderer outputs.
/// </summary>
public sealed class DecimalRendererAccountingTests
{
    [Fact]
    public void TupleRendererCommitsExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var rendering = new PyRenderingContext(context);
        var tuple = new PyDecimalTuple(0, new PyTuple(new object[] { new BigInteger(1), new BigInteger(5) }), new BigInteger(-1));
        var text = tuple.RenderPython(rendering);
        Assert.Equal("DecimalTuple(sign=0, digits=(1, 5), exponent=-1)", text.AsString());
        Assert.Same(context.MemoryGovernor, text.OwnerMemoryGovernor);
        // Nested digit renders remain conservatively charged to this execution;
        // no pending construction reservation remains after rendering.
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= text.CommittedOwnedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void ContextRendererCommitsExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var rendering = new PyRenderingContext(context);
        var baseline = context.MemoryGovernor.CurrentCommittedBytes;
        var text = PyDecimalContext.Default().RenderPython(rendering);
        Assert.Same(context.MemoryGovernor, text.OwnerMemoryGovernor);
        Assert.Equal(220L, context.MemoryGovernor.CurrentCommittedBytes - baseline);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
