using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: clock-info objects own a shell beside shared implementation labels
/// (fixed engine vocabulary), so retained results accumulate while repeated
/// label reads alias stably.
/// </summary>
public sealed class ClockInfoAccountingTests
{
    private static object GetClockInfo(LythonRuntime.ExecutionContext context, LythonSourceSpan span, string name)
    {
        var type = typeof(LythonRuntime).GetNestedType("TimeModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TimeModule not found.");
        var method = type.GetMethod("GetClockInfo", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GetClockInfo not found.");
        return method.Invoke(null, [new object[] { PyString.FromString(name) }, span, context])!;
    }

    private static PyString ImplementationOf(object clockInfo, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        Assert.True(PyMemberAccess.TryResolve(clockInfo, "implementation", context, span, out var value));
        return Assert.IsType<PyString>(value);
    }

    [Fact]
    public void ClockInfoCommitsShellAndSharesLabels()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var first = ImplementationOf(GetClockInfo(context, span, "time"), context, span);
        var second = ImplementationOf(GetClockInfo(context, span, "time"), context, span);
        Assert.Same(first, second);
        Assert.Equal("Lython host UTC wall clock", first.AsString());
        // Two shells (64 each); the shared labels ride free.
        Assert.Equal(128L, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void FailedClockInfoLeaksNothing()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var thrown = Assert.Throws<TargetInvocationException>(() => GetClockInfo(context, span, "bogus"));
        Assert.IsType<LythonRuntimeException>(thrown.InnerException);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
    }
}