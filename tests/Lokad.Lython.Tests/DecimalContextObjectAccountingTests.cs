using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: constructed decimal Context values own their storage like other
/// constructed values; the shared run context stays aliased and free.
/// </summary>
public sealed class DecimalContextObjectAccountingTests
{
    private static object InvokeFactory(
        string name,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        var method = typeof(LythonRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(name + " not found.");
        return method.Invoke(null, [Array.Empty<object>(), span, context])
            ?? throw new InvalidOperationException(name + " returned null.");
    }

    [Fact]
    public void ConstructedContextsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = InvokeFactory("DecimalContextCtor", context, span);
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeFactory("DecimalLocalContext", context, span);
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        var member = PyDecimalContext.Default().TryGetMember("copy", out var value)
            ? value
            : throw new InvalidOperationException("copy member not found.");
        _ = ((LythonRuntime.ICallable)member).Invoke([], span, context);
        Assert.Equal(3L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
