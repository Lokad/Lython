using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Decimal method results own their storage like operator results;
/// the existing single-digit rotate path preserves its alias.
/// </summary>
public sealed class DecimalMethodAccountingTests
{
    private static object InvokeMethod(
        PyDecimal receiver,
        string name,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span,
        params object[] positional)
    {
        if (!LythonRuntime.DecimalMembers.TryGetMember(receiver, name, out var member) || member is null)
        {
            throw new InvalidOperationException(name + " member not found.");
        }
        var callable = (LythonRuntime.ICallable)member;
        var arguments = new CallArgumentValue[positional.Length];
        for (var i = 0; i < positional.Length; i++)
        {
            arguments[i] = CallArgumentValue.Positional(positional[i]);
        }
        return callable.Invoke(arguments, span, context);
    }

    [Fact]
    public void DecimalMethodResultsPreserveValuesAndAliases()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var one = new PyDecimal(1m);
        var two = new PyDecimal(2m);
        _ = InvokeMethod(one, "copy_abs", context, span);
        _ = InvokeMethod(two, "sqrt", context, span);
        _ = InvokeMethod(one, "quantize", context, span, new PyDecimal(1m));
        var beforeMin = context.MemoryGovernor.CurrentCommittedBytes;
        var minimum = Assert.IsType<PyDecimal>(InvokeMethod(one, "min", context, span, two));
        Assert.NotSame(one, minimum);
        Assert.Equal(1m, minimum.Value);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > beforeMin);
        _ = InvokeMethod(two, "min", context, span, one);
        var five = new PyDecimal(5m);
        var rotateAlias = InvokeMethod(five, "rotate", context, span, new BigInteger(1));
        Assert.Same(five, rotateAlias);
        _ = InvokeMethod(one, "remainder_near", context, span, two);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
