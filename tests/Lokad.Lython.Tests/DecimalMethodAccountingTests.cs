using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: decimal method results own their storage like operator results;
/// aliasing winners (min/max, single-digit rotate) stay free.
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
    public void DecimalMethodResultsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var one = new PyDecimal(1m);
        var two = new PyDecimal(2m);
        _ = InvokeMethod(one, "copy_abs", context, span);
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeMethod(two, "sqrt", context, span);
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeMethod(one, "quantize", context, span, new PyDecimal(1m));
        Assert.Equal(3L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        var minAlias = InvokeMethod(one, "min", context, span, two);
        Assert.Same(one, minAlias);
        Assert.Equal(3L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeMethod(two, "min", context, span, one);
        Assert.Equal(4L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        var five = new PyDecimal(5m);
        var rotateAlias = InvokeMethod(five, "rotate", context, span, new BigInteger(1));
        Assert.Same(five, rotateAlias);
        Assert.Equal(4L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeMethod(one, "remainder_near", context, span, two);
        Assert.Equal(5L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
