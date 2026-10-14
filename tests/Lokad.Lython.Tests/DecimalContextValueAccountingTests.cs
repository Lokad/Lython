using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: Context.create_decimal/from_float values own their storage like
/// decimal.Decimal(); Decimal-to-Decimal aliasing stays free.
/// </summary>
public sealed class DecimalContextValueAccountingTests
{
    private static object InvokeFactory(
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span,
        string name,
        object argument)
    {
        var member = PyDecimalContext.Default().TryGetMember(name, out var value)
            ? value
            : throw new InvalidOperationException(name + " member not found.");
        var callable = (LythonRuntime.ICallable)member;
        return callable.Invoke([CallArgumentValue.Positional(argument)], span, context);
    }


    [Fact]
    public void CreateDecimalAliasingStaysFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var existing = InvokeFactory(context, span, "create_decimal", new BigInteger(7));
        var retained = context.MemoryGovernor.CurrentCommittedBytes;
        var aliased = InvokeFactory(context, span, "create_decimal", existing);
        Assert.Same(existing, aliased);
        Assert.Equal(retained, context.MemoryGovernor.CurrentCommittedBytes);
    }
}
