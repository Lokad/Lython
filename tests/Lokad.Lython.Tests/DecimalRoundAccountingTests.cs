using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Rounded Decimal results own their storage like other Decimal results.
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
    public void RoundDecimalResultsAreFreshAndFunded()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        Assert.Equal(1.5m, Assert.IsType<PyDecimal>(InvokeRound(context, span, new PyDecimal(1.54m), new BigInteger(1))).Value);
        Assert.Equal(1.6m, Assert.IsType<PyDecimal>(InvokeRound(context, span, new PyDecimal(1.55m), new BigInteger(1))).Value);
        var five = new PyDecimal(5m);
        var retained = context.MemoryGovernor.CurrentCommittedBytes;
        var rounded = Assert.IsType<PyDecimal>(InvokeRound(context, span, five, BigInteger.Zero));
        Assert.NotSame(five, rounded);
        Assert.Equal(5m, rounded.Value);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > retained);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
