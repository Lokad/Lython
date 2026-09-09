using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: decimal operator results own their storage like constructed decimals;
/// each fresh result commits one 64B slot through the in-scope governor.
/// </summary>
public sealed class DecimalArithmeticAccountingTests
{
    private static object InvokeBinary(
        string name,
        object left,
        object right,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        var method = typeof(LythonRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(name + " not found.");
        object?[] arguments = method.GetParameters().Length == 4
            ? [left, right, context, span]
            : [left, right, span];
        return method.Invoke(null, arguments)
            ?? throw new InvalidOperationException(name + " returned null.");
    }

    private static object InvokeUnary(
        string name,
        object operand,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        var method = typeof(LythonRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(name + " not found.");
        return method.Invoke(null, [operand, context, span])
            ?? throw new InvalidOperationException(name + " returned null.");
    }

    [Fact]
    public void DecimalOperatorResultsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var one = new PyDecimal(1m);
        var two = new PyDecimal(2m);
        _ = InvokeBinary("EvaluateAdd", one, two, context, span);
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeBinary("EvaluateSubtract", one, two, context, span);
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeBinary("EvaluateMultiply", one, two, context, span);
        Assert.Equal(3L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeBinary("EvaluateDivide", one, two, context, span);
        Assert.Equal(4L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeBinary("EvaluateModulo", one, two, context, span);
        Assert.Equal(5L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeBinary("EvaluatePower", two, new BigInteger(2), context, span);
        Assert.Equal(6L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeUnary("EvaluateUnaryMinus", one, context, span);
        Assert.Equal(7L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = InvokeUnary("EvaluateAbsolute", one, context, span);
        Assert.Equal(8L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        var abs = typeof(LythonRuntime).GetMethod("Abs", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Abs not found.");
        _ = abs.Invoke(null, [new object[] { one }, span, context]);
        Assert.Equal(9L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
