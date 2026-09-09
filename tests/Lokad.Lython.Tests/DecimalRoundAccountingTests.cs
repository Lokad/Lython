using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: round() decimal results own their storage like other decimal results;
/// the past-28-digits alias passthrough stays free.
/// </summary>
public sealed class DecimalRoundAccountingTests
{
    private static object InvokeRound(
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span,
        params object[] positional)
    {
        var method = typeof(LythonRuntime).GetMethod("Round", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Round not found.");
        return method.Invoke(null, [positional, span, context])
            ?? throw new InvalidOperationException("Round returned null.");
    }

    [Fact]
    public void RoundDecimalResultsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = InvokeRound(context, span, new PyDecimal(1.54m), new BigInteger(1));
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeRound(context, span, new PyDecimal(1.55m), new BigInteger(1));
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        var five = new PyDecimal(5m);
        var aliased = InvokeRound(context, span, five, new BigInteger(29));
        Assert.Same(five, aliased);
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
