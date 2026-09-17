using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: constructed decimals own their storage; the factories always carry
/// a governor, so there is no ungoverned case.
// M05: adopted results additionally hold one 128 B pool entry each, plus tier
// growth where the shared pool crosses a backing boundary.
/// </summary>
public sealed class DecimalValueAccountingTests
{
    private static object CreateDecimal(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var method = typeof(LythonRuntime).GetMethod("DecimalCtor", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DecimalCtor not found.");
        return method.Invoke(null, [Array.Empty<object>(), span, context])
            ?? throw new InvalidOperationException("DecimalCtor returned null.");
    }

    [Fact]
    public void ConstructedDecimalsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = CreateDecimal(context, span);
        Assert.Equal(224L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = CreateDecimal(context, span);
        Assert.Equal(416L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}