using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: Context.create_decimal/from_float values own their storage like
/// decimal.Decimal(); context conversion creates a fresh admitted value.
/// </summary>
public sealed class DecimalContextValueAccountingTests
{
    private static object InvokeFactory(
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span,
        string name,
        object argument)
    {
        var member = context.DecimalContext.TryGetMember(name, out var value)
            ? value
            : throw new InvalidOperationException(name + " member not found.");
        var callable = (LythonRuntime.ICallable)member;
        return callable.Invoke([CallArgumentValue.Positional(argument)], span, context);
    }


    [Fact]
    public void CreateDecimalCopiesStayFunded()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var existing = InvokeFactory(context, span, "create_decimal", new BigInteger(7));
        var retained = context.MemoryGovernor.CurrentCommittedBytes;
        var copied = InvokeFactory(context, span, "create_decimal", existing);
        Assert.NotSame(existing, copied);
        Assert.Equal(((PyDecimal)existing).Value, ((PyDecimal)copied).Value);
        Assert.Equal(retained + 64L, context.MemoryGovernor.CurrentCommittedBytes);
    }
}
