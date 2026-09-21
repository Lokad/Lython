using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// R19: unbound type-method and data-descriptor lookups consult the existing
// per-constructor cache before constructing a probe receiver. The cache hit
// returns the identical descriptor and unsupported members keep failing;
// only the wasted probe work is gone.
public sealed class UnboundMemberCacheTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void RepeatedMethodLookupReturnsIdenticalDescriptor()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        Assert.True(context.TryGetBuiltin("list", out var listType));
        Assert.True(PyMemberAccess.TryResolve(listType!, "append", context, Span, out var first));
        Assert.True(PyMemberAccess.TryResolve(listType!, "append", context, Span, out var second));
        Assert.Same(first, second);
    }

    [Fact]
    public void UnsupportedMethodStillMisses()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        Assert.True(context.TryGetBuiltin("list", out var listType));
        Assert.False(PyMemberAccess.TryResolve(listType!, "no_such_method_xyz", context, Span, out _));
    }

    [Fact]
    public void RepeatedDataDescriptorLookupReturnsIdenticalDescriptor()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        Assert.True(context.TryGetBuiltin("int", out var intType));
        Assert.True(PyMemberAccess.TryResolve(intType!, "real", context, Span, out var first));
        Assert.True(PyMemberAccess.TryResolve(intType!, "real", context, Span, out var second));
        Assert.Same(first, second);
    }
}
