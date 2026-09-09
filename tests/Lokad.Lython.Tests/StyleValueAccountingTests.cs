using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG17: constructed style values commit object storage; internal defaults
/// built without a context stay free.
/// </summary>
public sealed class StyleValueAccountingTests
{
    private static object CreateFont(LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        var method = typeof(LythonRuntime).GetMethod("CreateFont", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CreateFont not found.");
        return method.Invoke(null, [Array.Empty<object>(), span, context])
            ?? throw new InvalidOperationException("CreateFont returned null.");
    }

    [Fact]
    public void StyleValueCommitsExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = CreateFont(context, span);
        Assert.Equal(128L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void ContextFreeDefaultsStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        _ = CreateFont(null, null);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}