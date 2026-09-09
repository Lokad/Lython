using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: constructed date/time values commit one slot each; the factories
/// always carry a governor, so there is no ungoverned case.
/// </summary>
public sealed class DateTimeValueAccountingTests
{
    private static CallArgumentValue[] Ints(params int[] values)
    {
        var arguments = new CallArgumentValue[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            arguments[i] = CallArgumentValue.Positional(new BigInteger(values[i]));
        }

        return arguments;
    }

    [Fact]
    public void ConstructedValuesCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = PyDateTimeOps.CreateDateTime(Ints(2024, 1, 1), span, context);
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = PyDateTimeOps.CreateDate(Ints(2024, 1, 1), span, context);
        _ = PyDateTimeOps.CreateTime(Ints(1, 2, 3), span, context);
        _ = PyDateTimeOps.CreateTimedelta([], span, context);
        Assert.Equal(4L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}